using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using AgriFlow.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace AgriFlow.Pages.PlantingBatches;

[Authorize(Roles = "Farmer,Agent")]
public class CreateModel : PageModel
{
    private const decimal AcresToHectares = 0.4046856422m;
    private readonly DbConnectionFactory _db;
    private readonly ILogger<CreateModel> _logger;

    public CreateModel(DbConnectionFactory db, ILogger<CreateModel> logger)
    {
        _db = db;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public int? FarmId { get; set; }

    [BindProperty]
    public PlantingInput Input { get; set; } = new();

    public List<FarmOption> Farms { get; private set; } = [];
    public List<SeedOption> SeedStocks { get; private set; } = [];
    public List<string> ExistingFields { get; private set; } = [];
    public string FarmName { get; private set; } = string.Empty;
    public string FarmDistrict { get; private set; } = string.Empty;
    public string FarmProvince { get; private set; } = string.Empty;
    public bool FarmVerified { get; private set; }
    public bool ShowStockModal { get; private set; }
    public string StockSeedName { get; private set; } = string.Empty;
    public decimal StockEntered { get; private set; }
    public decimal StockAvailable { get; private set; }
    public string? PageError { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!TryGetActor(out var actorId, out var actorRole)) return Forbid();

        Farms = await GetAuthorizedFarmsAsync(actorId, actorRole);
        if (Farms.Count == 0) return Page();

        if (!FarmId.HasValue) FarmId = Farms[0].Id;
        var selectedFarm = Farms.FirstOrDefault(farm => farm.Id == FarmId.Value);
        if (selectedFarm is null) return NotFound();

        SetFarmDetails(selectedFarm);
        Input.BatchCode = GenerateCode("B-");
        Input.ListingCode = GenerateCode("L-");
        await LoadFarmOptionsAsync(selectedFarm.Id);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!TryGetActor(out var actorId, out var actorRole)) return Forbid();

        Input.FieldName = string.Equals(Input.FieldChoice, NewFieldChoice, StringComparison.Ordinal)
            ? Input.NewFieldName?.Trim()
            : Input.FieldChoice?.Trim();
        if (string.IsNullOrWhiteSpace(Input.FieldName))
            ModelState.AddModelError("Input.FieldChoice", "Choose a field or enter a new field name.");
        if (Input.FieldName?.Length > 60)
            ModelState.AddModelError("Input.FieldChoice", "Field name must be 60 characters or fewer.");
        if (!SellingUnits.Any(unit => unit.Value == Input.SellingUnit))
            ModelState.AddModelError("Input.SellingUnit", "Choose a valid selling unit.");
        if (!Regex.IsMatch(Input.BatchCode ?? string.Empty, "^B-[A-F0-9]{10}$") ||
            !Regex.IsMatch(Input.ListingCode ?? string.Empty, "^L-[A-F0-9]{10}$"))
        {
            ModelState.AddModelError(string.Empty, "The planting form has expired. Reload it and try again.");
        }

