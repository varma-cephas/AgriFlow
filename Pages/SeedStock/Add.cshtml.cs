using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Claims;
using AgriFlow.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace AgriFlow.Pages.SeedStock;

[Authorize(Roles = "Farmer,Agent")]
public class AddModel : PageModel
{
    private readonly DbConnectionFactory _db;
    private readonly ILogger<AddModel> _logger;

    public AddModel(DbConnectionFactory db, ILogger<AddModel> logger)
    {
        _db = db;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public int? FarmId { get; set; }

    [BindProperty]
    public SeedInput Input { get; set; } = new();

    public List<FarmOption> Farms { get; private set; } = [];
    public List<CropOption> Crops { get; private set; } = [];
    public string? PageError { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!TryGetActor(out var actorId, out var role)) return Forbid();
        Farms = await GetFarmsAsync(actorId, role);
        if (Farms.Count == 0) return Page();
        FarmId ??= Farms[0].Id;
        if (!Farms.Any(farm => farm.Id == FarmId.Value)) return NotFound();
        await LoadCropsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!TryGetActor(out var actorId, out var role)) return Forbid();
        if (Input.Variety?.Trim().Length > 80)
            ModelState.AddModelError("Input.Variety", "Variety must be 80 characters or fewer.");

        Farms = await GetFarmsAsync(actorId, role);
        if (!FarmId.HasValue || !Farms.Any(farm => farm.Id == FarmId.Value)) return NotFound();
        if (!ModelState.IsValid)
        {
            await LoadCropsAsync();
            return Page();
        }

        await using var connection = _db.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        try
        {
            const string farmSql = @"
                SELECT fp.UserId
                FROM farmer_profile fp WITH (UPDLOCK, HOLDLOCK)
                WHERE fp.UserId = @FarmId
                  AND ((@Role = 'Farmer' AND fp.UserId = @ActorId)
                    OR (@Role = 'Agent' AND EXISTS (
                        SELECT 1 FROM agent_farmer_assignment afa WITH (HOLDLOCK)
                        WHERE afa.FarmerId = fp.UserId AND afa.AgentId = @ActorId
                    )));";
            await using (var farmCommand = new SqlCommand(farmSql, connection, transaction))
            {
                farmCommand.Parameters.Add("@FarmId", SqlDbType.Int).Value = FarmId.Value;
                farmCommand.Parameters.Add("@Role", SqlDbType.NVarChar, 10).Value = role;
                farmCommand.Parameters.Add("@ActorId", SqlDbType.Int).Value = actorId;
                if (await farmCommand.ExecuteScalarAsync() is null)
                {
                    await transaction.RollbackAsync();
                    return NotFound();
                }
            }

            const string cropSql = "SELECT COUNT(1) FROM crop WHERE Id = @CropId;";
            await using (var cropCommand = new SqlCommand(cropSql, connection, transaction))
            {
                cropCommand.Parameters.Add("@CropId", SqlDbType.Int).Value = Input.CropId;
                if ((int)(await cropCommand.ExecuteScalarAsync() ?? 0) == 0)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError("Input.CropId", "Choose a valid crop.");
                    await LoadCropsAsync();
                    return Page();
                }
            }

            const string updateSql = @"
                UPDATE seed_stock WITH (UPDLOCK, HOLDLOCK)
                SET Quantity = Quantity + @Quantity,
                    Supplier = COALESCE(@Supplier, Supplier),
                    UpdatedAt = SYSUTCDATETIME()
                WHERE FarmerId = @FarmId AND CropId = @CropId AND Variety = @Variety AND Unit = 'kg';";
            int affected;
            await using (var updateCommand = new SqlCommand(updateSql, connection, transaction))
            {
                AddDecimal(updateCommand, "@Quantity", 10, 2, Input.Quantity);
                updateCommand.Parameters.Add("@Supplier", SqlDbType.NVarChar, 120).Value =
                    string.IsNullOrWhiteSpace(Input.Supplier) ? DBNull.Value : Input.Supplier.Trim();
                updateCommand.Parameters.Add("@FarmId", SqlDbType.Int).Value = FarmId.Value;
                updateCommand.Parameters.Add("@CropId", SqlDbType.Int).Value = Input.CropId;
                updateCommand.Parameters.Add("@Variety", SqlDbType.NVarChar, 80).Value = Input.Variety!.Trim();
                affected = await updateCommand.ExecuteNonQueryAsync();
            }

