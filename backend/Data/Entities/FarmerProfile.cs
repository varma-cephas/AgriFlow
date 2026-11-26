using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Data.Entities
{
    [Table("farmer_profile")]
    public class FarmerProfile
    {
        [Key, ForeignKey(nameof(User))]
        public int UserId { get; set; }

        [Required, MaxLength(150)]
        public string FarmName { get; set; } = string.Empty;

        [Required, MaxLength(512)]
        public byte[] NationalIdEncrypted { get; set; } = [];

        [Required, MaxLength(32)]
        public byte[] NationalIdHash { get; set; } = [];

        [Required, MaxLength(30)]
        public string NationalIdMasked { get; set; } = string.Empty;

        [Required, MaxLength(30)]
        public string LandUpi { get; set; } = string.Empty;

        [Required, MaxLength(40)]
        public string Province { get; set; } = string.Empty;

        [Required, MaxLength(60)]
        public string District { get; set; } = string.Empty;

        [Required, MaxLength(60)]
        public string Sector { get; set; } = string.Empty;

        [Required, MaxLength(60)]
        public string Cell { get; set; } = string.Empty;

        [Required, MaxLength(60)]
        public string Village { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string MainCrops { get; set; } = string.Empty;

        [Column(TypeName = "decimal(8,2)")]
        public decimal? FarmSizeHa { get; set; }

        [MaxLength(150)]
        public string? CooperativeName { get; set; }

        [MaxLength(50)]
        public string? CooperativeMemberNo { get; set; }

        [Required, MaxLength(20)]
        public string PreferredLanguage { get; set; } = "Kinyarwanda";

        [Required, MaxLength(12)]
        public string VerificationStatus { get; set; } = "Unverified";

        public int? VerifiedByUserId { get; set; }
        public DateTime? VerifiedAt { get; set; }

        [MaxLength(500)]
        public string? RejectionReason { get; set; }

        public int? RegisteredByAgentId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public AppUser User { get; set; } = null!;
    }
}
