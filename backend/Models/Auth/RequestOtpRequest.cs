using System.ComponentModel.DataAnnotations;

namespace backend.Models.Auth
{
    public class RequestOtpRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [RegularExpression("^(Verification|Login|PasswordReset)$")]
        public string Purpose { get; set; } = "Verification";
    }
}
