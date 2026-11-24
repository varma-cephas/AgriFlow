using System.Globalization;
using AgriFlow.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AgriFlow.Pages.Buyer;

[Authorize(Roles = "Buyer")]
public class DashboardModel : PageModel
{
    private readonly BuyerDashboardRepository _repo;

    public DashboardModel(BuyerDashboardRepository repo) => _repo = repo;

    public BuyerDashboardData Dash { get; private set; } = null!;
    public DateTime Today => DateTime.Today;

    public async Task<IActionResult> OnGetAsync()
    {
        if (!int.TryParse(User.FindFirst("BuyerBusinessId")?.Value, out var businessId)) return Forbid();

        var data = await _repo.GetAsync(businessId, DateTime.Today);
        if (data is null) return NotFound();

        Dash = data;
        return Page();
    }

    // ---- Chart helpers ----
    public decimal ChartMax => Dash.Weeks.Max(w => Math.Max(w.ConfirmedTonnes, w.ReservedTonnes));

    public string Pct(decimal value) =>
        ChartMax <= 0 ? "0" : (value / ChartMax * 100).ToString("0.##", CultureInfo.InvariantCulture);

    public string MinH(decimal value) => value > 0 ? "3px" : "0";

    // ---- Formatting helpers (fixed culture so every PC shows the same) ----
    public string Money(decimal value) => value.ToString("#,##0", CultureInfo.InvariantCulture) + " RWF";
    public string Num(decimal value) => value.ToString("#,##0.##", CultureInfo.InvariantCulture);
    public string Tonnes(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    public string ShortDate(DateTime d) => d.ToString("dd MMM", CultureInfo.InvariantCulture);
    public string LongDate(DateTime d) => d.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
    public string Plural(decimal qty, string unit) => qty == 1 ? unit : unit + "s";

    public string StatusBadge(string status) => status switch
    {
        "Reserved" => "bg-info-subtle text-info-emphasis border border-info-subtle",
        "Placed" or "Confirmed" => "bg-primary-subtle text-primary-emphasis border border-primary-subtle",
        "Dispatched" => "bg-warning-subtle text-warning-emphasis border border-warning-subtle",
        "Delivered" or "Paid" => "bg-success-subtle text-success border border-success-subtle",
        "Cancelled" => "bg-danger-subtle text-danger border border-danger-subtle",
        _ => "bg-secondary-subtle text-secondary-emphasis border border-secondary-subtle"
    };
}