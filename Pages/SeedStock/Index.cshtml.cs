using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace AgriFlow.Pages.SeedStock;

public class IndexModel : PageModel
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IConfiguration configuration, ILogger<IndexModel> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public IReadOnlyList<SeedStockRow> StockRows { get; private set; } = [];
    public string ActiveFarmerName { get; private set; } = string.Empty;
    public string StatusMessage { get; private set; } = string.Empty;
    public bool IsAgent { get; private set; }
    public bool CanDelete { get; private set; }
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
        CanDelete = IsAgent
            || role.Equals("Farmer", StringComparison.OrdinalIgnoreCase)
            || role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
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

    public IActionResult OnPostDelete(int id)
    {
        var role = SeedStockContext.GetRole(User);
        var actorId = SeedStockContext.GetActorUserId(User);
        var activeFarmerId = SeedStockContext.GetActiveFarmerId(HttpContext, User);
        var isFarmer = role.Equals("Farmer", StringComparison.OrdinalIgnoreCase);
        var isAgent = role.Equals("Agent", StringComparison.OrdinalIgnoreCase);
        var isAdmin = role.Equals("Admin", StringComparison.OrdinalIgnoreCase);

        if (id <= 0 || !actorId.HasValue || (!isFarmer && !isAgent && !isAdmin))
            return RedirectToPage("./Index", new { status = "You are not authorized to delete this seed stock." });

        if (isFarmer)
            activeFarmerId = SeedStockContext.GetFarmerId(User);
        if ((isFarmer || isAgent) && !activeFarmerId.HasValue)
            return RedirectToPage("./Index", new { status = "You are not authorized to delete this seed stock." });

        SqlConnection? connection = null;
        SqlTransaction? transaction = null;
        var farmerId = 0;
        var deleteInProgress = false;
        try
        {
            connection = new SqlConnection(_configuration.GetConnectionString("AgriFlowDb"));
            connection.Open();
            transaction = connection.BeginTransaction(IsolationLevel.Serializable);

            using (var stock = connection.CreateCommand())
            {
                stock.Transaction = transaction;
                stock.CommandText = @"
SELECT ss.FarmerId, c.Name, ss.Variety
FROM seed_stock ss WITH (UPDLOCK, HOLDLOCK)
INNER JOIN crop c ON c.Id = ss.CropId
WHERE ss.Id = @Id";
                stock.Parameters.AddWithValue("@Id", id);
                using var reader = stock.ExecuteReader();
                if (!reader.Read())
                {
                    reader.Close();
                    transaction.Rollback();
                    return RedirectToPage("./Index", new { status = "This seed stock was not found or is outside your authorized scope." });
                }

                farmerId = reader.GetInt32(0);
                var cropName = reader.GetString(1);
                var variety = reader.GetString(2);
                reader.Close();

                if ((isFarmer && activeFarmerId != farmerId)
                    || (isAgent && (activeFarmerId != farmerId || !IsAssignedAgent(connection, transaction, actorId.Value, farmerId)))
                    || (isAdmin && activeFarmerId.HasValue && activeFarmerId != farmerId))
                {
                    transaction.Rollback();
                    return RedirectToPage("./Index", new { status = "This seed stock was not found or is outside your authorized scope." });
                }

                using var delete = connection.CreateCommand();
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM seed_stock WHERE Id = @Id AND FarmerId = @FarmerId";
                delete.Parameters.AddWithValue("@Id", id);
                delete.Parameters.AddWithValue("@FarmerId", farmerId);
                deleteInProgress = true;
                var deletedRows = delete.ExecuteNonQuery();
                deleteInProgress = false;
                if (deletedRows != 1)
                {
                    transaction.Rollback();
                    return RedirectToPage("./Index", new { status = "This seed stock was not found or is outside your authorized scope." });
                }

                WriteDeleteAudit(connection, transaction, actorId.Value, farmerId, id, role, cropName, variety);
            }

            transaction.Commit();
            return RedirectToPage("./Index", new { status = "Seed stock deleted successfully." });
        }
        catch (SqlException exception) when (exception.Number == 547 && deleteInProgress)
        {
            RollbackSafely(transaction, id);
            try
            {
                using var countCommand = connection!.CreateCommand();
                countCommand.CommandText = "SELECT COUNT(*) FROM planting_batch WHERE SeedStockId = @Id";
                countCommand.Parameters.AddWithValue("@Id", id);
                var batchCount = Convert.ToInt32(countCommand.ExecuteScalar());
                if (batchCount > 0)
                    return RedirectToPage("./Index", new { status = $"This seed stock is used by {batchCount} planting batch(es) and can't be deleted." });

                _logger.LogError(exception, "Seed stock deletion failed due to a foreign key constraint for stock {SeedStockId}.", id);
            }
            catch (Exception countException)
            {
                _logger.LogError(countException, "Could not count planting batches after seed stock deletion was blocked for stock {SeedStockId}.", id);
            }

            return RedirectToPage("./Index", new { status = "The seed stock could not be deleted. Please try again." });
        }
        catch (Exception exception)
        {
            RollbackSafely(transaction, id);
            _logger.LogError(exception, "An unexpected error occurred while deleting seed stock {SeedStockId}.", id);
            return RedirectToPage("./Index", new { status = "The seed stock could not be deleted. Please try again." });
        }
        finally
        {
            transaction?.Dispose();
            connection?.Dispose();
        }
    }

    private static bool IsAssignedAgent(SqlConnection connection, SqlTransaction transaction, int agentId, int farmerId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(1) FROM agent_farmer_assignment WITH (HOLDLOCK) WHERE AgentId = @AgentId AND FarmerId = @FarmerId";
        command.Parameters.AddWithValue("@AgentId", agentId);
        command.Parameters.AddWithValue("@FarmerId", farmerId);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private void WriteDeleteAudit(SqlConnection connection, SqlTransaction transaction, int actorId, int farmerId, int entityId, string role, string cropName, string variety)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
INSERT INTO audit_log (At, ActorUserId, OnBehalfOfFarmerId, Entity, EntityId, Action, Summary, Result)
VALUES (SYSUTCDATETIME(), @ActorUserId, @FarmerId, 'SeedStock', @EntityId, 'Delete', @Summary, 'Success')";
        command.Parameters.AddWithValue("@ActorUserId", actorId);
        command.Parameters.AddWithValue("@FarmerId", farmerId);
        command.Parameters.AddWithValue("@EntityId", entityId.ToString());
        command.Parameters.AddWithValue("@Summary", $"Deleted {cropName} seed stock ({variety}). Actor role: {role}.");
        command.ExecuteNonQuery();
    }

    private void RollbackSafely(SqlTransaction? transaction, int seedStockId)
    {
        if (transaction is null)
            return;

        try
        {
            transaction.Rollback();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not roll back the seed stock deletion transaction for stock {SeedStockId}.", seedStockId);
        }
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
