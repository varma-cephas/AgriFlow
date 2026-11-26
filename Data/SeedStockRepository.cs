using System.Data;
using Microsoft.Data.SqlClient;

namespace AgriFlow.Data;

public sealed record SeedStockItem(
    string FarmName,
    string CropName,
    string Variety,
    decimal Quantity,
    string Unit,
    DateTime UpdatedAt);

public sealed class SeedStockRepository
{
    public const decimal LowStockThreshold = 10m;

    private readonly DbConnectionFactory _db;

    public SeedStockRepository(DbConnectionFactory db)
    {
        _db = db;
    }

    public async Task<int> GetLowStockCountAsync(string role, int? actorId, CancellationToken cancellationToken = default)
    {
        var normalizedRole = NormalizeRole(role);
        var scopeFilter = GetScopeFilter(normalizedRole);
        const string sql = @"
SELECT COUNT(1)
FROM seed_stock ss
WHERE ss.Quantity <= @Threshold";

        await using var connection = _db.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql + scopeFilter, connection);
        AddScopeParameters(command, normalizedRole, actorId);

        var threshold = command.Parameters.Add("@Threshold", SqlDbType.Decimal);
        threshold.Precision = 10;
        threshold.Scale = 2;
        threshold.Value = LowStockThreshold;

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<IReadOnlyList<SeedStockItem>> GetInventoryAsync(
        string role,
        int? actorId,
        CancellationToken cancellationToken = default)
    {
        var normalizedRole = NormalizeRole(role);
        var scopeFilter = GetScopeFilter(normalizedRole);
        var sql = @"
SELECT fp.FarmName, c.Name AS CropName, ss.Variety, ss.Quantity, ss.Unit, ss.UpdatedAt
FROM seed_stock ss
INNER JOIN farmer_profile fp ON fp.UserId = ss.FarmerId
INNER JOIN crop c ON c.Id = ss.CropId
WHERE 1 = 1" + scopeFilter + "\nORDER BY fp.FarmName, c.Name, ss.Variety;";

        await using var connection = _db.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        AddScopeParameters(command, normalizedRole, actorId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<SeedStockItem>();
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new SeedStockItem(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetDecimal(3),
                reader.GetString(4),
                reader.GetDateTime(5)));
        }

        return items;
    }

    public static bool CanViewInventory(string role) =>
        role.Equals("Farmer", StringComparison.OrdinalIgnoreCase)
        || role.Equals("Agent", StringComparison.OrdinalIgnoreCase)
        || role.Equals("Admin", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeRole(string role)
    {
        if (role.Equals("Farmer", StringComparison.OrdinalIgnoreCase)) return "Farmer";
        if (role.Equals("Agent", StringComparison.OrdinalIgnoreCase)) return "Agent";
        if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase)) return "Admin";
        throw new ArgumentException("The role is not authorized to view seed stock.", nameof(role));
    }

    private static string GetScopeFilter(string role) => role switch
    {
        "Farmer" => " AND ss.FarmerId = @ActorId",
        "Agent" => @"
  AND EXISTS (
      SELECT 1
      FROM agent_farmer_assignment afa
      WHERE afa.FarmerId = ss.FarmerId AND afa.AgentId = @ActorId
  )",
        "Admin" => string.Empty,
        _ => throw new ArgumentException("The role is not authorized to view seed stock.", nameof(role))
    };

    private static void AddScopeParameters(SqlCommand command, string role, int? actorId)
    {
        if (role == "Admin") return;
        if (!actorId.HasValue || actorId.Value <= 0)
            throw new ArgumentException("A valid user ID is required for this inventory scope.", nameof(actorId));

        command.Parameters.Add("@ActorId", SqlDbType.Int).Value = actorId.Value;
    }
}
