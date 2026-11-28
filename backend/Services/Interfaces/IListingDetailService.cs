using backend.Models.Marketplace;

namespace backend.Services.Interfaces;

public interface IListingDetailService
{
    Task<ListingDetailDto?> GetAsync(int listingId, CancellationToken ct = default);
}