using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Data.Entities
{
    [Table("orders")]
    public class Order
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(20)]
        public string OrderNumber { get; set; } = string.Empty;

        public int BuyerBusinessId { get; set; }
        public int PlacedByUserId { get; set; }

        [Required, MaxLength(250)]
        public string DeliveryAddress { get; set; } = string.Empty;

        public DateOnly? DeliveryDate { get; set; }

        [Required, MaxLength(12)]
        public string Status { get; set; } = "Placed";

        [Column(TypeName = "decimal(14,2)")]
        public decimal TotalAmount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(BuyerBusinessId))]
        public BuyerBusiness BuyerBusiness { get; set; } = null!;
    }
}
