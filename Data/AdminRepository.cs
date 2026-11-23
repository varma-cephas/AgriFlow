using Microsoft.Data.SqlClient;

namespace AgriFlow.Data;

public sealed record AdminStats(int Farmers, int PendingFarmers, int Businesses, int PendingBusinesses, int Agents, int AssignedFarmers, int Orders);
public sealed record AdminAgentRow(int Id, string Name, string Email, string District, int FarmerCount);
public sealed record AdminFarmerRow(int Id, string Name, string FarmName, string District, string? AgentName);
public sealed record PendingFarmerRow(int Id, string Name, string Email, string Phone, string FarmName, string District, string LandUpi, string Status, DateTime CreatedAt);
public sealed record PendingBusinessRow(int Id, string Name, string ContactName, string Email, string RdbCode, string City, string Status, DateTime CreatedAt);

public sealed class AdminRepository
{
    private readonly DbConnectionFactory _db;
    public AdminRepository(DbConnectionFactory db) => _db = db;

    public async Task<AdminStats> GetStatsAsync()
    {
        const string sql = @"
            SELECT
              (SELECT COUNT(*) FROM app_user WHERE Role = 'Farmer'),
              (SELECT COUNT(*) FROM farmer_profile WHERE VerificationStatus = 'Unverified'),
              (SELECT COUNT(*) FROM buyer_business),
              (SELECT COUNT(*) FROM buyer_business WHERE VerificationStatus = 'Pending'),
              (SELECT COUNT(*) FROM app_user WHERE Role = 'Agent'),
              (SELECT COUNT(*) FROM agent_farmer_assignment),
              (SELECT COUNT(*) FROM orders);";
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        await using var r = await cmd.ExecuteReaderAsync();
        await r.ReadAsync();
        return new AdminStats(r.GetInt32(0), r.GetInt32(1), r.GetInt32(2), r.GetInt32(3), r.GetInt32(4), r.GetInt32(5), r.GetInt32(6));
    }

    public async Task<IReadOnlyList<AdminAgentRow>> GetAgentsAsync()
    {
        const string sql = @"SELECT u.Id, u.FullName, ISNULL(u.Email,''), ISNULL(fp.District,''), COUNT(a.FarmerId)
            FROM app_user u LEFT JOIN farmer_profile fp ON fp.RegisteredByAgentId = u.Id
            LEFT JOIN agent_farmer_assignment a ON a.AgentId = u.Id
            WHERE u.Role = 'Agent' GROUP BY u.Id, u.FullName, u.Email, fp.District ORDER BY u.FullName;";
        var result = new List<AdminAgentRow>();
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) result.Add(new AdminAgentRow(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetInt32(4)));
        return result;
    }

    public async Task<IReadOnlyList<AdminFarmerRow>> GetFarmersAsync()
    {
        const string sql = @"SELECT u.Id, u.FullName, fp.FarmName, fp.District, au.FullName
            FROM farmer_profile fp INNER JOIN app_user u ON u.Id = fp.UserId
            LEFT JOIN agent_farmer_assignment aa ON aa.FarmerId = fp.UserId
            LEFT JOIN app_user au ON au.Id = aa.AgentId ORDER BY u.FullName;";
        var result = new List<AdminFarmerRow>();
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) result.Add(new AdminFarmerRow(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4)));
        return result;
    }

    public async Task AssignFarmersAsync(int agentId, int[] farmerIds)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();
        try
        {
            foreach (var farmerId in farmerIds)
            {
                await using (var remove = new SqlCommand("DELETE FROM agent_farmer_assignment WHERE FarmerId = @FarmerId;", conn, tx))
                {
                    remove.Parameters.AddWithValue("@FarmerId", farmerId);
                    await remove.ExecuteNonQueryAsync();
                }
                await using var add = new SqlCommand("INSERT INTO agent_farmer_assignment (AgentId, FarmerId) VALUES (@AgentId, @FarmerId);", conn, tx);
                add.Parameters.AddWithValue("@AgentId", agentId);
                add.Parameters.AddWithValue("@FarmerId", farmerId);
                await add.ExecuteNonQueryAsync();
            }
            await tx.CommitAsync();
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    public async Task<IReadOnlyList<PendingFarmerRow>> GetPendingFarmersAsync()
    {
        const string sql = @"SELECT u.Id, u.FullName, ISNULL(u.Email,''), ISNULL(u.Phone,''), fp.FarmName, fp.District, fp.LandUpi, fp.VerificationStatus, fp.CreatedAt
            FROM farmer_profile fp INNER JOIN app_user u ON u.Id = fp.UserId
            WHERE fp.VerificationStatus = 'Unverified' ORDER BY fp.CreatedAt;";
        var result = new List<PendingFarmerRow>();
        await using var conn = _db.CreateConnection(); await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn); await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) result.Add(new PendingFarmerRow(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetString(6), r.GetString(7), r.GetDateTime(8)));
        return result;
    }

    public async Task<IReadOnlyList<PendingBusinessRow>> GetPendingBusinessesAsync()
    {
        const string sql = @"SELECT bb.Id, bb.Name, u.FullName, ISNULL(u.Email,''), bb.RdbCompanyCode, bb.City, bb.VerificationStatus, bb.CreatedAt
            FROM buyer_business bb INNER JOIN app_user u ON u.BuyerBusinessId = bb.Id
            WHERE bb.VerificationStatus = 'Pending' ORDER BY bb.CreatedAt;";
        var result = new List<PendingBusinessRow>();
        await using var conn = _db.CreateConnection(); await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn); await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) result.Add(new PendingBusinessRow(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetString(6), r.GetDateTime(7)));
        return result;
    }

    public async Task SetFarmerVerificationAsync(int farmerId, int adminId, string status, string? reason)
    {
        const string sql = @"UPDATE farmer_profile SET VerificationStatus = @Status, VerifiedByUserId = @AdminId,
            VerifiedAt = CASE WHEN @Status = 'Verified' THEN SYSUTCDATETIME() ELSE NULL END,
            RejectionReason = @Reason WHERE UserId = @FarmerId;";
        await using var conn = _db.CreateConnection(); await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Status", status); cmd.Parameters.AddWithValue("@AdminId", adminId);
        cmd.Parameters.AddWithValue("@Reason", string.IsNullOrWhiteSpace(reason) ? DBNull.Value : reason.Trim()); cmd.Parameters.AddWithValue("@FarmerId", farmerId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SetBusinessVerificationAsync(int businessId, int adminId, string status, string? reason)
    {
        const string sql = @"UPDATE buyer_business SET VerificationStatus = @Status, VerifiedByUserId = @AdminId,
            VerifiedAt = CASE WHEN @Status = 'Verified' THEN SYSUTCDATETIME() ELSE NULL END,
            RejectionReason = @Reason WHERE Id = @BusinessId;";
        await using var conn = _db.CreateConnection(); await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Status", status); cmd.Parameters.AddWithValue("@AdminId", adminId);
        cmd.Parameters.AddWithValue("@Reason", string.IsNullOrWhiteSpace(reason) ? DBNull.Value : reason.Trim()); cmd.Parameters.AddWithValue("@BusinessId", businessId);
        await cmd.ExecuteNonQueryAsync();
    }
}
