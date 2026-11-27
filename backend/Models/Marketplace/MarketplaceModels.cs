namespace backend.Models.Marketplace;

// Query string of GET /api/marketplace
public class MarketplaceQuery
{
    public string? Tab { get; set; }       // all | available | upcoming (default all)
    public string? Crop { get; set; }      // crop name, e.g. Tomato
    public string? Province { get; set; }  // e.g. "Eastern Province"
    public string? Grade { get; set; }     // A | B | C
    public string? Search { get; set; }    // crop, farm, district or listing code
    public string? Sort { get; set; }      // soonest | priceLow | priceHigh (default soonest)
    public int Page { get; set; } = 1;
}

public record MarketplaceListingDto(
    int Id,
    string Code,
    string Crop,
    string Grade,
    string FarmName,
    string District,
    string Province,
    string Status,            // Available | Upcoming
    decimal PricePerUnit,     // RWF
    string Unit,
    decimal MinOrderQty,
    decimal AvailableQty,     // Available: stock now. Upcoming: estimated qty still unreserved
    DateOnly HarvestDate);    // Available: date harvested. Upcoming: expected harvest date

public record MarketplaceTabCounts(int All, int Available, int Upcoming);

public record MarketplaceFilters(IReadOnlyList<string> Crops, IReadOnlyList<string> Provinces);

public record MarketplaceResponse(
    IReadOnlyList<MarketplaceListingDto> Items,
    MarketplaceTabCounts Counts,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    MarketplaceFilters Filters);