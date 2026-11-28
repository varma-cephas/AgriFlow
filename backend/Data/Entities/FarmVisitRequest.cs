using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Data.Entities
{
    [Table("farm_visit_request")]
    public class FarmVisitRequest
    {
        [Key]
        public int Id { get; set; }

        public int FarmerId { get; set; }
        public int AgentId { get; set; }
        public int RequestedByUserId { get; set; }
        public DateOnly VisitBy { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }

        [Required, MaxLength(10)]
        public string Status { get; set; } = "Requested";

        public DateTime? CompletedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(FarmerId))]
        public FarmerProfile Farmer { get; set; } = null!;
    }
}