        await using var connection = _db.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);

        try
        {
            var farm = await GetAuthorizedFarmAsync(connection, transaction, FarmId, actorId, actorRole, lockRow: true);
            if (farm is null)
            {
                await transaction.RollbackAsync();
                return NotFound();
            }

            SetFarmDetails(farm);
            if (!ModelState.IsValid)
            {
                await transaction.RollbackAsync();
                Farms = await GetAuthorizedFarmsAsync(actorId, actorRole);
                await LoadFarmOptionsAsync(farm.Id);
                return Page();
            }

            var seed = await GetSeedStockAsync(connection, transaction, farm.Id, Input.SeedStockId);
            if (seed is null)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError("Input.SeedStockId", "Select seed stock that belongs to this farm.");
                Farms = await GetAuthorizedFarmsAsync(actorId, actorRole);
                await LoadFarmOptionsAsync(farm.Id);
                return Page();
            }

            if (seed.Quantity < Input.QuantityPlanted)
            {
                await transaction.RollbackAsync();
                ShowStockError(seed, Input.QuantityPlanted);
                Farms = await GetAuthorizedFarmsAsync(actorId, actorRole);
                await LoadFarmOptionsAsync(farm.Id);
                return Page();
            }

            const string updateStockSql = @"
                UPDATE seed_stock
                SET Quantity = Quantity - @Quantity,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE Id = @SeedStockId AND FarmerId = @FarmerId AND Quantity >= @Quantity;";
            await using (var updateStock = new SqlCommand(updateStockSql, connection, transaction))
            {
                AddDecimal(updateStock, "@Quantity", 10, 2, Input.QuantityPlanted);
                updateStock.Parameters.Add("@SeedStockId", SqlDbType.Int).Value = Input.SeedStockId;
                updateStock.Parameters.Add("@FarmerId", SqlDbType.Int).Value = farm.Id;
                if (await updateStock.ExecuteNonQueryAsync() != 1)
                {
                    var currentSeed = await GetSeedStockAsync(connection, transaction, farm.Id, Input.SeedStockId);
                    await transaction.RollbackAsync();
                    if (currentSeed is not null)
                    {
                        ShowStockError(currentSeed, Input.QuantityPlanted);
                        Farms = await GetAuthorizedFarmsAsync(actorId, actorRole);
                        await LoadFarmOptionsAsync(farm.Id);
                        return Page();
                    }

                    ModelState.AddModelError("Input.SeedStockId", "The selected seed stock is no longer available.");
                    Farms = await GetAuthorizedFarmsAsync(actorId, actorRole);
                    await LoadFarmOptionsAsync(farm.Id);
                    return Page();
                }
            }

            const string insertBatchSql = @"
                INSERT INTO planting_batch (
                    BatchCode, FarmerId, SeedStockId, QuantityPlanted, PlantingDate,
                    PredictedHarvestDate, FieldName, AreaHa, Status, RecordedByUserId
                )
                OUTPUT INSERTED.Id
                VALUES (
                    @BatchCode, @FarmerId, @SeedStockId, @QuantityPlanted, @PlantingDate,
                    DATEADD(day, @MaturityDays, @PlantingDate), @FieldName, @AreaHa, 'Active', @RecordedByUserId
                );";

            int batchId;
            await using (var insertBatch = new SqlCommand(insertBatchSql, connection, transaction))
            {
                insertBatch.Parameters.Add("@BatchCode", SqlDbType.NVarChar, 20).Value = Input.BatchCode!;
                insertBatch.Parameters.Add("@FarmerId", SqlDbType.Int).Value = farm.Id;
                insertBatch.Parameters.Add("@SeedStockId", SqlDbType.Int).Value = Input.SeedStockId;
                AddDecimal(insertBatch, "@QuantityPlanted", 10, 2, Input.QuantityPlanted);
                insertBatch.Parameters.Add("@PlantingDate", SqlDbType.Date).Value = Input.PlantingDate.Date;
                insertBatch.Parameters.Add("@MaturityDays", SqlDbType.Int).Value = seed.MaturityDays;
                insertBatch.Parameters.Add("@FieldName", SqlDbType.NVarChar, 60).Value = Input.FieldName!;
                AddDecimal(insertBatch, "@AreaHa", 8, 2, decimal.Round(Input.AreaAcres * AcresToHectares, 2));
                insertBatch.Parameters.Add("@RecordedByUserId", SqlDbType.Int).Value = actorId;
                batchId = (int)(await insertBatch.ExecuteScalarAsync())!;
            }

            var unit = SellingUnits.Single(item => item.Value == Input.SellingUnit);
            var estimatedQuantity = Input.QuantityPlanted * seed.YieldFactor / unit.SizeKg;
            const string insertListingSql = @"
                INSERT INTO listing (
                    ListingCode, BatchId, FarmerId, CropId, Grade, Unit, UnitSizeKg,
                    PricePerUnit, MinOrderQty, EstimatedQty, Status, OpenForReservation
                )
                VALUES (
                    @ListingCode, @BatchId, @FarmerId, @CropId, @Grade, @Unit, @UnitSizeKg,
                    @PricePerUnit, @MinOrderQty, @EstimatedQty, 'Upcoming', @OpenForReservation
                );";
            await using (var insertListing = new SqlCommand(insertListingSql, connection, transaction))
            {
                insertListing.Parameters.Add("@ListingCode", SqlDbType.NVarChar, 20).Value = Input.ListingCode!;
                insertListing.Parameters.Add("@BatchId", SqlDbType.Int).Value = batchId;
                insertListing.Parameters.Add("@FarmerId", SqlDbType.Int).Value = farm.Id;
                insertListing.Parameters.Add("@CropId", SqlDbType.Int).Value = seed.CropId;
                insertListing.Parameters.Add("@Grade", SqlDbType.Char, 1).Value = Input.Grade;
                insertListing.Parameters.Add("@Unit", SqlDbType.NVarChar, 20).Value = unit.Label;
                AddDecimal(insertListing, "@UnitSizeKg", 8, 2, unit.SizeKg);
                AddDecimal(insertListing, "@PricePerUnit", 12, 2, Input.PricePerUnit);
                AddDecimal(insertListing, "@MinOrderQty", 10, 2, Input.MinimumOrder);
                AddDecimal(insertListing, "@EstimatedQty", 12, 2, decimal.Round(estimatedQuantity, 2));
                insertListing.Parameters.Add("@OpenForReservation", SqlDbType.Bit).Value = Input.OpenForReservationsNow && farm.Verified;
                await insertListing.ExecuteNonQueryAsync();
            }

            var remainingStock = seed.Quantity - Input.QuantityPlanted;
            await transaction.CommitAsync();
            TempData["SuccessMessage"] = $"Planting recorded. {seed.CropName} is now listed as Upcoming.";
            if (remainingStock <= 10)
                TempData["LowStockWarning"] = $"Low seed stock: {seed.Variety} has {remainingStock:N2} {seed.Unit} remaining.";
            return RedirectToPage("./Create", new { farmId = farm.Id });
        }
        catch (SqlException exception)
        {
            await transaction.RollbackAsync();
            _logger.LogError(exception, "Unable to record a planting for farm {FarmId}.", FarmId);
            PageError = "We couldn't save this planting right now. Please try again.";
            Farms = await GetAuthorizedFarmsAsync(actorId, actorRole);
            if (FarmId.HasValue)
            {
                var selectedFarm = Farms.FirstOrDefault(item => item.Id == FarmId.Value);
                if (selectedFarm is not null) SetFarmDetails(selectedFarm);
                await LoadFarmOptionsAsync(FarmId.Value);
            }
            return Page();
        }
    }

    private static readonly (string Value, string Label, decimal SizeKg)[] SellingUnits =
    [
        ("crate", "Crate (25 kg)", 25m),
        ("kg", "Kilogram (kg)", 1m),
        ("sack", "Sack (50 kg)", 50m)
    ];

    private const string NewFieldChoice = "__new_field__";

    public IReadOnlyList<(string Value, string Label)> UnitOptions => SellingUnits
        .Select(item => (item.Value, item.Label)).ToArray();

    private bool TryGetActor(out int actorId, out string actorRole)
    {
        actorId = 0;
        actorRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actorId)
            && actorRole is "Farmer" or "Agent";
    }

    private async Task<List<FarmOption>> GetAuthorizedFarmsAsync(int actorId, string actorRole)
    {
        const string sql = @"
            SELECT fp.UserId, fp.FarmName, fp.District, fp.Province, fp.VerificationStatus
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
        command.Parameters.Add("@Role", SqlDbType.NVarChar, 10).Value = actorRole;
        command.Parameters.Add("@ActorId", SqlDbType.Int).Value = actorId;
        await using var reader = await command.ExecuteReaderAsync();
        var farms = new List<FarmOption>();
        while (await reader.ReadAsync())
        {
            farms.Add(new FarmOption(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                string.Equals(reader.GetString(4), "Verified", StringComparison.Ordinal)));
        }
        return farms;
    }

    private static async Task<FarmOption?> GetAuthorizedFarmAsync(
        SqlConnection connection, SqlTransaction transaction, int? farmId, int actorId, string actorRole, bool lockRow)
    {
        if (!farmId.HasValue) return null;
        var lockHint = lockRow ? "WITH (UPDLOCK, HOLDLOCK)" : string.Empty;
        var sql = $@"
            SELECT fp.UserId, fp.FarmName, fp.District, fp.Province, fp.VerificationStatus
            FROM farmer_profile fp {lockHint}
            WHERE fp.UserId = @FarmId
              AND ((@Role = 'Farmer' AND fp.UserId = @ActorId)
                OR (@Role = 'Agent' AND EXISTS (
                    SELECT 1 FROM agent_farmer_assignment afa WITH (HOLDLOCK)
                    WHERE afa.FarmerId = fp.UserId AND afa.AgentId = @ActorId
                )));";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@FarmId", SqlDbType.Int).Value = farmId.Value;
        command.Parameters.Add("@Role", SqlDbType.NVarChar, 10).Value = actorRole;
        command.Parameters.Add("@ActorId", SqlDbType.Int).Value = actorId;
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return new FarmOption(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
            string.Equals(reader.GetString(4), "Verified", StringComparison.Ordinal));
    }

    private async Task<SeedStock?> GetSeedStockAsync(SqlConnection connection, SqlTransaction transaction, int farmId, int seedStockId)
    {
        const string sql = @"
            SELECT ss.CropId, ss.Variety, ss.Quantity, ss.Unit, c.Name, c.MaturityDays, c.YieldFactor
            FROM seed_stock ss WITH (UPDLOCK, ROWLOCK)
            INNER JOIN crop c ON c.Id = ss.CropId
            WHERE ss.Id = @SeedStockId AND ss.FarmerId = @FarmerId;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@SeedStockId", SqlDbType.Int).Value = seedStockId;
        command.Parameters.Add("@FarmerId", SqlDbType.Int).Value = farmId;
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return new SeedStock(reader.GetInt32(0), reader.GetString(1), reader.GetDecimal(2), reader.GetString(3),
            reader.GetString(4), reader.GetInt32(5), reader.GetDecimal(6));
    }

    private async Task LoadFarmOptionsAsync(int farmId)
    {
        const string seedSql = @"
            SELECT ss.Id, c.Name, ss.Variety, ss.Quantity, ss.Unit, c.MaturityDays, c.YieldFactor
            FROM seed_stock ss
            INNER JOIN crop c ON c.Id = ss.CropId
            WHERE ss.FarmerId = @FarmId
            ORDER BY c.Name, ss.Variety;";
        const string fieldSql = @"
            SELECT DISTINCT FieldName
            FROM planting_batch
            WHERE FarmerId = @FarmId AND FieldName IS NOT NULL AND LTRIM(RTRIM(FieldName)) <> ''
            ORDER BY FieldName;";

        await using var connection = _db.CreateConnection();
        await connection.OpenAsync();
        await using (var command = new SqlCommand(seedSql, connection))
        {
            command.Parameters.Add("@FarmId", SqlDbType.Int).Value = farmId;
            await using var reader = await command.ExecuteReaderAsync();
            SeedStocks = [];
            while (await reader.ReadAsync())
                SeedStocks.Add(new SeedOption(reader.GetInt32(0), reader.GetString(1), reader.GetString(2),
                    reader.GetDecimal(3), reader.GetString(4), reader.GetInt32(5), reader.GetDecimal(6)));
        }

        await using (var command = new SqlCommand(fieldSql, connection))
        {
            command.Parameters.Add("@FarmId", SqlDbType.Int).Value = farmId;
            await using var reader = await command.ExecuteReaderAsync();
            ExistingFields = [];
            while (await reader.ReadAsync()) ExistingFields.Add(reader.GetString(0));
        }

        if (Input.SeedStockId == 0 && SeedStocks.Count > 0) Input.SeedStockId = SeedStocks[0].Id;
        if (string.IsNullOrWhiteSpace(Input.BatchCode)) Input.BatchCode = GenerateCode("B-");
        if (string.IsNullOrWhiteSpace(Input.ListingCode)) Input.ListingCode = GenerateCode("L-");
        if (string.IsNullOrWhiteSpace(Input.FieldChoice) && ExistingFields.Count > 0)
        {
            Input.FieldChoice = ExistingFields[0];
            Input.FieldName = ExistingFields[0];
        }
    }

    private void SetFarmDetails(FarmOption farm)
    {
        FarmName = farm.Name;
        FarmDistrict = farm.District;
        FarmProvince = farm.Province;
        FarmVerified = farm.Verified;
    }

    private void ShowStockError(SeedStock seed, decimal entered)
    {
        ShowStockModal = true;
        StockSeedName = $"{seed.Variety} {seed.CropName}";
        StockEntered = entered;
        StockAvailable = seed.Quantity;
    }

    private static string GenerateCode(string prefix) => $"{prefix}{RandomNumberGenerator.GetHexString(10)}";

    private static void AddDecimal(SqlCommand command, string name, byte precision, byte scale, decimal value)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.Decimal);
        parameter.Precision = precision;
        parameter.Scale = scale;
        parameter.Value = value;
    }

    public sealed class PlantingInput
    {
        [Range(1, int.MaxValue, ErrorMessage = "Select seed stock.")]
        public int SeedStockId { get; set; }

        [Range(typeof(decimal), "0.01", "10000000", ErrorMessage = "Enter a quantity greater than zero.")]
        public decimal QuantityPlanted { get; set; }

        [DataType(DataType.Date)]
        public DateTime PlantingDate { get; set; } = DateTime.Today;

        public string? FieldChoice { get; set; }
        public string? NewFieldName { get; set; }
        public string? FieldName { get; set; }

        [Range(typeof(decimal), "0.01", "2000000", ErrorMessage = "Enter an area greater than zero.")]
        public decimal AreaAcres { get; set; }

        [Required]
        public string SellingUnit { get; set; } = "crate";

        [Required]
        [RegularExpression("^[ABC]$", ErrorMessage = "Choose a valid grade.")]
        public string Grade { get; set; } = "A";

        [Range(typeof(decimal), "0.01", "1000000000", ErrorMessage = "Price must be greater than zero.")]
        public decimal PricePerUnit { get; set; }

        [Range(typeof(decimal), "0.01", "1000000", ErrorMessage = "Minimum order must be greater than zero.")]
        public decimal MinimumOrder { get; set; } = 1m;

        public bool OpenForReservationsNow { get; set; } = true;

        public string? BatchCode { get; set; }
        public string? ListingCode { get; set; }
    }

    public sealed record FarmOption(int Id, string Name, string District, string Province, bool Verified);
    public sealed record SeedOption(int Id, string CropName, string Variety, decimal Quantity, string Unit, int MaturityDays, decimal YieldFactor);
    private sealed record SeedStock(int CropId, string Variety, decimal Quantity, string Unit, string CropName, int MaturityDays, decimal YieldFactor);
}
