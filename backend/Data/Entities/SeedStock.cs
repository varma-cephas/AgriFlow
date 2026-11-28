using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Data.Entities;

[Table("seed_stock")]
public class SeedStock
{
    [Key]
    public int Id { get; set; }

    public int FarmerId { get; set; }
    public int CropId { get; set; }

    [Required, MaxLength(80)]
    public string Variety { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? Supplier { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal Quantity { get; set; }

    [Required, MaxLength(10)]
    public string Unit { get; set; } = "kg";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Timestamp]
    public byte[]? RowVer { get; set; }
}