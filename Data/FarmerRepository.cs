using System.Data;
using Microsoft.Data.SqlClient;

namespace AgriFlow.Data;

public sealed record FarmerOverview(int SeedItems, decimal TotalSeedStock, int ActiveBatches, int UpcomingHarvests, int OpenOrders);
public sealed record FarmerStockRow(int Id, string CropName, int MaturityDays, decimal CurrentStock, string Unit, DateTime UpdatedAt);
public sealed record FarmerBatchRow(int Id, string CropName, decimal QuantityPlanted, DateTime PlantingDate, DateTime PredictedHarvestDate, string Status, decimal RemainingStock);
public sealed record FarmerListingRow(int Id, int BatchId, string CropName, decimal AvailableQuantity, string Unit, decimal PricePerUnit, bool IsActive, DateTime CreatedAt);
public sealed record FarmerOrderRow(int OrderId, string CropName, decimal Quantity, string Unit, decimal PricePerUnit, decimal TotalAmount, string Status, DateTime CreatedAt);
public sealed record MarketplaceRow(int Id, string CropName, decimal AvailableQuantity, string Unit, decimal PricePerUnit, string FarmerName, string FarmName, DateTime CreatedAt);

public sealed class FarmerRepository
{
    private readonly DbConnectionFactory _db;
    public FarmerRepository(DbConnectionFactory db) => _db = db;

