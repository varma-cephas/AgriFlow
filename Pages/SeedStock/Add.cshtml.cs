using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace AgriFlow.Pages.SeedStock;

public class AddModel : PageModel
{
    private readonly IConfiguration _configuration;

    public AddModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [BindProperty]
    public SeedStockInput Input { get; set; } = new();

    public List<CropOption> Crops { get; private set; } = [];
    public string StatusMessage { get; private set; } = string.Empty;

    public IActionResult OnGet()
    {
        var farmerId = ResolveFarmerId();
        if (!farmerId.HasValue)
        {
            StatusMessage = "Please select an active farmer or sign in to add seed stock.";
            return Page();
        }
        LoadCrops();
        return Page();
    }

    public IActionResult OnPost()
    {
        ValidateInput();
        var farmerId = ResolveFarmerId();
        if (!farmerId.HasValue)
        {
            StatusMessage = "Please select an active farmer or sign in to add seed stock.";
            LoadCrops();
            return Page();
        }
        if (!ModelState.IsValid)
        {
            LoadCrops();
            return Page();
        }

        SqlConnection? connection = null;
        SqlTransaction? transaction = null;
        try
        {
            connection = new SqlConnection(_configuration.GetConnectionString("AgriFlowDb"));
            connection.Open();
            transaction = connection.BeginTransaction();

            if (!CropExists(connection, transaction, Input.CropId))
            {
                transaction.Rollback();
                StatusMessage = "Please select a valid crop.";
                LoadCrops();
                return Page();
            }

            using var duplicateCommand = connection.CreateCommand();
            duplicateCommand.Transaction = transaction;
            duplicateCommand.CommandText = "SELECT COUNT(1) FROM seed_stock WHERE FarmerId = @FarmerId AND CropId = @CropId AND Variety = @Variety";
            duplicateCommand.Parameters.AddWithValue("@FarmerId", farmerId.Value);
            duplicateCommand.Parameters.AddWithValue("@CropId", Input.CropId);
            duplicateCommand.Parameters.AddWithValue("@Variety", Input.Variety.Trim());
            if ((int)duplicateCommand.ExecuteScalar() > 0)
            {
                transaction.Rollback();
                StatusMessage = "This crop variety is already in the seed inventory.";
                LoadCrops();
                return Page();
            }

            using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = @"
INSERT INTO seed_stock (FarmerId, CropId, Variety, Supplier, Quantity, Unit, CreatedAt, UpdatedAt)
OUTPUT INSERTED.Id
VALUES (@FarmerId, @CropId, @Variety, @Supplier, @Quantity, @Unit, SYSUTCDATETIME(), SYSUTCDATETIME())";
            insertCommand.Parameters.AddWithValue("@FarmerId", farmerId.Value);
            insertCommand.Parameters.AddWithValue("@CropId", Input.CropId);
            insertCommand.Parameters.AddWithValue("@Variety", Input.Variety.Trim());
            insertCommand.Parameters.AddWithValue("@Supplier", string.IsNullOrWhiteSpace(Input.Supplier) ? DBNull.Value : Input.Supplier.Trim());
            insertCommand.Parameters.AddWithValue("@Quantity", Input.Quantity);
            insertCommand.Parameters.AddWithValue("@Unit", Input.Unit.Trim());
            var entityId = (int)insertCommand.ExecuteScalar();

            WriteAudit(connection, transaction, farmerId.Value, entityId, "Create", "Added seed stock.", "Success");
            transaction.Commit();
            return RedirectToPage("./Index", new { status = "Seed stock added successfully." });
        }
        catch (SqlException)
        {
            transaction?.Rollback();
            StatusMessage = "The seed stock could not be added. Please check the details and try again.";
            LoadCrops();
            return Page();
        }
        finally
        {
            transaction?.Dispose();
            connection?.Dispose();
        }
    }

