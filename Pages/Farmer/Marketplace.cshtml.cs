using AgriFlow.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AgriFlow.Pages.Farmer;

[Authorize(Roles = "Farmer")]
public class MarketplaceModel : PageModel
{
    private readonly FarmerRepository _repository;

    public IReadOnlyList<MarketplaceRow> Listings { get; private set; } = [];

    public MarketplaceModel(FarmerRepository repository) => _repository = repository;

    public async Task OnGetAsync()
    {
        Listings = await _repository.GetMarketplaceAsync();
    }
}
