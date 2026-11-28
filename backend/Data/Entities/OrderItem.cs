using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Data.Entities
{
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
        public decimal UnitPrice { get; set; }

        [Required, MaxLength(10)]
        public string Status { get; set; } = "Placed";

        public DateTime? ReservedAt { get; set; }

        [ForeignKey(nameof(OrderId))]
        public Order Order { get; set; } = null!;

        [ForeignKey(nameof(ListingId))]
        public Listing Listing { get; set; } = null!;
    }
}
