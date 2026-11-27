using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Data.Entities;

[Table("planting_batch")]
public class PlantingBatch
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(20)]
    public string BatchCode { get; set; } = string.Empty;

    public int FarmerId { get; set; }
    public int SeedStockId { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal QuantityPlanted { get; set; }

    [Column(TypeName = "date")]
    public DateOnly PlantingDate { get; set; }

    [Column(TypeName = "date")]
    public DateOnly PredictedHarvestDate { get; set; }

    [MaxLength(60)]
    public string? FieldName { get; set; }

    [Column(TypeName = "decimal(8,2)")]
    public decimal? AreaHa { get; set; }

    [Required, MaxLength(10)]
    public string Status { get; set; } = "Active";

    [Column(TypeName = "decimal(12,2)")]
    public decimal? ActualYield { get; set; }

    [MaxLength(20)]
    public string? YieldUnit { get; set; }

    [Column(TypeName = "date")]
    public DateOnly? HarvestDate { get; set; }

    public int RecordedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}