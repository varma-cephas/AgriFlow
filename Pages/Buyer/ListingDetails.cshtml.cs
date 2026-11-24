using System.Globalization;
using System.Security.Claims;
using AgriFlow.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AgriFlow.Pages.Buyer;

[Authorize(Roles = "Buyer")]
public class ListingDetailsModel : PageModel
{
    private readonly OrderRepository _repo;

    public ListingDetailsModel(OrderRepository repo) => _repo = repo;

    [BindProperty(SupportsGet = true)] public int Id { get; set; }

    // Read as text and parsed with a fixed culture, so "10.5" works on every PC.
    [BindProperty] public string? Quantity { get; set; }
    [BindProperty] public string? DeliveryDate { get; set; }
    [BindProperty] public string? DeliveryAddress { get; set; }

    // Filled once after an order is placed (shown in the "Order placed" pop-up).
    [TempData] public string? PlacedOrderNumber { get; set; }
    [TempData] public string? PlacedTotal { get; set; }
    [TempData] public string? PlacedFarm { get; set; }
    [TempData] public string? PlacedConfirmBy { get; set; }

    public ListingDetail Listing { get; private set; } = null!;
    public string BuyerStatus { get; private set; } = "Pending";
    public string? ErrorMessage { get; private set; }

    public bool IsSoldOut => Listing.Status != "Available" || Listing.AvailableQty <= 0;
    public bool CanOrder => BuyerStatus == "Verified" && !IsSoldOut;

    public string? RestrictionMessage => BuyerStatus switch
    {
        "Verified" => null,
        "Rejected" => "Your business was not approved, so ordering is disabled. Contact an AgriFlow administrator for help.",
        _ => "Your business is waiting for admin verification. You can look at this listing, but ordering is disabled until you are approved."
    };

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadAsync()) return NotFound();

        Quantity ??= Attr(Listing.MinOrderQty);
        DeliveryDate ??= DateTime.Today.AddDays(3).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await LoadAsync()) return NotFound();

        if (!int.TryParse(User.FindFirstValue("BuyerBusinessId"), out var businessId) ||
            !int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Forbid();
        }

        if (!CanOrder)
        {
            ErrorMessage = IsSoldOut ? "This listing is sold out." : RestrictionMessage;
            return Page();
        }

        // ---- Validate what the buyer typed ----
        if (!decimal.TryParse(Quantity, NumberStyles.Number, CultureInfo.InvariantCulture, out var qty) || qty <= 0)
        {
            ErrorMessage = "Enter a valid quantity.";
            return Page();
        }
        if (decimal.Round(qty, 2) != qty)
        {
            ErrorMessage = "The quantity can have at most 2 decimal places.";
            return Page();
        }
        if (qty < Listing.MinOrderQty)
        {
            ErrorMessage = $"The minimum order is {FormatNumber(Listing.MinOrderQty)} {Plural(Listing.MinOrderQty)}.";
            return Page();
        }
        if (qty > Listing.AvailableQty)
        {
            ErrorMessage = $"Only {FormatNumber(Listing.AvailableQty)} {Plural(Listing.AvailableQty)} left. Please lower your quantity.";
            return Page();
        }

        if (!DateTime.TryParseExact(DeliveryDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            ErrorMessage = "Choose a delivery date.";
            return Page();
        }
        if (date.Date <= DateTime.Today)
        {
            ErrorMessage = "Choose a delivery date after today.";
            return Page();
        }

        var address = DeliveryAddress?.Trim() ?? "";
        if (address.Length == 0)
        {
            ErrorMessage = "Enter the address where the produce should be delivered.";
            return Page();
        }
        if (address.Length > 250)
        {
            ErrorMessage = "The delivery address is too long (250 characters at most).";
            return Page();
        }

        // ---- Place the order (one database transaction) ----
        var result = await _repo.PlaceOrderAsync(
            new PlaceOrderRequest(Id, userId, businessId, qty, date, address));

        if (!result.Success)
        {
            ErrorMessage = result.ErrorMessage;
            var fresh = await _repo.GetListingDetailAsync(Id);   // show the real remaining stock
            if (fresh is not null) Listing = fresh;
            return Page();
        }

        PlacedOrderNumber = result.OrderNumber;
        PlacedTotal = Money(result.Total);
        PlacedFarm = result.FarmName;
        PlacedConfirmBy = FormatDate(DateTime.Today.AddDays(2));

        // Redirect so a refresh doesn't place the order twice.
        return RedirectToPage(new { id = Id });
    }

    private async Task<bool> LoadAsync()
    {
        var detail = await _repo.GetListingDetailAsync(Id);
        if (detail is null) return false;
        Listing = detail;

        if (int.TryParse(User.FindFirstValue("BuyerBusinessId"), out var businessId))
        {
            var buyer = await _repo.GetBuyerInfoAsync(businessId);
            if (buyer is not null)
            {
                BuyerStatus = buyer.Status;
                var defaultAddress = buyer.Address.Length > 250 ? buyer.Address[..250] : buyer.Address;
                DeliveryAddress ??= defaultAddress;
            }
        }
        return true;
    }

    // ---- Helpers used by the page ----
    public string MinDate => DateTime.Today.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public string PriceJs => Listing.PricePerUnit.ToString(CultureInfo.InvariantCulture);

    public string Attr(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    public string FormatNumber(decimal value) => value.ToString("#,##0.##", CultureInfo.InvariantCulture);

    public string Money(decimal value) => value.ToString("#,##0", CultureInfo.InvariantCulture) + " RWF";

    public string FormatDate(DateTime date) => date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

    public string Plural(decimal qty) => qty == 1 ? Listing.Unit : Listing.Unit + "s";

    public string UnitLabel() =>
        Listing.UnitSizeKg.HasValue ? $"{Listing.Unit} ({FormatNumber(Listing.UnitSizeKg.Value)} kg)" : Listing.Unit;
}