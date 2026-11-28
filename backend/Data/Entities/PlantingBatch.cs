using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Data.Entities
{
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

        public DateOnly PlantingDate { get; set; }
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

        public DateOnly? HarvestDate { get; set; }
        public int RecordedByUserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(SeedStockId))]
        public SeedStock SeedStock { get; set; } = null!;
    }
}
