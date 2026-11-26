namespace backend.Models.Auth
{
    public record AuthResponse(
        string Token,
        int UserId,
        string FullName,
        string Email,
        string Role,
        string VerificationStatus,
        int? BuyerBusinessId
    );
}
