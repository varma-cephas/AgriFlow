using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Data.Entities
{
    [Table("app_user")]
    public class AppUser
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(150)]
        public string FullName { get; set; } = string.Empty;

        [MaxLength(256)]
        public string? Email { get; set; }

        [MaxLength(20)]
        public string? Phone { get; set; }

        [MaxLength(500)]
        public string? PasswordHash { get; set; }

        [Required, MaxLength(10)]
        public string Role { get; set; } = "Farmer";

        public bool IsActive { get; set; } = true;

        [MaxLength(300)]
        public string? SuspendedReason { get; set; }

        public int? BuyerBusinessId { get; set; }

        [MaxLength(100)]
        public string? JobTitle { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastLoginAt { get; set; }

        // Navigation properties
        [ForeignKey(nameof(BuyerBusinessId))]
        public BuyerBusiness? BuyerBusiness { get; set; }

        public FarmerProfile? FarmerProfile { get; set; }
    }
}
