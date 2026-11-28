using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Data.Entities
{
    [Table("otp_code")]
    public class OtpCode
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required, MaxLength(32)]
        public byte[] CodeHash { get; set; } = [];

        [Required, MaxLength(20)]
        public string Purpose { get; set; } = "Verification";

        public DateTime ExpiresAtUtc { get; set; }

        public bool IsUsed { get; set; }

        public int Attempts { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