            if (affected == 0)
            {
                const string insertSql = @"
                    INSERT INTO seed_stock (FarmerId, CropId, Variety, Supplier, Quantity, Unit)
                    VALUES (@FarmId, @CropId, @Variety, @Supplier, @Quantity, 'kg');";
                await using var insertCommand = new SqlCommand(insertSql, connection, transaction);
                insertCommand.Parameters.Add("@FarmId", SqlDbType.Int).Value = FarmId.Value;
                insertCommand.Parameters.Add("@CropId", SqlDbType.Int).Value = Input.CropId;
                insertCommand.Parameters.Add("@Variety", SqlDbType.NVarChar, 80).Value = Input.Variety!.Trim();
                insertCommand.Parameters.Add("@Supplier", SqlDbType.NVarChar, 120).Value =
                    string.IsNullOrWhiteSpace(Input.Supplier) ? DBNull.Value : Input.Supplier.Trim();
                AddDecimal(insertCommand, "@Quantity", 10, 2, Input.Quantity);
                await insertCommand.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            TempData["SuccessMessage"] = "Seed stock added successfully.";
            return RedirectToPage("/PlantingBatches/Create", new { farmId = FarmId.Value });
        }
        catch (SqlException exception)
        {
            await transaction.RollbackAsync();
            _logger.LogError(exception, "Unable to add seed stock for farm {FarmId}.", FarmId);
            PageError = "We couldn't add seed stock right now. Please try again.";
            await LoadCropsAsync();
            return Page();
        }
    }

    private bool TryGetActor(out int actorId, out string role)
    {
        role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actorId)
            && role is "Farmer" or "Agent";
    }

    private async Task<List<FarmOption>> GetFarmsAsync(int actorId, string role)
    {
        const string sql = @"
            SELECT fp.UserId, fp.FarmName
            FROM farmer_profile fp
            WHERE (@Role = 'Farmer' AND fp.UserId = @ActorId)
               OR (@Role = 'Agent' AND EXISTS (
                    SELECT 1 FROM agent_farmer_assignment afa
                    WHERE afa.FarmerId = fp.UserId AND afa.AgentId = @ActorId
               ))
            ORDER BY fp.FarmName;";
        await using var connection = _db.CreateConnection();
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Role", SqlDbType.NVarChar, 10).Value = role;
        command.Parameters.Add("@ActorId", SqlDbType.Int).Value = actorId;
        await using var reader = await command.ExecuteReaderAsync();
        var farms = new List<FarmOption>();
        while (await reader.ReadAsync()) farms.Add(new FarmOption(reader.GetInt32(0), reader.GetString(1)));
        return farms;
    }

    private async Task LoadCropsAsync()
    {
        const string sql = "SELECT Id, Name, MaturityDays FROM crop ORDER BY Name;";
        await using var connection = _db.CreateConnection();
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        Crops = [];
        while (await reader.ReadAsync())
            Crops.Add(new CropOption(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2)));
    }

    private static void AddDecimal(SqlCommand command, string name, byte precision, byte scale, decimal value)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.Decimal);
        parameter.Precision = precision;
        parameter.Scale = scale;
        parameter.Value = value;
    }

    public sealed class SeedInput
    {
        [Range(1, int.MaxValue, ErrorMessage = "Choose a crop.")]
        public int CropId { get; set; }

        [Required, StringLength(80, MinimumLength = 1)]
        public string? Variety { get; set; }

        [Range(typeof(decimal), "0.01", "99999999", ErrorMessage = "Quantity must be greater than zero.")]
        public decimal Quantity { get; set; }

        [StringLength(120)]
        public string? Supplier { get; set; }
    }

    public sealed record FarmOption(int Id, string Name);
    public sealed record CropOption(int Id, string Name, int MaturityDays);
}
