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
            (SELECT COUNT(*) FROM seed_stock WHERE FarmerId=@FarmerId),
            ISNULL((SELECT SUM(Quantity) FROM seed_stock WHERE FarmerId=@FarmerId),0),
            (SELECT COUNT(*) FROM planting_batch WHERE FarmerId=@FarmerId AND Status='Active'),
            (SELECT COUNT(*) FROM planting_batch WHERE FarmerId=@FarmerId AND PredictedHarvestDate BETWEEN CAST(GETDATE() AS date) AND DATEADD(day,30,CAST(GETDATE() AS date))),
            (SELECT COUNT(DISTINCT oi.OrderId) FROM order_item oi INNER JOIN listing l ON l.Id=oi.ListingId INNER JOIN orders o ON o.Id=oi.OrderId WHERE l.FarmerId=@FarmerId AND o.Status IN ('Reserved','Placed','Confirmed','Dispatched'));";
        await using var connection = _db.CreateConnection(); await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection); command.Parameters.Add("@FarmerId", SqlDbType.Int).Value = farmerId;
        await using var reader = await command.ExecuteReaderAsync(); await reader.ReadAsync();
        return new FarmerOverview(reader.GetInt32(0), reader.GetDecimal(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4));
    }

    public async Task<IReadOnlyList<FarmerStockRow>> GetStockAsync(int farmerId)
    {
        const string sql = @"SELECT ss.Id,c.Name,c.MaturityDays,ss.Quantity,ss.Unit,ss.UpdatedAt FROM seed_stock ss INNER JOIN crop c ON c.Id=ss.CropId WHERE ss.FarmerId=@FarmerId ORDER BY c.Name;";
        var rows = new List<FarmerStockRow>(); await using var connection = _db.CreateConnection(); await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection); command.Parameters.Add("@FarmerId", SqlDbType.Int).Value = farmerId;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) rows.Add(new FarmerStockRow(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2), reader.GetDecimal(3), reader.GetString(4), reader.GetDateTime(5)));
        return rows;
    }

    public async Task AddStockAsync(int farmerId, string cropName, int maturityDays, decimal quantity, string unit)
    {
        await using var connection = _db.CreateConnection(); await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            int cropId;
            const string cropSql = @"IF NOT EXISTS (SELECT 1 FROM crop WHERE Name=@Name) INSERT INTO crop(Name,MaturityDays) VALUES(@Name,@MaturityDays); SELECT Id FROM crop WHERE Name=@Name;";
            await using (var crop = new SqlCommand(cropSql, connection, transaction))
            {
                crop.Parameters.Add("@Name", SqlDbType.NVarChar, 60).Value = cropName.Trim(); crop.Parameters.Add("@MaturityDays", SqlDbType.Int).Value = maturityDays;
                cropId = Convert.ToInt32(await crop.ExecuteScalarAsync());
            }
            const string sql = @"UPDATE seed_stock SET Quantity=Quantity+@Quantity, UpdatedAt=SYSUTCDATETIME() WHERE FarmerId=@FarmerId AND CropId=@CropId AND Variety='Standard';
                IF @@ROWCOUNT=0 INSERT INTO seed_stock(FarmerId,CropId,Variety,Quantity,Unit) VALUES(@FarmerId,@CropId,'Standard',@Quantity,@Unit);";
            await using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.Add("@FarmerId", SqlDbType.Int).Value = farmerId; command.Parameters.Add("@CropId", SqlDbType.Int).Value = cropId; command.Parameters.Add("@Quantity", SqlDbType.Decimal).Value = quantity; command.Parameters.Add("@Unit", SqlDbType.NVarChar, 10).Value = unit.Trim();
            await command.ExecuteNonQueryAsync(); await transaction.CommitAsync();
        }
        catch { await transaction.RollbackAsync(); throw; }
    }

    public async Task RecordPlantingAsync(int farmerId, int stockId, decimal quantity, DateTime plantingDate)
    {
        if (quantity <= 0) throw new InvalidOperationException("Planting quantity must be greater than zero.");
        await using var connection = _db.CreateConnection(); await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            const string check = @"SELECT ss.Quantity,c.MaturityDays FROM seed_stock ss INNER JOIN crop c ON c.Id=ss.CropId WHERE ss.Id=@StockId AND ss.FarmerId=@FarmerId;";
            decimal stock; int maturity;
            await using (var command = new SqlCommand(check, connection, transaction))
            {
                command.Parameters.Add("@StockId", SqlDbType.Int).Value = stockId; command.Parameters.Add("@FarmerId", SqlDbType.Int).Value = farmerId;
                await using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync()) throw new InvalidOperationException("The selected seed stock was not found.");
                stock = reader.GetDecimal(0); maturity = reader.GetInt32(1);
            }
            if (quantity > stock) throw new InvalidOperationException($"Not enough seed. Only {stock:0.##} is available.");
            const string update = "UPDATE seed_stock SET Quantity=Quantity-@Quantity,UpdatedAt=SYSUTCDATETIME() WHERE Id=@StockId AND FarmerId=@FarmerId;";
            await using (var command = new SqlCommand(update, connection, transaction)) { command.Parameters.Add("@Quantity", SqlDbType.Decimal).Value = quantity; command.Parameters.Add("@StockId", SqlDbType.Int).Value = stockId; command.Parameters.Add("@FarmerId", SqlDbType.Int).Value = farmerId; await command.ExecuteNonQueryAsync(); }
            var batchCode = "B-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            const string insert = @"INSERT INTO planting_batch(BatchCode,FarmerId,SeedStockId,QuantityPlanted,PlantingDate,PredictedHarvestDate,Status,RecordedByUserId) VALUES(@BatchCode,@FarmerId,@StockId,@Quantity,@PlantingDate,DATEADD(day,@MaturityDays,@PlantingDate),'Active',@FarmerId);";
            await using (var command = new SqlCommand(insert, connection, transaction)) { command.Parameters.Add("@BatchCode", SqlDbType.NVarChar, 20).Value = batchCode; command.Parameters.Add("@FarmerId", SqlDbType.Int).Value = farmerId; command.Parameters.Add("@StockId", SqlDbType.Int).Value = stockId; command.Parameters.Add("@Quantity", SqlDbType.Decimal).Value = quantity; command.Parameters.Add("@PlantingDate", SqlDbType.Date).Value = plantingDate.Date; command.Parameters.Add("@MaturityDays", SqlDbType.Int).Value = maturity; await command.ExecuteNonQueryAsync(); }
            await transaction.CommitAsync();
        }
        catch { await transaction.RollbackAsync(); throw; }
    }

    public async Task<IReadOnlyList<FarmerBatchRow>> GetBatchesAsync(int farmerId)
    {
        const string sql = @"SELECT pb.Id,c.Name,pb.QuantityPlanted,pb.PlantingDate,pb.PredictedHarvestDate,pb.Status,ss.Quantity FROM planting_batch pb INNER JOIN seed_stock ss ON ss.Id=pb.SeedStockId INNER JOIN crop c ON c.Id=ss.CropId WHERE pb.FarmerId=@FarmerId ORDER BY pb.PlantingDate DESC;";
        var rows = new List<FarmerBatchRow>(); await using var connection = _db.CreateConnection(); await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection); command.Parameters.Add("@FarmerId", SqlDbType.Int).Value = farmerId; await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) rows.Add(new FarmerBatchRow(reader.GetInt32(0), reader.GetString(1), reader.GetDecimal(2), reader.GetDateTime(3), reader.GetDateTime(4), reader.GetString(5), reader.GetDecimal(6))); return rows;
    }

    public async Task<IReadOnlyList<FarmerListingRow>> GetListingsAsync(int farmerId)
    {
        const string sql = @"SELECT l.Id,l.BatchId,c.Name,l.AvailableQty,l.Unit,l.PricePerUnit,CASE WHEN l.Status <> 'Hidden' THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END,l.CreatedAt FROM listing l INNER JOIN crop c ON c.Id=l.CropId WHERE l.FarmerId=@FarmerId ORDER BY l.CreatedAt DESC;";
        var rows = new List<FarmerListingRow>(); await using var connection = _db.CreateConnection(); await connection.OpenAsync(); await using var command = new SqlCommand(sql, connection); command.Parameters.Add("@FarmerId", SqlDbType.Int).Value = farmerId; await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) rows.Add(new FarmerListingRow(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetDecimal(3), reader.GetString(4), reader.GetDecimal(5), reader.GetBoolean(6), reader.GetDateTime(7))); return rows;
    }

    public async Task CreateListingAsync(int farmerId, int batchId, decimal quantity, decimal price, string unit)
    {
        if (quantity <= 0) throw new InvalidOperationException("Available quantity must be greater than zero.");
        if (price <= 0) throw new InvalidOperationException("Price per unit must be greater than zero.");
        if (unit is not ("kg" or "crate" or "bag" or "tonne")) throw new InvalidOperationException("The selected unit is not supported.");
        const string sql = @"INSERT INTO listing(ListingCode,BatchId,FarmerId,CropId,Grade,Unit,PricePerUnit,MinOrderQty,EstimatedQty,AvailableQty,Status,OpenForReservation,Description)
            SELECT @Code,pb.Id,pb.FarmerId,ss.CropId,'A',@Unit,@Price,1,@Quantity,@Quantity,'Available',1,NULL FROM planting_batch pb INNER JOIN seed_stock ss ON ss.Id=pb.SeedStockId WHERE pb.Id=@BatchId AND pb.FarmerId=@FarmerId AND pb.Status='Active' AND NOT EXISTS (SELECT 1 FROM listing existing WHERE existing.BatchId=pb.Id);";
        await using var connection = _db.CreateConnection(); await connection.OpenAsync(); await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Code", SqlDbType.NVarChar, 20).Value = "L-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(); command.Parameters.Add("@BatchId", SqlDbType.Int).Value = batchId; command.Parameters.Add("@FarmerId", SqlDbType.Int).Value = farmerId; command.Parameters.Add("@Quantity", SqlDbType.Decimal).Value = quantity; command.Parameters.Add("@Unit", SqlDbType.NVarChar, 20).Value = unit; command.Parameters.Add("@Price", SqlDbType.Decimal).Value = price;
        if (await command.ExecuteNonQueryAsync() == 0) throw new InvalidOperationException("The selected batch is unavailable or already has a listing.");
    }

    public async Task SetListingStatusAsync(int farmerId, int listingId, bool active)
    {
        const string sql = "UPDATE listing SET Status=CASE WHEN @Active=1 THEN 'Available' ELSE 'Hidden' END, OpenForReservation=@Active, UpdatedAt=SYSUTCDATETIME() WHERE Id=@ListingId AND FarmerId=@FarmerId;";
        await using var connection = _db.CreateConnection(); await connection.OpenAsync(); await using var command = new SqlCommand(sql, connection); command.Parameters.Add("@Active", SqlDbType.Bit).Value = active; command.Parameters.Add("@ListingId", SqlDbType.Int).Value = listingId; command.Parameters.Add("@FarmerId", SqlDbType.Int).Value = farmerId; await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<FarmerOrderRow>> GetOrdersAsync(int farmerId)
    {
        const string sql = @"SELECT o.Id,c.Name,oi.QtyRequested,l.Unit,oi.UnitPrice,o.TotalAmount,o.Status,o.CreatedAt FROM orders o INNER JOIN order_item oi ON oi.OrderId=o.Id INNER JOIN listing l ON l.Id=oi.ListingId INNER JOIN crop c ON c.Id=l.CropId WHERE l.FarmerId=@FarmerId ORDER BY o.CreatedAt DESC;";
        var rows = new List<FarmerOrderRow>(); await using var connection = _db.CreateConnection(); await connection.OpenAsync(); await using var command = new SqlCommand(sql, connection); command.Parameters.Add("@FarmerId", SqlDbType.Int).Value = farmerId; await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) rows.Add(new FarmerOrderRow(reader.GetInt32(0), reader.GetString(1), reader.GetDecimal(2), reader.GetString(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetString(6), reader.GetDateTime(7))); return rows;
    }

    public async Task UpdateOrderStatusAsync(int farmerId, int orderId, string status)
    {
        if (status is not ("Confirmed" or "Dispatched" or "Delivered" or "Cancelled")) return;
        await using var connection = _db.CreateConnection(); await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            const string update = @"UPDATE o SET Status=@Status,UpdatedAt=SYSUTCDATETIME()
                FROM orders o WHERE o.Id=@OrderId AND EXISTS
                (SELECT 1 FROM order_item oi INNER JOIN listing l ON l.Id=oi.ListingId
                 WHERE oi.OrderId=o.Id AND l.FarmerId=@FarmerId);";
            await using (var command = new SqlCommand(update, connection, transaction))
            {
                command.Parameters.Add("@Status", SqlDbType.NVarChar, 12).Value = status;
                command.Parameters.Add("@OrderId", SqlDbType.Int).Value = orderId;
                command.Parameters.Add("@FarmerId", SqlDbType.Int).Value = farmerId;
                if (await command.ExecuteNonQueryAsync() == 0) return;
            }
            if (status == "Delivered")
            {
                const string invoice = @"UPDATE order_item SET Status='Delivered', DeliveredAt=SYSUTCDATETIME(),
                    QtyAccepted=COALESCE(QtyDispatched,QtyConfirmed,QtyRequested)
                    WHERE OrderId=@OrderId AND Status <> 'Cancelled';
                    INSERT INTO invoice(InvoiceNumber,OrderId,Amount,Status,DueDate)
                    SELECT 'INV-'+RIGHT('00000000'+CONVERT(varchar(8),o.Id),8),o.Id,
                        SUM(COALESCE(oi.QtyAccepted,oi.QtyRequested)*oi.UnitPrice),'Unpaid',DATEADD(day,30,CAST(GETDATE() AS date))
                    FROM orders o INNER JOIN order_item oi ON oi.OrderId=o.Id
                    WHERE o.Id=@OrderId AND NOT EXISTS (SELECT 1 FROM invoice WHERE OrderId=o.Id)
                    GROUP BY o.Id;
                    UPDATE orders SET Status='Invoiced',UpdatedAt=SYSUTCDATETIME() WHERE Id=@OrderId AND EXISTS (SELECT 1 FROM invoice WHERE OrderId=@OrderId);";
                await using var command = new SqlCommand(invoice, connection, transaction);
                command.Parameters.Add("@OrderId", SqlDbType.Int).Value = orderId;
                await command.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
        }
        catch { await transaction.RollbackAsync(); throw; }
    }

    public async Task<IReadOnlyList<MarketplaceRow>> GetMarketplaceAsync()
    {
        const string sql = @"SELECT l.Id,c.Name,l.AvailableQty,l.Unit,l.PricePerUnit,u.FullName,fp.FarmName,l.CreatedAt FROM listing l INNER JOIN crop c ON c.Id=l.CropId INNER JOIN farmer_profile fp ON fp.UserId=l.FarmerId INNER JOIN app_user u ON u.Id=l.FarmerId WHERE l.Status='Available' AND l.OpenForReservation=1 AND l.AvailableQty>0 AND fp.VerificationStatus='Verified' ORDER BY l.CreatedAt DESC;";
        var rows = new List<MarketplaceRow>(); await using var connection = _db.CreateConnection(); await connection.OpenAsync(); await using var command = new SqlCommand(sql, connection); await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) rows.Add(new MarketplaceRow(reader.GetInt32(0), reader.GetString(1), reader.GetDecimal(2), reader.GetString(3), reader.GetDecimal(4), reader.GetString(5), reader.GetString(6), reader.GetDateTime(7))); return rows;
    }
}
