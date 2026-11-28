using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Data.Entities;

[Table("agent_farmer_assignment")]
public class AgentFarmerAssignment
{
    [Key] public int Id { get; set; }
    public int AgentId { get; set; }
    public int FarmerId { get; set; }
    public int? AssignedByUserId { get; set; }
    public DateTime AssignedAt { get; set; }
    public DateTime? ConsentRecordedAt { get; set; }
    [MaxLength(150)] public string? ConsentPlace { get; set; }
}

[Table("farm_visit_request")]
public class FarmVisitRequest
{
    [Key] public int Id { get; set; }
    public int FarmerId { get; set; }
    public int AgentId { get; set; }
    public int RequestedByUserId { get; set; }
    public DateTime VisitBy { get; set; }
    [MaxLength(500)] public string? Note { get; set; }
    [Required, MaxLength(10)] public string Status { get; set; } = "Requested";
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

[Table("crop")]
public class Crop
{
    [Key] public int Id { get; set; }
    [Required, MaxLength(60)] public string Name { get; set; } = string.Empty;
    public int MaturityDays { get; set; }
}

[Table("seed_stock")]
public class SeedStock
{
    [Key] public int Id { get; set; }
    public int FarmerId { get; set; }
    public int CropId { get; set; }
    [Required, MaxLength(80)] public string Variety { get; set; } = string.Empty;
    [MaxLength(120)] public string? Supplier { get; set; }
    public decimal Quantity { get; set; }
    [Required, MaxLength(10)] public string Unit { get; set; } = "kg";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    [Timestamp] public byte[]? RowVer { get; set; }
}

[Table("planting_batch")]
public class PlantingBatch
{
    [Key] public int Id { get; set; }
    [Required, MaxLength(20)] public string BatchCode { get; set; } = string.Empty;
    public int FarmerId { get; set; }
    public int SeedStockId { get; set; }
    public decimal QuantityPlanted { get; set; }
    public DateTime PlantingDate { get; set; }
    public DateTime PredictedHarvestDate { get; set; }
    [MaxLength(60)] public string? FieldName { get; set; }
    public decimal? AreaHa { get; set; }
    [Required, MaxLength(10)] public string Status { get; set; } = "Active";
    public decimal? ActualYield { get; set; }
    [MaxLength(20)] public string? YieldUnit { get; set; }
    public DateTime? HarvestDate { get; set; }
    public int RecordedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

[Table("listing")]
public class Listing
{
    [Key] public int Id { get; set; }
    [Required, MaxLength(20)] public string ListingCode { get; set; } = string.Empty;
    public int BatchId { get; set; }
    public int FarmerId { get; set; }
    public int CropId { get; set; }
    [Required, MaxLength(1)] public string Grade { get; set; } = string.Empty;
    [Required, MaxLength(20)] public string Unit { get; set; } = string.Empty;
    public decimal? UnitSizeKg { get; set; }
    public decimal PricePerUnit { get; set; }
    public decimal MinOrderQty { get; set; }
    public decimal? EstimatedQty { get; set; }
    public decimal ReservedQty { get; set; }
    public decimal AvailableQty { get; set; }
    [Required, MaxLength(10)] public string Status { get; set; } = string.Empty;
    public bool OpenForReservation { get; set; }
    [MaxLength(10)] public string? HiddenBy { get; set; }
    [MaxLength(500)] public string? HiddenReason { get; set; }
    [MaxLength(1000)] public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    [Timestamp] public byte[]? RowVer { get; set; }
}

[Table("orders")]
public class FarmOrder
{
    [Key] public int Id { get; set; }
    [Required, MaxLength(20)] public string OrderNumber { get; set; } = string.Empty;
    public int BuyerBusinessId { get; set; }
    public int PlacedByUserId { get; set; }
    [Required, MaxLength(250)] public string DeliveryAddress { get; set; } = string.Empty;
    public DateTime? DeliveryDate { get; set; }
    [Required, MaxLength(12)] public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int? CancelledByUserId { get; set; }
    [MaxLength(500)] public string? CancelReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

[Table("order_item")]
public class FarmOrderItem
{
    [Key] public int Id { get; set; }
    public int OrderId { get; set; }
    public int ListingId { get; set; }
    public decimal QtyRequested { get; set; }
    public decimal? QtyConfirmed { get; set; }
    public decimal? QtyDispatched { get; set; }
    public decimal? QtyAccepted { get; set; }
    public decimal? QtyRejected { get; set; }
    [MaxLength(300)] public string? RejectReason { get; set; }
    public decimal UnitPrice { get; set; }
    [Required, MaxLength(10)] public string Status { get; set; } = string.Empty;
    [MaxLength(10)] public string? CancelType { get; set; }
    [MaxLength(100)] public string? VehicleInfo { get; set; }
    public DateTime? ReservedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? DispatchedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
}
