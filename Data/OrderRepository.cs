using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace AgriFlow.Data;

public record ListingDetail(
    int Id,
    string ListingCode,
    string BatchCode,
    string Status,
    string CropName,
    string Variety,
    string? Supplier,
    string Grade,
    string Unit,
    decimal? UnitSizeKg,
    decimal PricePerUnit,
    decimal MinOrderQty,
    decimal AvailableQty,
    string? Description,
    string FarmName,
    string Province,
    string District,
    DateTime PlantingDate,
    DateTime? HarvestDate,
    DateTime ListedAt,
    int HarvestsCompleted,
    int OrdersDelivered,
    DateTime FarmMemberSince);

public record BuyerInfo(string Status, string Address);

public record PlaceOrderRequest(
    int ListingId, int UserId, int BuyerBusinessId,
    decimal Quantity, DateTime DeliveryDate, string DeliveryAddress);

public record PlaceOrderResult(
    bool Success, string? ErrorMessage = null, string? OrderNumber = null,
    decimal Total = 0, string FarmName = "");

public class OrderRepository
{
    private readonly DbConnectionFactory _db;

    public OrderRepository(DbConnectionFactory db) => _db = db;

    // Only listings from verified farms; Available or SoldOut (so a stale page can still say "sold out").
    // The farmer's National ID is never selected (BR-15).
    public async Task<ListingDetail?> GetListingDetailAsync(int listingId)
    {
        const string sql = @"
            SELECT l.Id, l.ListingCode, pb.BatchCode, l.Status, c.Name, ss.Variety, ss.Supplier,
                   l.Grade, l.Unit, l.UnitSizeKg, l.PricePerUnit, l.MinOrderQty, l.AvailableQty, l.Description,
                   fp.FarmName, fp.Province, fp.District,
                   pb.PlantingDate, pb.HarvestDate, l.CreatedAt,
                   (SELECT COUNT(*) FROM planting_batch h
                     WHERE h.FarmerId = l.FarmerId AND h.Status = 'Harvested') AS HarvestsCompleted,
                   (SELECT COUNT(*) FROM order_item oi
                     INNER JOIN listing ol ON ol.Id = oi.ListingId
                     WHERE ol.FarmerId = l.FarmerId AND oi.Status = 'Delivered') AS OrdersDelivered,
                   fp.CreatedAt
            FROM listing l
            INNER JOIN farmer_profile fp ON fp.UserId = l.FarmerId
            INNER JOIN crop c ON c.Id = l.CropId
            INNER JOIN planting_batch pb ON pb.Id = l.BatchId
            INNER JOIN seed_stock ss ON ss.Id = pb.SeedStockId
            WHERE l.Id = @Id
              AND fp.VerificationStatus = 'Verified'
              AND l.Status IN ('Available', 'SoldOut');";

        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", listingId);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        return new ListingDetail(
            r.GetInt32(0),
            r.GetString(1),
            r.GetString(2),
            r.GetString(3),
            r.GetString(4),
            r.GetString(5),
            r.IsDBNull(6) ? null : r.GetString(6),
            r.GetString(7),
            r.GetString(8),
            r.IsDBNull(9) ? null : r.GetDecimal(9),
            r.GetDecimal(10),
            r.GetDecimal(11),
            r.GetDecimal(12),
            r.IsDBNull(13) ? null : r.GetString(13),
            r.GetString(14),
            r.GetString(15),
            r.GetString(16),
            r.GetDateTime(17),
            r.IsDBNull(18) ? null : r.GetDateTime(18),
            r.GetDateTime(19),
            r.GetInt32(20),
            r.GetInt32(21),
            r.GetDateTime(22));
    }

    // Buyer's verification status and the address saved on their business.
    public async Task<BuyerInfo?> GetBuyerInfoAsync(int buyerBusinessId)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(
            "SELECT VerificationStatus, Address, City FROM buyer_business WHERE Id = @Id;", conn);
        cmd.Parameters.AddWithValue("@Id", buyerBusinessId);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return new BuyerInfo(r.GetString(0), r.GetString(1) + ", " + r.GetString(2));
    }

    // One transaction: lock listing -> validate -> create order + order_item -> reduce stock -> commit.
    public async Task<PlaceOrderResult> PlaceOrderAsync(PlaceOrderRequest req)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();

        try
        {
            // Rule 1: only verified businesses can buy.
            string? buyerStatus;
            await using (var cmd = new SqlCommand(
                "SELECT VerificationStatus FROM buyer_business WHERE Id = @Id;", conn, tx))
            {
                cmd.Parameters.AddWithValue("@Id", req.BuyerBusinessId);
                buyerStatus = await cmd.ExecuteScalarAsync() as string;
            }
            if (buyerStatus != "Verified")
            {
                await tx.RollbackAsync();
                return new PlaceOrderResult(false, "Your business must be verified by an AgriFlow admin before you can order.");
            }

            // Lock the listing row. A second buyer must wait here until we commit,
            // then sees the reduced stock (BR-8: two buyers can't take the same last units).
            const string lockSql = @"
                SELECT l.Status, l.AvailableQty, l.MinOrderQty, l.PricePerUnit, l.Unit,
                       fp.VerificationStatus, fp.FarmName, c.Name
                FROM listing l WITH (UPDLOCK, ROWLOCK)
                INNER JOIN farmer_profile fp ON fp.UserId = l.FarmerId
                INNER JOIN crop c ON c.Id = l.CropId
                WHERE l.Id = @Id;";

            bool found;
            string status = "", unit = "", farmStatus = "", farmName = "", cropName = "";
            decimal available = 0, minQty = 0, price = 0;

            await using (var cmd = new SqlCommand(lockSql, conn, tx))
            {
                cmd.Parameters.AddWithValue("@Id", req.ListingId);
                await using var r = await cmd.ExecuteReaderAsync();
                found = await r.ReadAsync();
                if (found)
                {
                    status = r.GetString(0);
                    available = r.GetDecimal(1);
                    minQty = r.GetDecimal(2);
                    price = r.GetDecimal(3);
                    unit = r.GetString(4);
                    farmStatus = r.GetString(5);
                    farmName = r.GetString(6);
                    cropName = r.GetString(7);
                }
            }

            if (!found || farmStatus != "Verified" || (status != "Available" && status != "SoldOut"))
            {
                await tx.RollbackAsync();
                return new PlaceOrderResult(false, "This listing is no longer available.");
            }
            if (status != "Available" || available <= 0)
            {
                await tx.RollbackAsync();
                return new PlaceOrderResult(false, "Sorry, this listing has just sold out.");
            }
            if (req.Quantity < minQty)
            {
                await tx.RollbackAsync();
                return new PlaceOrderResult(false, $"The minimum order is {Fmt(minQty)} {Plural(unit, minQty)}.");
            }
            if (req.Quantity > available)
            {
                await tx.RollbackAsync();
                return new PlaceOrderResult(false, $"Only {Fmt(available)} {Plural(unit, available)} left. Please lower your quantity.");
            }

            // The price comes from the locked database row, never from the form.
            var total = Math.Round(req.Quantity * price, 2);

            // 1. Create the order with a temporary unique number.
            int orderId;
            await using (var cmd = new SqlCommand(@"
                INSERT INTO orders (OrderNumber, BuyerBusinessId, PlacedByUserId, DeliveryAddress, DeliveryDate, Status, TotalAmount)
                OUTPUT INSERTED.Id
                VALUES (LEFT(CONVERT(NVARCHAR(36), NEWID()), 20), @BuyerId, @UserId, @Address, @DeliveryDate, 'Placed', @Total);", conn, tx))
            {
                cmd.Parameters.AddWithValue("@BuyerId", req.BuyerBusinessId);
                cmd.Parameters.AddWithValue("@UserId", req.UserId);
                cmd.Parameters.AddWithValue("@Address", req.DeliveryAddress);
                cmd.Parameters.Add("@DeliveryDate", SqlDbType.Date).Value = req.DeliveryDate.Date;
                cmd.Parameters.AddWithValue("@Total", total);
                orderId = (int)(await cmd.ExecuteScalarAsync())!;
            }

            // 2. Give it the real order number, e.g. AF-00007.
            string orderNumber;
            await using (var cmd = new SqlCommand(@"
                UPDATE orders SET OrderNumber = N'AF-' + FORMAT(Id, '00000')
                OUTPUT INSERTED.OrderNumber
                WHERE Id = @Id;", conn, tx))
            {
                cmd.Parameters.AddWithValue("@Id", orderId);
                orderNumber = (string)(await cmd.ExecuteScalarAsync())!;
            }

            // 3. Order line: unit price copied, status Placed.
            await using (var cmd = new SqlCommand(@"
                INSERT INTO order_item (OrderId, ListingId, QtyRequested, UnitPrice, Status)
                VALUES (@OrderId, @ListingId, @Qty, @Price, 'Placed');", conn, tx))
            {
                cmd.Parameters.AddWithValue("@OrderId", orderId);
                cmd.Parameters.AddWithValue("@ListingId", req.ListingId);
                cmd.Parameters.AddWithValue("@Qty", req.Quantity);
                cmd.Parameters.AddWithValue("@Price", price);
                await cmd.ExecuteNonQueryAsync();
            }

            // 4. Reduce the stock. The extra conditions are a second safety net.
            int updated;
            await using (var cmd = new SqlCommand(@"
                UPDATE listing
                SET AvailableQty = AvailableQty - @Qty,
                    Status = CASE WHEN AvailableQty - @Qty <= 0 THEN 'SoldOut' ELSE Status END,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE Id = @Id AND Status = 'Available' AND AvailableQty >= @Qty;", conn, tx))
            {
                cmd.Parameters.AddWithValue("@Id", req.ListingId);
                cmd.Parameters.AddWithValue("@Qty", req.Quantity);
                updated = await cmd.ExecuteNonQueryAsync();
            }
            if (updated != 1)
            {
                await tx.RollbackAsync();
                return new PlaceOrderResult(false, "Sorry, this listing has just sold out.");
            }

            // 5. Audit log
            await using (var cmd = new SqlCommand(@"
                INSERT INTO audit_log (ActorUserId, Entity, EntityId, Action, Summary, Result)
                VALUES (@ActorId, 'Order', @EntityId, 'Placed order', @Summary, 'Success');", conn, tx))
            {
                cmd.Parameters.AddWithValue("@ActorId", req.UserId);
                cmd.Parameters.AddWithValue("@EntityId", orderId.ToString());
                cmd.Parameters.AddWithValue("@Summary",
                    $"{orderNumber}: {cropName} {Fmt(req.Quantity)} {Plural(unit, req.Quantity)} from {farmName}");
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            return new PlaceOrderResult(true, null, orderNumber, total, farmName);
        }
        catch (SqlException ex) when (ex.Number == 1205)
        {
            // Deadlock victim: SQL Server already rolled the transaction back.
            return new PlaceOrderResult(false, "The system was busy. Please try placing your order again.");
        }
    }

    private static string Fmt(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Plural(string unit, decimal qty) => qty == 1 ? unit : unit + "s";
}