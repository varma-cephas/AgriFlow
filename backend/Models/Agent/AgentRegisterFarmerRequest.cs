using System.ComponentModel.DataAnnotations;

namespace backend.Models.Agent
{
    public record RegisterNonDigitalFarmerRequest(
        [Required, MaxLength(120)] string FullName,
        [MaxLength(30)] string? Phone,
        [Required, MaxLength(60)] string Village,
        [Required, MaxLength(20)] string PreferredLanguage,
        [Required, MaxLength(150)] string FarmName,
        [Required, MaxLength(40)] string Province,
        [Required, MaxLength(60)] string District,
        decimal? FarmSizeAcres,
        [Required] bool ConsentConfirmed
    );

    public record RegisterNonDigitalFarmerResponse(
        int FarmerId,
        string FullName,
        string FirstName,
        string FarmName,
        string Message
    );
}
