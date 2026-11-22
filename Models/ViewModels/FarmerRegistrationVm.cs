using System.ComponentModel.DataAnnotations;

namespace AgriFlow.Models.ViewModels;

public class MustBeTrueAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is true;
}

public class FarmerRegisterVm
{
    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone number is required.")]
    [StringLength(20)]
    public string Phone { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(256)]
    public string? Email { get; set; }

    [Required(ErrorMessage = "National ID number is required.")]
    [RegularExpression(@"^(\d\s*){16}$", ErrorMessage = "National ID must be exactly 16 digits.")]
    public string NationalId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Farm name is required.")]
    [StringLength(150)]
    public string FarmName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Province is required.")]
    public string Province { get; set; } = string.Empty;

    [Required(ErrorMessage = "District is required.")]
    public string District { get; set; } = string.Empty;

    [Required(ErrorMessage = "Sector is required.")]
    public string Sector { get; set; } = string.Empty;

    [Required(ErrorMessage = "Cell is required.")]
    public string Cell { get; set; } = string.Empty;

    [Required(ErrorMessage = "Village is required.")]
    [StringLength(60)]
    public string Village { get; set; } = string.Empty;

    [Required(ErrorMessage = "Land UPI number is required.")]
    [StringLength(30)]
    public string LandUpi { get; set; } = string.Empty;

    [Range(0.01, 100000, ErrorMessage = "Farm size must be greater than 0.")]
    public decimal? FarmSizeHa { get; set; }

    [Required(ErrorMessage = "Please list your main crops.")]
    [StringLength(200)]
    public string MainCrops { get; set; } = string.Empty;

    [StringLength(150)]
    public string? Cooperative { get; set; }

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [MustBeTrue(ErrorMessage = "You must agree to the Terms and Privacy Policy.")]
    public bool AgreeToTerms { get; set; }
}