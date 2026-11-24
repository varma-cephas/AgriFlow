using AgriFlow.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Globalization;
using System.Security.Claims;

namespace AgriFlow.Pages.Buyer;

[Authorize(Roles = "Buyer")]
public class MarketplaceModel : PageModel
{
    private readonly MarketplaceRepository _repo;

    public MarketplaceModel(MarketplaceRepository repo) => _repo = repo;

    // Filters come from the address bar (?Tab=...&Search=...), so a filtered page can be bookmarked.
    [BindProperty(SupportsGet = true)] public string? Tab { get; set; }
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public int? CropId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Province { get; set; }
    [BindProperty(SupportsGet = true)] public string? Grade { get; set; }
    [BindProperty(SupportsGet = true)] public string? Sort { get; set; }

    public List<MarketplaceListing> Listings { get; private set; } = new();
    public MarketplaceFilterOptions Options { get; private set; } =
        new MarketplaceFilterOptions(new List<CropOption>(), new List<string>());

    public int AllCount { get; private set; }
    public int AvailableCount { get; private set; }
    public int UpcomingCount { get; private set; }

    // 'Verified', 'Pending' or 'Rejected'. Anything unknown is treated as Pending.
    public string BuyerStatus { get; private set; } = "Pending";
    public bool CanAct => BuyerStatus == "Verified";

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
            BuyerStatus = await _repo.GetBuyerStatusAsync(businessId) ?? "Pending";
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

    // These pages belong to AGRI-24 (order) and AGRI-23 (reserve). Change the paths
    // here when you build them.
    public string OrderUrl(int listingId) => $"/Buyer/ListingDetails?id={listingId}";
    public string ReserveUrl(int listingId) => $"/Buyer/Reserve?id={listingId}";

    // ---- Display helpers (fixed culture so numbers look the same on every PC) ----
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