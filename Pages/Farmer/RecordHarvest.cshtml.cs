using System.Globalization;
using System.Security.Claims;
using AgriFlow.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AgriFlow.Pages.Farmer;

[Authorize(Roles = "Farmer")]
public class RecordHarvestModel : PageModel
{
    private readonly HarvestRepository _repo;

    public RecordHarvestModel(HarvestRepository repo) => _repo = repo;

    [BindProperty(SupportsGet = true)] public int BatchId { get; set; }

    // Read as text and parsed with a fixed culture, so "104.5" works on every PC.
    [BindProperty] public string? ActualYield { get; set; }
    [BindProperty] public string? HarvestDate { get; set; }
    [BindProperty] public string? Unit { get; set; }
    [BindProperty] public string? Grade { get; set; }

    [TempData] public string? StatusMessage { get; set; }
    public string? FormError { get; private set; }

    public HarvestBatch Batch { get; private set; } = null!;
    public List<HarvestReservation> Reservations { get; private set; } = new();

    public bool IsActive => Batch.Status == "Active" && Batch.ListingId.HasValue;
    public bool HasReservations => Reservations.Any(r => r.Status == "Reserved");

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadAsync()) return NotFound();

        HarvestDate ??= DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        Unit ??= Batch.Unit;
        Grade ??= Batch.Grade;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await LoadAsync()) return NotFound();
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Forbid();

        if (!IsActive)
        {
            FormError = "This batch is not open for harvest.";
            return Page();
        }

        if (!decimal.TryParse(ActualYield, NumberStyles.Number, CultureInfo.InvariantCulture, out var yield) || yield <= 0)
        {
            FormError = "The actual yield must be more than zero.";
            return Page();
        }
        if (decimal.Round(yield, 2) != yield)
        {
            FormError = "The yield can have at most 2 decimal places.";
            return Page();
        }

        if (!DateTime.TryParseExact(HarvestDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            FormError = "Choose the harvest date.";
            return Page();
        }
        if (date.Date > DateTime.Today)
        {
            FormError = "The harvest date cannot be in the future.";
            return Page();
        }
        if (date.Date < Batch.PlantingDate.Date)
        {
            FormError = "The harvest date cannot be before the planting date.";
            return Page();
        }

        // With reservations, the unit stays as buyers reserved it.
        var unit = HasReservations ? Batch.Unit : Unit;
        if (unit is not ("crate" or "bag" or "tonne"))
        {
            FormError = "Choose a selling unit.";
            return Page();
        }
        if (Grade is not ("A" or "B" or "C"))
        {
            FormError = "Choose a grade.";
            return Page();
        }

        var result = await _repo.RecordHarvestAsync(
            new RecordHarvestRequest(BatchId, userId, yield, date, unit, Grade));

        if (!result.Success)
        {
            FormError = result.ErrorMessage;
            await LoadAsync();   // show the real current state
            return Page();
        }

        var u = result.Unit;
        StatusMessage =
            $"Harvest recorded: {FormatNumber(result.Yield)} {Plural(result.Yield, u)} harvested against " +
            $"{FormatNumber(result.Reserved)} reserved. {result.Full} filled in full, {result.Partial} partly filled, " +
            $"{result.Cancelled} cancelled (shortfall). {FormatNumber(result.Leftover)} {Plural(result.Leftover, u)} now on sale.";

        return RedirectToPage(new { batchId = BatchId });
    }

    private async Task<bool> LoadAsync()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return false;

        var view = await _repo.GetHarvestViewAsync(BatchId);
        // Another farmer's batch looks the same as a batch that does not exist.
        if (view is null || view.Batch.FarmerId != userId) return false;

        Batch = view.Batch;
        Reservations = view.Reservations;
        return true;
    }

    // ---- Helpers used by the page ----
    public string Today => DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public string MinDate => Batch.PlantingDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public string Attr(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    public string FormatNumber(decimal value) => value.ToString("#,##0.##", CultureInfo.InvariantCulture);
    public string FormatDate(DateTime d) => d.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
    public string FormatDateTime(DateTime d) => d.ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture);

    public string Plural(decimal qty, string unit) => qty == 1 ? unit : unit + "s";
    public string UnitPlural(decimal qty) => Plural(qty, Batch.Unit ?? "");

    public string OutcomeText(HarvestReservation r) => r.Status switch
    {
        "Placed" when r.QtyConfirmed.HasValue && r.QtyConfirmed.Value < r.QtyRequested =>
            $"Partly filled: {FormatNumber(r.QtyConfirmed.Value)} of {FormatNumber(r.QtyRequested)}",
        "Placed" => "Filled in full",
        "Cancelled" => "Cancelled (shortfall)",
        "Reserved" => "Waiting for harvest",
        _ => r.Status
    };
}