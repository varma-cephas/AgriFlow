namespace backend.Models.Auth
{
    public class RegisterBuyerRequest
    {
        [Required, MaxLength(150)]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress, MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Phone { get; set; } = string.Empty;

        [Required, MinLength(8), MaxLength(100)]
        public string Password { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? JobTitle { get; set; }

        [Required, MaxLength(150)]
        public string BusinessName { get; set; } = string.Empty;

        [Required, RegularExpression("^(Supermarket|Hotel|Restaurant|School|Hospital|Processor|Exporter|Other)$",
            ErrorMessage = "Invalid business type.")]
        public string BusinessType { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string RdbCompanyCode { get; set; } = string.Empty;

        [Required, MaxLength(250)]
        public string Address { get; set; } = string.Empty;

        [Required, MaxLength(80)]
        public string City { get; set; } = string.Empty;
    }
}
