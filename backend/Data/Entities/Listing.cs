using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Data.Entities;

[Table("listing")]
public class Listing
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(20)]
    public string ListingCode { get; set; } = string.Empty;

    public int BatchId { get; set; }
    public int FarmerId { get; set; }
    public int CropId { get; set; }

    [Required, MaxLength(1), Column(TypeName = "char(1)")]
    public string Grade { get; set; } = "B";

    [Required, MaxLength(20)]
    public string Unit { get; set; } = string.Empty;

    [Column(TypeName = "decimal(8,2)")]
    public decimal? UnitSizeKg { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal PricePerUnit { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal MinOrderQty { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal? EstimatedQty { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal ReservedQty { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal AvailableQty { get; set; }

    [Required, MaxLength(10)]
    public string Status { get; set; } = "Upcoming";

    public bool OpenForReservation { get; set; } = true;

    [MaxLength(10)]
    public string? HiddenBy { get; set; }

    [MaxLength(500)]
    public string? HiddenReason { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Concurrency token (ROWVERSION): stops two buyers taking the same last units (BR-9)
    [Timestamp]
    public byte[]? RowVer { get; set; }

    public PlantingBatch Batch { get; set; } = null!;
    public FarmerProfile Farmer { get; set; } = null!;
    public Crop Crop { get; set; } = null!;
}