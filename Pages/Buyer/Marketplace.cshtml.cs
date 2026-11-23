using System.Security.Claims;
using AgriFlow.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AgriFlow.Pages.Buyer;

[Authorize(Roles = "Buyer")]
public class MarketplaceModel : PageModel
{
    private readonly FarmerRepository _repository;
    private readonly BuyerRepository _buyerRepository;
    public IReadOnlyList<MarketplaceRow> Listings { get; private set; } = [];
    [BindProperty] public int ListingId { get; set; }
    [BindProperty] public decimal Quantity { get; set; }
    public string? Error { get; private set; }
    public string? Success { get; private set; }

    public MarketplaceModel(FarmerRepository repository, BuyerRepository buyerRepository) { _repository = repository; _buyerRepository = buyerRepository; }
    public async Task OnGetAsync() => Listings = await _repository.GetMarketplaceAsync();
    public async Task<IActionResult> OnPostOrderAsync()
    {
        try
        {
            var orderId = await _buyerRepository.PlaceOrderAsync(UserId(), ListingId, Quantity);
            TempData["SuccessMessage"] = $"Order #{orderId} was placed successfully and sent to the farmer.";
            return RedirectToPage("/Buyer/Orders");
        }
        catch (InvalidOperationException ex)
        {
            Error = ex.Message;
            Listings = await _repository.GetMarketplaceAsync();
            return Page();
        }
    }
    private int UserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
