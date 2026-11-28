using System.ComponentModel.DataAnnotations;

namespace backend.Models.Auth
{
    public class VerifyOtpRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, RegularExpression(@"^\d{6}$", ErrorMessage = "OTP code must be 6 digits.")]
        public string Code { get; set; } = string.Empty;

        [RegularExpression("^(Verification|Login)$")]
        public string Purpose { get; set; } = "Verification";
    }
}
