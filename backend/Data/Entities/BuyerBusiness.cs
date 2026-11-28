using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Data.Entities
{
    [Table("buyer_business")]
    public class BuyerBusiness
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(30)]
        public string BusinessType { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string RdbCompanyCode { get; set; } = string.Empty;

        [Required, MaxLength(250)]
        public string Address { get; set; } = string.Empty;

        [Required, MaxLength(80)]
        public string City { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? Phone { get; set; }

        [Required, MaxLength(10)]
        public string VerificationStatus { get; set; } = "Pending";

        public int? VerifiedByUserId { get; set; }
        public DateTime? VerifiedAt { get; set; }

        [MaxLength(500)]
        public string? RejectionReason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<AppUser> Users { get; set; } = [];
    }
}
