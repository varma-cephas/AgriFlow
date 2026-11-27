using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Data.Entities
{
    [Table("audit_log")]
    public class AuditLog
    {
        [Key]
        public long Id { get; set; }

        public DateTime At { get; set; } = DateTime.UtcNow;

        public int ActorUserId { get; set; }

        public int? OnBehalfOfFarmerId { get; set; }

        [Required, MaxLength(40)]
        public string Entity { get; set; } = string.Empty;

        [MaxLength(40)]
        public string? EntityId { get; set; }

        [Required, MaxLength(60)]
        public string Action { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Summary { get; set; }

        [Required, MaxLength(10)]
        public string Result { get; set; } = "Success";
    }
}