    private int? ResolveFarmerId()
    {
        var role = SeedStockContext.GetRole(User);
        if (role.Equals("Farmer", StringComparison.OrdinalIgnoreCase))
            return SeedStockContext.GetFarmerId(User);
        if (role.Equals("Agent", StringComparison.OrdinalIgnoreCase))
        {
            var farmerId = SeedStockContext.GetActiveFarmerId(HttpContext, User);
            if (!farmerId.HasValue) return null;
            using var connection = new SqlConnection(_configuration.GetConnectionString("AgriFlowDb"));
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(1) FROM agent_farmer_assignment WHERE AgentId = @AgentId AND FarmerId = @FarmerId";
            command.Parameters.AddWithValue("@AgentId", SeedStockContext.GetActorUserId(User) ?? 0);
            command.Parameters.AddWithValue("@FarmerId", farmerId.Value);
            return (int)command.ExecuteScalar() > 0 ? farmerId : null;
        }
        if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            return SeedStockContext.GetActiveFarmerId(HttpContext, User);
        return null;
    }

    private void ValidateInput()
    {
        if (Input.CropId <= 0) ModelState.AddModelError("Input.CropId", "Select a crop.");
        if (string.IsNullOrWhiteSpace(Input.Variety)) ModelState.AddModelError("Input.Variety", "Variety is required.");
        else if (Input.Variety.Trim().Length > 80) ModelState.AddModelError("Input.Variety", "Variety cannot exceed 80 characters.");
        if (Input.Quantity < 0) ModelState.AddModelError("Input.Quantity", "Quantity cannot be negative.");
        if (string.IsNullOrWhiteSpace(Input.Unit)) ModelState.AddModelError("Input.Unit", "Unit is required.");
        else if (Input.Unit.Trim().Length > 10) ModelState.AddModelError("Input.Unit", "Unit cannot exceed 10 characters.");
        if (!string.IsNullOrEmpty(Input.Supplier) && Input.Supplier.Trim().Length > 120) ModelState.AddModelError("Input.Supplier", "Supplier cannot exceed 120 characters.");
    }

    private void LoadCrops()
    {
        try
        {
            using var connection = new SqlConnection(_configuration.GetConnectionString("AgriFlowDb"));
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name FROM crop ORDER BY Name";
            using var reader = command.ExecuteReader();
            var crops = new List<CropOption>();
            while (reader.Read()) crops.Add(new CropOption { Id = reader.GetInt32(0), Name = reader.GetString(1) });
            Crops = crops;
        }
        catch (SqlException)
        {
            StatusMessage = "Crop choices could not be loaded. Please try again.";
        }
    }

    private static bool CropExists(SqlConnection connection, SqlTransaction transaction, int cropId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(1) FROM crop WHERE Id = @CropId";
        command.Parameters.AddWithValue("@CropId", cropId);
        return (int)command.ExecuteScalar() > 0;
    }

    private void WriteAudit(SqlConnection connection, SqlTransaction transaction, int farmerId, int entityId, string action, string summary, string result)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
INSERT INTO audit_log (At, ActorUserId, OnBehalfOfFarmerId, Entity, EntityId, Action, Summary, Result)
VALUES (SYSUTCDATETIME(), @ActorUserId, @FarmerId, 'SeedStock', @EntityId, @Action, @Summary, @Result)";
        var actorId = SeedStockContext.GetActorUserId(User);
        command.Parameters.AddWithValue("@ActorUserId", actorId.HasValue ? actorId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@FarmerId", farmerId);
        command.Parameters.AddWithValue("@EntityId", entityId);
        command.Parameters.AddWithValue("@Action", action);
        command.Parameters.AddWithValue("@Summary", summary);
        command.Parameters.AddWithValue("@Result", result);
        command.ExecuteNonQuery();
    }

    public sealed class SeedStockInput
    {
        [Required(ErrorMessage = "Select a crop.")]
        public int CropId { get; set; }

        [Required(ErrorMessage = "Variety is required."), StringLength(80, ErrorMessage = "Variety cannot exceed 80 characters.")]
        public string Variety { get; set; } = string.Empty;

        [Required(ErrorMessage = "Quantity is required."), Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "Quantity cannot be negative.")]
        public decimal Quantity { get; set; }

        [Required(ErrorMessage = "Unit is required."), StringLength(10, ErrorMessage = "Unit cannot exceed 10 characters.")]
        public string Unit { get; set; } = "kg";

        [StringLength(120, ErrorMessage = "Supplier cannot exceed 120 characters.")]
        public string? Supplier { get; set; }
    }

    public sealed class CropOption
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }
}
