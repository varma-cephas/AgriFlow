using System.ComponentModel.DataAnnotations;

namespace backend.Models.Agent;

public sealed class CompleteVisitRequest
{
    [Required]
    public DateTime? VisitDate { get; set; }

    [Required, MinLength(1), MaxLength(2000)]
    public string Findings { get; set; } = string.Empty;
}

public sealed class CannotVerifyRequest
{
    [Required, MinLength(1), MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}
