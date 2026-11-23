using System.Data;
using System.Security.Claims;
using AgriFlow.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace AgriFlow.Pages.PlantingBatches;

[Authorize(Roles = "Farmer,Agent")]
public sealed class IndexModel : PageModel
{
    private readonly DbConnectionFactory _db;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(DbConnectionFactory db, ILogger<IndexModel> logger)
    {
        _db = db;
        _logger = logger;
    }

    public IReadOnlyList<ActiveBatch> Batches { get; private set; } = [];
    public DateOnly Today { get; private set; } = DateOnly.FromDateTime(DateTime.Today);
    public bool LoadFailed { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Forbid();
        }

        var isFarmer = User.IsInRole("Farmer");
        var farmScope = isFarmer
            ? "pb.FarmerId = @UserId"
            : "(fp.RegisteredByAgentId = @UserId OR EXISTS (SELECT 1 FROM agent_farmer_assignment afa WHERE afa.FarmerId = fp.UserId AND afa.AgentId = @UserId))";

        const string selectSql = @"
            SELECT pb.Id, c.Name, ss.Variety, pb.BatchCode, pb.FieldName,
                   pb.QuantityPlanted, ss.Unit, pb.PlantingDate,
                   pb.PredictedHarvestDate, pb.Status
            FROM planting_batch pb
            INNER JOIN farmer_profile fp ON fp.UserId = pb.FarmerId
            INNER JOIN seed_stock ss ON ss.Id = pb.SeedStockId
            INNER JOIN crop c ON c.Id = ss.CropId
            WHERE pb.Status = N'Active' AND ";
        var sql = selectSql + farmScope + @"
            ORDER BY pb.PredictedHarvestDate ASC, pb.PlantingDate ASC, pb.Id ASC;";

        Today = DateOnly.FromDateTime(DateTime.Today);
        var batches = new List<ActiveBatch>();

        try
        {
            await using var connection = _db.CreateConnection();
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                batches.Add(new ActiveBatch(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.GetDecimal(5),
                    reader.GetString(6),
                    DateOnly.FromDateTime(reader.GetDateTime(7)),
                    DateOnly.FromDateTime(reader.GetDateTime(8)),
                    reader.GetString(9)));
            }
        }
        catch (SqlException exception)
        {
            _logger.LogError(exception, "Unable to load active planting batches for user {UserId}.", userId);
            LoadFailed = true;
            TempData["ErrorMessage"] = "We couldn't load planting batches right now. Please try again later.";
        }

        Batches = batches;
        return Page();
    }

    public sealed record ActiveBatch(
        int Id,
        string CropName,
        string Variety,
        string BatchCode,
        string? FieldName,
        decimal QuantityPlanted,
        string Unit,
        DateOnly PlantingDate,
        DateOnly PredictedHarvestDate,
        string Status);
}
