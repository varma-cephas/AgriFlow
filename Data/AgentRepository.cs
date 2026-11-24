using Microsoft.Data.SqlClient;

namespace AgriFlow.Data;

public sealed record AgentOverview(
    int AssignedFarmers,
    int ActiveBatches,
    int HarvestsDue,
    int OrdersToHandle);

public sealed record AgentFarmerRow(
    int UserId,
    string FullName,
    string FarmName,
    string District,
    string Phone,
    int ActiveBatches,
    decimal SeedStock,
    int OpenOrders,
    string VerificationStatus);

public sealed class AgentRepository
{
    private readonly DbConnectionFactory _db;

    public AgentRepository(DbConnectionFactory db) => _db = db;

    public async Task<AgentOverview> GetOverviewAsync(int agentId)
    {
        const string sql = @"
            SELECT
                COUNT(DISTINCT a.FarmerId),
                (SELECT COUNT(*) FROM planting_batch pb
                 INNER JOIN agent_farmer_assignment aa ON aa.FarmerId = pb.FarmerId
                 WHERE aa.AgentId = @AgentId AND pb.Status = 'Active'),
                (SELECT COUNT(*) FROM planting_batch pb
                 INNER JOIN agent_farmer_assignment aa ON aa.FarmerId = pb.FarmerId
                 WHERE aa.AgentId = @AgentId
                   AND pb.PredictedHarvestDate BETWEEN CAST(GETDATE() AS date)
                   AND DATEADD(day, 14, CAST(GETDATE() AS date))),
                 (SELECT COUNT(DISTINCT o.Id) FROM orders o
                  INNER JOIN order_item oi ON oi.OrderId = o.Id
                  INNER JOIN listing l ON l.Id = oi.ListingId
                  INNER JOIN agent_farmer_assignment aa ON aa.FarmerId = l.FarmerId
                  WHERE aa.AgentId = @AgentId
                    AND o.Status IN ('Placed','Confirmed','Dispatched'))
            FROM agent_farmer_assignment a
            WHERE a.AgentId = @AgentId;";

        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@AgentId", agentId);
        await using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();

        return new AgentOverview(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetInt32(3));
    }

    public async Task<IReadOnlyList<AgentFarmerRow>> GetFarmersAsync(int agentId)
    {
        const string sql = @"
            SELECT
                u.Id,
                u.FullName,
                fp.FarmName,
                fp.District,
                ISNULL(u.Phone, ''),
                (SELECT COUNT(*) FROM planting_batch pb
                 WHERE pb.FarmerId = fp.UserId AND pb.Status = 'Active'),
                 ISNULL((SELECT SUM(ss.Quantity) FROM seed_stock ss
                        WHERE ss.FarmerId = fp.UserId), 0),
                0,
                fp.VerificationStatus
            FROM agent_farmer_assignment a
            INNER JOIN farmer_profile fp ON fp.UserId = a.FarmerId
            INNER JOIN app_user u ON u.Id = fp.UserId
            WHERE a.AgentId = @AgentId
            ORDER BY u.FullName;";

        var farmers = new List<AgentFarmerRow>();
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@AgentId", agentId);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            farmers.Add(new AgentFarmerRow(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetInt32(5),
                reader.GetDecimal(6),
                reader.GetInt32(7),
                reader.GetString(8)));
        }

        return farmers;
    }
}
