using AgriFlow.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AgriFlow.Pages.Buyer;

[Authorize(Roles = "Buyer")]
public class DashboardModel : PageModel
{
    private readonly FarmerRepository _repository;
    public IReadOnlyList<MarketplaceRow> Listings { get; private set; } = [];
    public int ActiveListings => Listings.Count;

    public DashboardModel(FarmerRepository repository) => _repository = repository;

    public async Task OnGetAsync() => Listings = await _repository.GetMarketplaceAsync();
}