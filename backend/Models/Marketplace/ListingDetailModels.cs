namespace backend.Models.Marketplace;

public record FarmTrackRecordDto(
    int OrdersDelivered,
    decimal? OnTimePercent,     // null when the farm has no delivery history yet
    decimal? RejectedPercent);

public record ListingFarmDto(
    string FarmName,
    string FarmerName,
    string District,
    string Province,
    FarmTrackRecordDto TrackRecord);

public record ListingTraceabilityDto(
    string? SeedVariety,
    string? SeedSupplier,
    DateOnly PlantingDate,
    DateOnly ExpectedHarvestDate,
    DateOnly? HarvestDate,
    decimal? HarvestAmount,
    string? HarvestUnit,
    DateTime ListedAt);

public record ListingDetailDto(
    int Id,
    string Code,
    string Status,              // Available | Upcoming | SoldOut
    string Crop,
    string Grade,
    string? Description,
    decimal PricePerUnit,       // RWF
    string Unit,
    decimal? UnitSizeKg,
    decimal MinOrderQty,
    decimal AvailableQty,       // Available: stock now. Upcoming: estimated qty still unreserved. SoldOut: 0
    bool OpenForReservation,
    ListingTraceabilityDto Traceability,
    ListingFarmDto Farm);