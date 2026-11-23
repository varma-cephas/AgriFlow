using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace AgriFlow.Pages.SeedStock;

public class IndexModel : PageModel
{
    private readonly IConfiguration _configuration;

    public IndexModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public IReadOnlyList<SeedStockRow> StockRows { get; private set; } = [];
    public string ActiveFarmerName { get; private set; } = string.Empty;
    public string StatusMessage { get; private set; } = string.Empty;
    public bool IsAgent { get; private set; }
    public int? ActiveFarmerId { get; private set; }
    public IReadOnlyList<FarmerOption> AssignedFarmers { get; private set; } = [];

    public IActionResult OnGet(string? status = null, int? farmerId = null)
    {
        StatusMessage = status ?? string.Empty;
        var role = SeedStockContext.GetRole(User);
        if (string.IsNullOrWhiteSpace(role))
        {
            StatusMessage = "Please sign in to view seed stock.";
            return Page();
        }

        IsAgent = role.Equals("Agent", StringComparison.OrdinalIgnoreCase);
        if (farmerId.HasValue && (IsAgent || role.Equals("Admin", StringComparison.OrdinalIgnoreCase)))
        {
            HttpContext.Session.SetString(SeedStockContext.ActiveFarmerKey, farmerId.Value.ToString());
        }
        ActiveFarmerId = SeedStockContext.GetActiveFarmerId(HttpContext, User);

        try
        {
            using var connection = new SqlConnection(_configuration.GetConnectionString("AgriFlowDb"));
            connection.Open();

            if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase) && !ActiveFarmerId.HasValue)
            {
                LoadRows(connection, null);
                ActiveFarmerName = "All scoped farmers";
                return Page();
            }

            if (!ActiveFarmerId.HasValue)
            {
                StatusMessage = IsAgent
                    ? "Select an assigned farmer to view seed stock."
                    : "No active farmer profile was found.";
                return Page();
            }

            if (role.Equals("Farmer", StringComparison.OrdinalIgnoreCase)
                && ActiveFarmerId != SeedStockContext.GetFarmerId(User))
            {
                StatusMessage = "You can only view seed stock for your own profile.";
                ActiveFarmerId = null;
                return Page();
            }

            if (IsAgent)
            {
                LoadAssignedFarmers(connection);
                using var assignment = connection.CreateCommand();
                assignment.CommandText = "SELECT COUNT(1) FROM agent_farmer_assignment WHERE AgentId = @AgentId AND FarmerId = @FarmerId";
                assignment.Parameters.AddWithValue("@AgentId", SeedStockContext.GetActorUserId(User) ?? 0);
                assignment.Parameters.AddWithValue("@FarmerId", ActiveFarmerId.Value);
                if ((int)assignment.ExecuteScalar() == 0)
                {
                    StatusMessage = "You are not assigned to this farmer.";
                    ActiveFarmerId = null;
                    return Page();
                }
            }

            using var farmerCommand = connection.CreateCommand();
            farmerCommand.CommandText = @"
SELECT COALESCE(fp.FarmName, u.FullName, u.Email, 'Active farmer')
FROM app_user u
LEFT JOIN farmer_profile fp ON fp.UserId = u.Id
WHERE u.Id = @FarmerId";
            farmerCommand.Parameters.AddWithValue("@FarmerId", ActiveFarmerId.Value);
            ActiveFarmerName = farmerCommand.ExecuteScalar()?.ToString() ?? "Active farmer";

            LoadRows(connection, ActiveFarmerId);
        }
        catch (SqlException)
        {
            StatusMessage = "We could not load the seed inventory. Please try again.";
        }

        return Page();
    }

    private void LoadRows(SqlConnection connection, int? farmerId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT ss.Id, ss.FarmerId, ss.CropId, c.Name AS CropName, ss.Variety,
       ss.Supplier, ss.Quantity, ss.Unit, c.MaturityDays, ss.UpdatedAt,
       u.Email AS FarmerName
FROM seed_stock ss
INNER JOIN crop c ON c.Id = ss.CropId
INNER JOIN app_user u ON u.Id = ss.FarmerId";
        if (farmerId.HasValue)
        {
            command.CommandText += " WHERE ss.FarmerId = @FarmerId";
            command.Parameters.AddWithValue("@FarmerId", farmerId.Value);
        }
        command.CommandText += " ORDER BY c.Name, ss.Variety";

        using var reader = command.ExecuteReader();
        var rows = new List<SeedStockRow>();
        while (reader.Read())
        {
            rows.Add(new SeedStockRow
            {
                Id = reader.GetInt32("Id"),
                FarmerId = reader.GetInt32("FarmerId"),
                CropName = reader.GetString("CropName"),
                Variety = reader.GetString("Variety"),
                Supplier = reader.IsDBNull("Supplier") ? null : reader.GetString("Supplier"),
                Quantity = reader.GetDecimal("Quantity"),
                Unit = reader.GetString("Unit"),
                MaturityDays = reader.GetInt32("MaturityDays"),
                UpdatedAt = reader.GetDateTime("UpdatedAt"),
                FarmerName = reader.GetString("FarmerName")
            });
        }
        StockRows = rows;
    }

    private void LoadAssignedFarmers(SqlConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT u.Id, COALESCE(fp.FarmName, u.FullName, u.Email, 'Farmer')
FROM agent_farmer_assignment afa
INNER JOIN app_user u ON u.Id = afa.FarmerId
LEFT JOIN farmer_profile fp ON fp.UserId = u.Id
WHERE afa.AgentId = @AgentId
ORDER BY COALESCE(fp.FarmName, u.FullName, u.Email)";
        command.Parameters.AddWithValue("@AgentId", SeedStockContext.GetActorUserId(User) ?? 0);
        using var reader = command.ExecuteReader();
        var farmers = new List<FarmerOption>();
        while (reader.Read()) farmers.Add(new FarmerOption { Id = reader.GetInt32(0), Name = reader.GetString(1) });
        AssignedFarmers = farmers;
    }

    public sealed class FarmerOption
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }

    public sealed class SeedStockRow
    {
        public int Id { get; init; }
        public int FarmerId { get; init; }
        public string CropName { get; init; } = string.Empty;
        public string Variety { get; init; } = string.Empty;
        public string? Supplier { get; init; }
        public decimal Quantity { get; init; }
        public string Unit { get; init; } = string.Empty;
        public int MaturityDays { get; init; }
        public DateTime UpdatedAt { get; init; }
        public string FarmerName { get; init; } = string.Empty;
    }
}
