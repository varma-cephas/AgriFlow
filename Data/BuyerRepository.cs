using System.Data;
using Microsoft.Data.SqlClient;

namespace AgriFlow.Data;

public sealed record BuyerOrderRow(int OrderId, string CropName, decimal Quantity, string Unit, decimal UnitPrice, decimal TotalAmount, string Status, string FarmerName, string FarmName, DateTime CreatedAt);
public sealed record BuyerInvoiceRow(int OrderId, string InvoiceNumber, string CropName, decimal Quantity, string Unit, decimal TotalAmount, string Status, DateTime CreatedAt);

public sealed class BuyerRepository
{
    private readonly DbConnectionFactory _db;
    public BuyerRepository(DbConnectionFactory db) => _db = db;

    public async Task<int> PlaceOrderAsync(int buyerId, int listingId, decimal quantity)
    {
        if (quantity <= 0) throw new InvalidOperationException("Order quantity must be greater than zero.");

        await using var connection = _db.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);

        try
        {
            const string reserveSql = @"UPDATE harvest_listing
                SET AvailableQuantity = AvailableQuantity - @Quantity
                WHERE Id=@ListingId AND IsActive=1 AND AvailableQuantity >= @Quantity;";
            await using var reserve = new SqlCommand(reserveSql, connection, transaction);
            reserve.Parameters.Add("@ListingId", SqlDbType.Int).Value = listingId;
            reserve.Parameters.Add("@Quantity", SqlDbType.Decimal).Value = quantity;
            reserve.Parameters["@Quantity"].Precision = 10;
            reserve.Parameters["@Quantity"].Scale = 2;
            if (await reserve.ExecuteNonQueryAsync() != 1)
                throw new InvalidOperationException("This listing does not have enough quantity or is no longer available.");

            const string detailsSql = "SELECT PricePerUnit FROM harvest_listing WHERE Id=@ListingId AND IsActive=1;";
            decimal price;
            await using (var details = new SqlCommand(detailsSql, connection, transaction))
            {
                details.Parameters.Add("@ListingId", SqlDbType.Int).Value = listingId;
                var value = await details.ExecuteScalarAsync();
                if (value is null) throw new InvalidOperationException("The selected listing is no longer available.");
                price = Convert.ToDecimal(value);
            }

            const string orderSql = @"INSERT INTO orders(CustomerId,TotalAmount,Status)
                OUTPUT INSERTED.Id VALUES(@BuyerId,@TotalAmount,'pending');";
            int orderId;
            await using (var order = new SqlCommand(orderSql, connection, transaction))
            {
                order.Parameters.Add("@BuyerId", SqlDbType.Int).Value = buyerId;
                order.Parameters.Add("@TotalAmount", SqlDbType.Decimal).Value = quantity * price;
                order.Parameters["@TotalAmount"].Precision = 10;
                order.Parameters["@TotalAmount"].Scale = 2;
                orderId = Convert.ToInt32(await order.ExecuteScalarAsync());
            }

            const string itemSql = @"INSERT INTO order_items(OrderId,HarvestListingId,QuantityOrdered,PriceAtPurchase)
                VALUES(@OrderId,@ListingId,@Quantity,@Price);";
            await using (var item = new SqlCommand(itemSql, connection, transaction))
            {
                item.Parameters.Add("@OrderId", SqlDbType.Int).Value = orderId;
                item.Parameters.Add("@ListingId", SqlDbType.Int).Value = listingId;
                item.Parameters.Add("@Quantity", SqlDbType.Decimal).Value = quantity;
                item.Parameters.Add("@Price", SqlDbType.Decimal).Value = price;
                item.Parameters["@Quantity"].Precision = 10;
                item.Parameters["@Quantity"].Scale = 2;
                item.Parameters["@Price"].Precision = 10;
                item.Parameters["@Price"].Scale = 2;
                await item.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            return orderId;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<IReadOnlyList<BuyerOrderRow>> GetOrdersAsync(int buyerId)
    {
        const string sql = @"SELECT o.Id,hl.CropName,oi.QuantityOrdered,hl.Unit,oi.PriceAtPurchase,
                o.TotalAmount,o.Status,u.FullName,fp.FarmName,o.CreatedAt
            FROM orders o
            INNER JOIN order_items oi ON oi.OrderId=o.Id
            INNER JOIN harvest_listing hl ON hl.Id=oi.HarvestListingId
            INNER JOIN app_user u ON u.Id=hl.FarmerId
            INNER JOIN farmer_profile fp ON fp.UserId=hl.FarmerId
            WHERE o.CustomerId=@BuyerId ORDER BY o.CreatedAt DESC;";
        var rows = new List<BuyerOrderRow>();
        await using var connection = _db.CreateConnection();
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@BuyerId", SqlDbType.Int).Value = buyerId;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            rows.Add(new BuyerOrderRow(reader.GetInt32(0), reader.GetString(1), reader.GetDecimal(2), reader.GetString(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetString(6), reader.GetString(7), reader.GetString(8), reader.GetDateTime(9)));
        return rows;
    }

    public async Task<IReadOnlyList<BuyerInvoiceRow>> GetInvoicesAsync(int buyerId)
    {
        const string sql = @"SELECT o.Id,oi.QuantityOrdered,hl.CropName,hl.Unit,o.TotalAmount,o.Status,o.CreatedAt
            FROM orders o INNER JOIN order_items oi ON oi.OrderId=o.Id INNER JOIN harvest_listing hl ON hl.Id=oi.HarvestListingId
            WHERE o.CustomerId=@BuyerId AND o.Status IN ('delivered','invoiced','paid') ORDER BY o.CreatedAt DESC;";
        var rows = new List<BuyerInvoiceRow>();
        await using var connection = _db.CreateConnection();
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@BuyerId", SqlDbType.Int).Value = buyerId;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var orderId = reader.GetInt32(0);
            rows.Add(new BuyerInvoiceRow(orderId, $"INV-{orderId:0000}", reader.GetString(2), reader.GetDecimal(1), reader.GetString(3), reader.GetDecimal(4), reader.GetString(5), reader.GetDateTime(6)));
        }
        return rows;
    }

    public async Task PayInvoiceAsync(int buyerId, int orderId)
    {
        const string sql = @"UPDATE orders SET Status='paid' WHERE Id=@OrderId AND CustomerId=@BuyerId AND Status IN ('delivered','invoiced');";
        await using var connection = _db.CreateConnection();
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@OrderId", SqlDbType.Int).Value = orderId;
        command.Parameters.Add("@BuyerId", SqlDbType.Int).Value = buyerId;
        if (await command.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("This invoice is not ready for payment.");
    }
}
