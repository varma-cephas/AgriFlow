using backend.Data;
using backend.Models.Marketplace;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public class ListingDetailService(AppDbContext db) : IListingDetailService
{
    public async Task<ListingDetailDto?> GetAsync(int listingId, CancellationToken ct = default)
    {
        // Verified farms only; Hidden listings look like they do not exist.
        var row = await db.Listings.AsNoTracking()
            .Where(l => l.Id == listingId
                     && l.Farmer.VerificationStatus == "Verified"
                     && l.Status != "Hidden")
            .Select(l => new
            {
                l.Id,
                l.ListingCode,
                l.Status,
                CropName = l.Crop.Name,
                l.Grade,
                l.Description,
                l.PricePerUnit,
                l.Unit,
                l.UnitSizeKg,
                l.MinOrderQty,
                l.AvailableQty,
                l.EstimatedQty,
                l.ReservedQty,
                l.OpenForReservation,
                l.CreatedAt,
                l.FarmerId,
                l.Batch.SeedStockId,
                l.Batch.PlantingDate,
                l.Batch.PredictedHarvestDate,
                l.Batch.HarvestDate,
                l.Batch.ActualYield,
                l.Batch.YieldUnit,
                l.Farmer.FarmName,
                FarmerName = l.Farmer.User.FullName,
                l.Farmer.District,
                l.Farmer.Province
            })
            .FirstOrDefaultAsync(ct);

        if (row is null) return null;

        var seed = await db.SeedStocks.AsNoTracking()
            .Where(s => s.Id == row.SeedStockId)
            .Select(s => new { s.Variety, s.Supplier })
            .FirstOrDefaultAsync(ct);

        // Track record, from this farm's delivered order lines
        var delivered = await db.OrderItems.AsNoTracking()
            .Where(i => i.Listing.FarmerId == row.FarmerId && i.Status == "Delivered")
            .Select(i => new { i.Order.DeliveryDate, i.DeliveredAt, i.QtyAccepted, i.QtyRejected })
            .ToListAsync(ct);

        var withDates = delivered.Where(d => d.DeliveryDate.HasValue && d.DeliveredAt.HasValue).ToList();
        decimal? onTime = withDates.Count == 0
            ? null
            : Math.Round(100m * withDates.Count(d => DateOnly.FromDateTime(d.DeliveredAt!.Value) <= d.DeliveryDate!.Value)
                         / withDates.Count, 1);

        var accepted = delivered.Sum(d => d.QtyAccepted ?? 0);
        var rejected = delivered.Sum(d => d.QtyRejected ?? 0);
        decimal? rejectedPct = accepted + rejected == 0 ? null : Math.Round(100m * rejected / (accepted + rejected), 1);

        var qty = row.Status switch
        {
            "Available" => row.AvailableQty,
            "Upcoming" => Math.Max(0, (row.EstimatedQty ?? 0) - row.ReservedQty),
            _ => 0m
        };

        return new ListingDetailDto(
            row.Id, row.ListingCode, row.Status, row.CropName, row.Grade, row.Description,
            row.PricePerUnit, row.Unit, row.UnitSizeKg, row.MinOrderQty, qty, row.OpenForReservation,
            new ListingTraceabilityDto(
                seed?.Variety, seed?.Supplier, row.PlantingDate, row.PredictedHarvestDate,
                row.HarvestDate, row.ActualYield, row.YieldUnit, row.CreatedAt),
            new ListingFarmDto(
                row.FarmName, row.FarmerName, row.District, row.Province,
                new FarmTrackRecordDto(delivered.Count, onTime, rejectedPct)));
    }
}