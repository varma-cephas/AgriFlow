using System.Globalization;
using System.Security.Claims;
using AgriFlow.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AgriFlow.Pages.Buyer;

[Authorize(Roles = "Buyer")]
public class MarketplaceModel : PageModel
{
    private readonly MarketplaceRepository _repo;
    private readonly OrderRepository _orders;
    private readonly ReservationRepository _reservations;

    public MarketplaceModel(MarketplaceRepository repo, OrderRepository orders, ReservationRepository reservations)
    {
        _repo = repo;
        _orders = orders;
        _reservations = reservations;
    }

    // Filters come from the address bar (?Tab=...&Search=...), so a filtered page can be bookmarked.
    [BindProperty(SupportsGet = true)] public string? Tab { get; set; }
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public int? CropId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Province { get; set; }
    [BindProperty(SupportsGet = true)] public string? Grade { get; set; }
    [BindProperty(SupportsGet = true)] public string? Sort { get; set; }

    // Messages shown once after a reservation attempt.
    [TempData] public string? StatusMessage { get; set; }
    [TempData] public string? ErrorMessage { get; set; }

    public List<MarketplaceListing> Listings { get; private set; } = new();
    public MarketplaceFilterOptions Options { get; private set; } =
        new MarketplaceFilterOptions(new List<CropOption>(), new List<string>());

    public int AllCount { get; private set; }
    public int AvailableCount { get; private set; }
    public int UpcomingCount { get; private set; }

    // 'Verified', 'Pending' or 'Rejected'. Anything unknown is treated as Pending.
    public string BuyerStatus { get; private set; } = "Pending";
    public bool CanAct => BuyerStatus == "Verified";
    public string DefaultAddress { get; private set; } = "";

    public string? RestrictionMessage => BuyerStatus switch
    {
        "Verified" => null,
        "Rejected" => "Your business was not approved, so ordering and reserving are disabled. Contact an AgriFlow administrator for help.",
        _ => "Your business is waiting for admin verification. You can browse the marketplace, but ordering and reserving are disabled until you are approved."
    };

    public bool HasFilters =>
        !string.IsNullOrWhiteSpace(Search) || CropId.HasValue ||
        !string.IsNullOrWhiteSpace(Province) || !string.IsNullOrWhiteSpace(Grade) ||
        !string.IsNullOrWhiteSpace(Sort);

    public async Task OnGetAsync()
    {
        // Keep only values we expect.
        Tab = Tab is "available" or "upcoming" ? Tab : "all";
        Grade = Grade is "A" or "B" or "C" ? Grade : null;
        Sort = Sort is "price_asc" or "price_desc" or "date" ? Sort : null;

        if (int.TryParse(User.FindFirstValue("BuyerBusinessId"), out var businessId))
        {
            var buyer = await _orders.GetBuyerInfoAsync(businessId);
            if (buyer is not null)
            {
                BuyerStatus = buyer.Status;
                DefaultAddress = buyer.Address.Length > 250 ? buyer.Address[..250] : buyer.Address;
            }
        }

        var all = await _repo.GetListingsAsync(new MarketplaceFilter(Search, CropId, Province, Grade, Sort));

        // Counts for the tabs use the current filters but ignore the selected tab.
        AllCount = all.Count;
        AvailableCount = all.Count(l => l.Status == "Available");
        UpcomingCount = all.Count(l => l.Status == "Upcoming");

        Listings = Tab == "available" ? all.Where(l => l.Status == "Available").ToList()
                 : Tab == "upcoming" ? all.Where(l => l.Status == "Upcoming").ToList()
                 : all;

        Options = await _repo.GetFilterOptionsAsync();
    }

    // Runs when the "Reserve supply" pop-up form is submitted.
    public async Task<IActionResult> OnPostReserveAsync(int listingId, string? quantity, string? deliveryAddress)
    {
        if (!int.TryParse(User.FindFirstValue("BuyerBusinessId"), out var businessId) ||
            !int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Forbid();
        }

        if (!decimal.TryParse(quantity, NumberStyles.Number, CultureInfo.InvariantCulture, out var qty) || qty <= 0)
        {
            ErrorMessage = "Enter a valid quantity.";
            return RedirectToPage(new { Tab = "upcoming" });
        }
        if (decimal.Round(qty, 2) != qty)
        {
            ErrorMessage = "The quantity can have at most 2 decimal places.";
            return RedirectToPage(new { Tab = "upcoming" });
        }

        var address = deliveryAddress?.Trim() ?? "";
        if (address.Length == 0 || address.Length > 250)
        {
            ErrorMessage = "Enter the delivery address (250 characters at most).";
            return RedirectToPage(new { Tab = "upcoming" });
        }

        var result = await _reservations.ReserveAsync(
            new ReserveRequest(listingId, userId, businessId, qty, address));

        if (result.Success)
        {
            var unit = qty == 1 ? result.Unit : result.Unit + "s";
            StatusMessage = $"Reservation saved ({result.OrderNumber}): {FormatNumber(qty)} {unit} of {result.CropName} " +
                            $"from {result.FarmName}. Estimated value {Money(result.Total)}. " +
                            "It will be filled oldest first at harvest.";
        }
        else
        {
            ErrorMessage = result.ErrorMessage;
        }

        return RedirectToPage(new { Tab = "upcoming" });
    }

    // Tab links keep the filters the buyer already chose.
    public string? TabUrl(string tab) => Url.Page("/Buyer/Marketplace", new
    {
        Tab = tab,
        Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim(),
        CropId,
        Province = string.IsNullOrWhiteSpace(Province) ? null : Province,
        Grade,
        Sort
    });

    public string? ClearUrl => Url.Page("/Buyer/Marketplace", new { Tab });

    // Opens the listing page built in AGRI-24.
    public string OrderUrl(int listingId) => $"/Buyer/ListingDetails?id={listingId}";

    // ---- Display helpers (fixed culture so numbers look the same on every PC) ----
    public string Attr(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    public string Money(decimal value) =>
        value.ToString("#,##0", CultureInfo.InvariantCulture) + " RWF";

    public string FormatNumber(decimal value) =>
        value.ToString("#,##0.##", CultureInfo.InvariantCulture);

    public string FormatDate(DateTime date) =>
        date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

    public string Qty(MarketplaceListing l) =>
        FormatNumber(l.Quantity) + " " + (l.Quantity == 1 ? l.Unit : l.Unit + "s");

    public string MinOrder(MarketplaceListing l) =>
        FormatNumber(l.MinOrderQty) + " " + (l.MinOrderQty == 1 ? l.Unit : l.Unit + "s");

    public string UnitLabel(MarketplaceListing l) =>
        l.UnitSizeKg.HasValue ? $"{l.Unit} ({FormatNumber(l.UnitSizeKg.Value)} kg)" : l.Unit;
}