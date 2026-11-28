using System.ComponentModel.DataAnnotations;

namespace backend.Models.Auth
{
    public class RegisterFarmerRequest
    {
        [Required, MaxLength(150)]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress, MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Phone { get; set; } = string.Empty;

        [Required, MinLength(8), MaxLength(100)]
        public string Password { get; set; } = string.Empty;

        [Required, MaxLength(150)]
        public string FarmName { get; set; } = string.Empty;

        [Required, RegularExpression(@"^\d{16}$", ErrorMessage = "National ID must be exactly 16 digits.")]
        public string NationalId { get; set; } = string.Empty;

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

        [Range(0.01, 100000)]
        public decimal? FarmSizeHa { get; set; }

        [MaxLength(150)]
        public string? CooperativeName { get; set; }

        [MaxLength(50)]
        public string? CooperativeMemberNo { get; set; }

        [MaxLength(20)]
        public string PreferredLanguage { get; set; } = "Kinyarwanda";
    }
}
