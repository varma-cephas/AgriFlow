using backend.Data;
using backend.Data.Entities;
using backend.Models.Marketplace;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public class MarketplaceService(AppDbContext db) : IMarketplaceService
{
    private const int PageSize = 12;

    public async Task<(bool Success, string Message, MarketplaceResponse? Data)> BrowseAsync(
        MarketplaceQuery q, CancellationToken ct = default)
    {
        // ---------- Validate input ----------
        var tab = (q.Tab ?? "all").Trim().ToLowerInvariant();
        if (tab is not ("all" or "available" or "upcoming"))
            return (false, "tab must be all, available or upcoming.", null);

        var sortText = string.IsNullOrWhiteSpace(q.Sort) ? "soonest" : q.Sort.Trim();
        var sort = new[] { "soonest", "priceLow", "priceHigh" }
            .FirstOrDefault(s => s.Equals(sortText, StringComparison.OrdinalIgnoreCase));
        if (sort is null)
            return (false, "sort must be soonest, priceLow or priceHigh.", null);

        string? grade = null;
        if (!string.IsNullOrWhiteSpace(q.Grade))
        {
            grade = q.Grade.Trim().ToUpperInvariant();
            if (grade is not ("A" or "B" or "C"))
                return (false, "grade must be A, B or C.", null);
        }

        var page = Math.Clamp(q.Page, 1, 100000);

        // ---------- What a buyer may see: verified farms, never Hidden or SoldOut ----------
        var browsable = db.Listings.AsNoTracking()
            .Where(l => l.Farmer.VerificationStatus == "Verified")
            .Where(l => (l.Status == "Available" && l.AvailableQty > 0)
                     || (l.Status == "Upcoming" && (l.EstimatedQty ?? 0) - l.ReservedQty > 0));

        // ---------- Filters (everything except the tab) ----------
        var filtered = browsable;

        var crop = q.Crop?.Trim();
        if (!string.IsNullOrEmpty(crop))
            filtered = filtered.Where(l => l.Crop.Name == crop);

        var province = q.Province?.Trim();
        if (!string.IsNullOrEmpty(province))
            filtered = filtered.Where(l => l.Farmer.Province == province);

        if (grade is not null)
            filtered = filtered.Where(l => l.Grade == grade);

        var term = q.Search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            if (term.Length > 100) term = term[..100];
            var pattern = "%" + EscapeLike(term) + "%";
            filtered = filtered.Where(l =>
                EF.Functions.Like(l.Crop.Name, pattern, "\\") ||
                EF.Functions.Like(l.ListingCode, pattern, "\\") ||
                EF.Functions.Like(l.Farmer.FarmName, pattern, "\\") ||
                EF.Functions.Like(l.Farmer.District, pattern, "\\"));
        }

        // ---------- Counts per tab ----------
        var statusCounts = await filtered
            .GroupBy(l => l.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var availableCount = statusCounts.FirstOrDefault(x => x.Status == "Available")?.Count ?? 0;
        var upcomingCount = statusCounts.FirstOrDefault(x => x.Status == "Upcoming")?.Count ?? 0;
        var counts = new MarketplaceTabCounts(availableCount + upcomingCount, availableCount, upcomingCount);

        // ---------- Tab, sort, page ----------
        var tabbed = tab switch
        {
            "available" => filtered.Where(l => l.Status == "Available"),
            "upcoming" => filtered.Where(l => l.Status == "Upcoming"),
            _ => filtered
        };
        var totalItems = tab switch
        {
            "available" => availableCount,
            "upcoming" => upcomingCount,
            _ => counts.All
        };

        IOrderedQueryable<Listing> ordered = sort switch
        {
            "priceLow" => tabbed.OrderBy(l => l.PricePerUnit).ThenBy(l => l.Id),
            "priceHigh" => tabbed.OrderByDescending(l => l.PricePerUnit).ThenBy(l => l.Id),
            // soonest: Available now first, then upcoming harvests by nearest expected date
            _ => tabbed.OrderBy(l => l.Status == "Available" ? 0 : 1)
                       .ThenBy(l => l.Batch.PredictedHarvestDate)
                       .ThenBy(l => l.Id)
        };

        var items = await ordered
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(l => new MarketplaceListingDto(
                l.Id,
                l.ListingCode,
                l.Crop.Name,
                l.Grade,
                l.Farmer.FarmName,
                l.Farmer.District,
                l.Farmer.Province,
                l.Status,
                l.PricePerUnit,
                l.Unit,
                l.MinOrderQty,
                l.Status == "Available" ? l.AvailableQty : (l.EstimatedQty ?? 0) - l.ReservedQty,
                l.Status == "Available"
                    ? (l.Batch.HarvestDate ?? l.Batch.PredictedHarvestDate)
                    : l.Batch.PredictedHarvestDate))
            .ToListAsync(ct);

        // ---------- Dropdown values for the React filters ----------
        var crops = await browsable.Select(l => l.Crop.Name).Distinct().OrderBy(n => n).ToListAsync(ct);
        var provinces = await browsable.Select(l => l.Farmer.Province).Distinct().OrderBy(n => n).ToListAsync(ct);

        var totalPages = (int)Math.Ceiling(totalItems / (double)PageSize);

        return (true, "OK", new MarketplaceResponse(
            items, counts, page, PageSize, totalItems, totalPages,
            new MarketplaceFilters(crops, provinces)));
    }

    // Stops a search for "50%" or "a_b" from acting as a wildcard.
    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
}