using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Data.Entities;

[Table("order_item")]
public class OrderItem
{
    [Key]
    public int Id { get; set; }

    public int OrderId { get; set; }
    public int ListingId { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal QtyRequested { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal? QtyConfirmed { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal? QtyDispatched { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal? QtyAccepted { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal? QtyRejected { get; set; }

    [MaxLength(300)]
    public string? RejectReason { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal UnitPrice { get; set; }

    [Required, MaxLength(10)]
    public string Status { get; set; } = "Placed";

    [MaxLength(10)]
    public string? CancelType { get; set; }

    [MaxLength(100)]
    public string? VehicleInfo { get; set; }

    public DateTime? ReservedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? DispatchedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }

    public Order Order { get; set; } = null!;
    public Listing Listing { get; set; } = null!;
}