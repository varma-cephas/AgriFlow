using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Data.Entities
{
    [Table("agent_farmer_assignment")]
    public class AgentFarmerAssignment
    {
        [Key]
        public int Id { get; set; }

        public int AgentId { get; set; }
        public int FarmerId { get; set; }
        public int? AssignedByUserId { get; set; }
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ConsentRecordedAt { get; set; }

        [MaxLength(150)]
        public string? ConsentPlace { get; set; }

        [ForeignKey(nameof(AgentId))]
        public AppUser Agent { get; set; } = null!;

        [ForeignKey(nameof(FarmerId))]
        public FarmerProfile Farmer { get; set; } = null!;
    }
}