    public async Task<FarmerOverview> GetOverviewAsync(int farmerId)
    {
        const string sql = @"SELECT
            (SELECT COUNT(*) FROM supply_stock WHERE FarmerId=@FarmerId),
            ISNULL((SELECT SUM(CurrentStock) FROM supply_stock WHERE FarmerId=@FarmerId),0),
            (SELECT COUNT(*) FROM planting_batch WHERE FarmerId=@FarmerId AND Status='active'),
            (SELECT COUNT(*) FROM planting_batch WHERE FarmerId=@FarmerId AND PredictedHarvestDate BETWEEN CAST(GETDATE() AS date) AND DATEADD(day,30,CAST(GETDATE() AS date))),
            (SELECT COUNT(DISTINCT oi.OrderId) FROM order_items oi INNER JOIN harvest_listing hl ON hl.Id=oi.HarvestListingId INNER JOIN orders o ON o.Id=oi.OrderId WHERE hl.FarmerId=@FarmerId AND o.Status IN ('pending','confirmed','dispatched'));";
        await using var connection = _db.CreateConnection(); await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection); command.Parameters.AddWithValue("@FarmerId", farmerId);
        await using var reader = await command.ExecuteReaderAsync(); await reader.ReadAsync();
        return new FarmerOverview(reader.GetInt32(0), reader.GetDecimal(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4));
    }

    public async Task<IReadOnlyList<FarmerStockRow>> GetStockAsync(int farmerId)
    {
        const string sql = "SELECT Id,CropName,MaturityDays,CurrentStock,Unit,UpdatedAt FROM supply_stock WHERE FarmerId=@FarmerId ORDER BY CropName";
        var rows = new List<FarmerStockRow>();
        await using var connection = _db.CreateConnection(); await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection); command.Parameters.AddWithValue("@FarmerId", farmerId);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) rows.Add(new FarmerStockRow(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2), reader.GetDecimal(3), reader.GetString(4), reader.GetDateTime(5)));
        return rows;
    }

    public async Task AddStockAsync(int farmerId, string cropName, int maturityDays, decimal quantity, string unit)
    {
        const string sql = @"UPDATE supply_stock SET CurrentStock=CurrentStock+@Quantity, MaturityDays=@MaturityDays, UpdatedAt=SYSUTCDATETIME() WHERE FarmerId=@FarmerId AND CropName=@CropName AND Unit=@Unit;
            IF @@ROWCOUNT=0 INSERT INTO supply_stock(FarmerId,CropName,MaturityDays,CurrentStock,Unit) VALUES(@FarmerId,@CropName,@MaturityDays,@Quantity,@Unit);";
        await using var connection = _db.CreateConnection(); await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@FarmerId", farmerId); command.Parameters.AddWithValue("@CropName", cropName.Trim()); command.Parameters.AddWithValue("@MaturityDays", maturityDays); command.Parameters.AddWithValue("@Quantity", quantity); command.Parameters.AddWithValue("@Unit", unit.Trim());
        await command.ExecuteNonQueryAsync();
    }

    public async Task RecordPlantingAsync(int farmerId, int stockId, decimal quantity, DateTime plantingDate)
    {
        await using var connection = _db.CreateConnection(); await connection.OpenAsync(); await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        const string check = "SELECT MaturityDays,CurrentStock FROM supply_stock WHERE Id=@StockId AND FarmerId=@FarmerId";
        await using var checkCommand = new SqlCommand(check, connection, transaction); checkCommand.Parameters.AddWithValue("@StockId", stockId); checkCommand.Parameters.AddWithValue("@FarmerId", farmerId);
        await using var reader = await checkCommand.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("The selected seed stock was not found.");
        var maturity = reader.GetInt32(0); var stock = reader.GetDecimal(1); await reader.CloseAsync();
        if (quantity <= 0) throw new InvalidOperationException("Planting quantity must be greater than zero.");
        if (quantity > stock) throw new InvalidOperationException($"Not enough seed. Only {stock:0.##} kg is available.");
        const string update = "UPDATE supply_stock SET CurrentStock=CurrentStock-@Quantity,UpdatedAt=SYSUTCDATETIME() WHERE Id=@StockId AND FarmerId=@FarmerId;";
        await using var updateCommand = new SqlCommand(update, connection, transaction); updateCommand.Parameters.AddWithValue("@Quantity", quantity); updateCommand.Parameters.AddWithValue("@StockId", stockId); updateCommand.Parameters.AddWithValue("@FarmerId", farmerId); await updateCommand.ExecuteNonQueryAsync();
        const string insert = "INSERT INTO planting_batch(FarmerId,SupplyStockId,QuantityPlanted,PlantingDate,PredictedHarvestDate) VALUES(@FarmerId,@StockId,@Quantity,@PlantingDate,DATEADD(day,@MaturityDays,@PlantingDate));";
        await using var insertCommand = new SqlCommand(insert, connection, transaction); insertCommand.Parameters.AddWithValue("@FarmerId", farmerId); insertCommand.Parameters.AddWithValue("@StockId", stockId); insertCommand.Parameters.AddWithValue("@Quantity", quantity); insertCommand.Parameters.AddWithValue("@PlantingDate", plantingDate.Date); insertCommand.Parameters.AddWithValue("@MaturityDays", maturity); await insertCommand.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
    }

    public async Task<IReadOnlyList<FarmerBatchRow>> GetBatchesAsync(int farmerId)
    {
        const string sql = @"SELECT pb.Id,ss.CropName,pb.QuantityPlanted,pb.PlantingDate,pb.PredictedHarvestDate,pb.Status,ss.CurrentStock FROM planting_batch pb INNER JOIN supply_stock ss ON ss.Id=pb.SupplyStockId WHERE pb.FarmerId=@FarmerId ORDER BY pb.PlantingDate DESC";
        var rows = new List<FarmerBatchRow>(); await using var connection = _db.CreateConnection(); await connection.OpenAsync(); await using var command = new SqlCommand(sql, connection); command.Parameters.AddWithValue("@FarmerId", farmerId); await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) rows.Add(new FarmerBatchRow(reader.GetInt32(0), reader.GetString(1), reader.GetDecimal(2), reader.GetDateTime(3), reader.GetDateTime(4), reader.GetString(5), reader.GetDecimal(6))); return rows;
    }

    public async Task<IReadOnlyList<FarmerListingRow>> GetListingsAsync(int farmerId)
    {
        const string sql = "SELECT Id,BatchId,CropName,AvailableQuantity,Unit,PricePerUnit,IsActive,CreatedAt FROM harvest_listing WHERE FarmerId=@FarmerId ORDER BY CreatedAt DESC";
        var rows = new List<FarmerListingRow>();
        await using var connection = _db.CreateConnection(); await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection); command.Parameters.AddWithValue("@FarmerId", farmerId);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) rows.Add(new FarmerListingRow(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetDecimal(3), reader.GetString(4), reader.GetDecimal(5), reader.GetBoolean(6), reader.GetDateTime(7)));
        return rows;
    }

    public async Task CreateListingAsync(int farmerId, int batchId, decimal quantity, decimal price, string unit)
    {
        const string sql = @"INSERT INTO harvest_listing(FarmerId,BatchId,CropName,AvailableQuantity,Unit,PricePerUnit)
            SELECT @FarmerId, pb.Id, ss.CropName, @Quantity, @Unit, @Price
            FROM planting_batch pb INNER JOIN supply_stock ss ON ss.Id=pb.SupplyStockId
            WHERE pb.Id=@BatchId AND pb.FarmerId=@FarmerId AND pb.Status='active';";
        await using var connection = _db.CreateConnection(); await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@FarmerId", SqlDbType.Int).Value = farmerId;
        command.Parameters.Add("@BatchId", SqlDbType.Int).Value = batchId;
        command.Parameters.Add("@Quantity", SqlDbType.Decimal).Value = quantity;
        command.Parameters.Add("@Unit", SqlDbType.NVarChar, 20).Value = unit;
        command.Parameters.Add("@Price", SqlDbType.Decimal).Value = price;
        if (await command.ExecuteNonQueryAsync() == 0) throw new InvalidOperationException("The selected planting batch was not found or is no longer active.");
    }

    public async Task SetListingStatusAsync(int farmerId, int listingId, bool active)
    {
        const string sql = "UPDATE harvest_listing SET IsActive=@Active WHERE Id=@ListingId AND FarmerId=@FarmerId";
        await using var connection = _db.CreateConnection(); await connection.OpenAsync(); await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Active", active); command.Parameters.AddWithValue("@ListingId", listingId); command.Parameters.AddWithValue("@FarmerId", farmerId); await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<FarmerOrderRow>> GetOrdersAsync(int farmerId)
    {
        const string sql = @"SELECT o.Id,hl.CropName,oi.QuantityOrdered,hl.Unit,oi.PriceAtPurchase,o.TotalAmount,o.Status,o.CreatedAt
            FROM orders o INNER JOIN order_items oi ON oi.OrderId=o.Id INNER JOIN harvest_listing hl ON hl.Id=oi.HarvestListingId
            WHERE hl.FarmerId=@FarmerId ORDER BY o.CreatedAt DESC";
        var rows = new List<FarmerOrderRow>(); await using var connection = _db.CreateConnection(); await connection.OpenAsync(); await using var command = new SqlCommand(sql, connection); command.Parameters.AddWithValue("@FarmerId", farmerId); await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) rows.Add(new FarmerOrderRow(reader.GetInt32(0), reader.GetString(1), reader.GetDecimal(2), reader.GetString(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetString(6), reader.GetDateTime(7))); return rows;
    }

    public async Task UpdateOrderStatusAsync(int farmerId, int orderId, string status)
    {
        const string sql = @"UPDATE o SET Status=@Status FROM orders o WHERE o.Id=@OrderId AND EXISTS
            (SELECT 1 FROM order_items oi INNER JOIN harvest_listing hl ON hl.Id=oi.HarvestListingId WHERE oi.OrderId=o.Id AND hl.FarmerId=@FarmerId)";
        await using var connection = _db.CreateConnection(); await connection.OpenAsync(); await using var command = new SqlCommand(sql, connection); command.Parameters.AddWithValue("@Status", status); command.Parameters.AddWithValue("@OrderId", orderId); command.Parameters.AddWithValue("@FarmerId", farmerId); await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<MarketplaceRow>> GetMarketplaceAsync()
    {
        const string sql = @"SELECT hl.Id,hl.CropName,hl.AvailableQuantity,hl.Unit,hl.PricePerUnit,u.FullName,fp.FarmName,hl.CreatedAt
            FROM harvest_listing hl INNER JOIN farmer_profile fp ON fp.UserId=hl.FarmerId INNER JOIN app_user u ON u.Id=hl.FarmerId
            WHERE hl.IsActive=1 AND hl.AvailableQuantity>0 ORDER BY hl.CreatedAt DESC";
        var rows = new List<MarketplaceRow>(); await using var connection = _db.CreateConnection(); await connection.OpenAsync(); await using var command = new SqlCommand(sql, connection); await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) rows.Add(new MarketplaceRow(reader.GetInt32(0), reader.GetString(1), reader.GetDecimal(2), reader.GetString(3), reader.GetDecimal(4), reader.GetString(5), reader.GetString(6), reader.GetDateTime(7))); return rows;
    }
}
