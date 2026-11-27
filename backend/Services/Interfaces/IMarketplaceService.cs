using backend.Models.Marketplace;

namespace backend.Services.Interfaces;

public interface IMarketplaceService
{
    Task<(bool Success, string Message, MarketplaceResponse? Data)> BrowseAsync(
        MarketplaceQuery query, CancellationToken ct = default);
}