using System.ComponentModel.DataAnnotations;

namespace AgriFlow.Models.ViewModels;

public class BusinessRegisterVm
{
    [Required(ErrorMessage = "Business name is required.")]
    [StringLength(150)]
    public string BusinessName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select a business type.")]
    public string BusinessType { get; set; } = string.Empty;

    [Required(ErrorMessage = "RDB company code is required.")]
    [StringLength(20)]
    public string RdbCompanyCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "City is required.")]
    public string City { get; set; } = string.Empty;

    [Required(ErrorMessage = "Business address is required.")]
    [StringLength(250)]
    public string Address { get; set; } = string.Empty;

    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(150)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(100)]
    public string? JobTitle { get; set; } // Optional: "Role in business"

    [Required(ErrorMessage = "Work email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid work email address.")]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone number is required.")]
    [StringLength(20)]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}