using backend.Models.Auth;

namespace backend.Services.Interfaces
{
    public interface IAuthService
    {
        Task<(bool Success, string Message)> RegisterFarmerAsync(RegisterFarmerRequest req, CancellationToken ct = default);
        Task<(bool Success, string Message)> RegisterBuyerAsync(RegisterBuyerRequest req, CancellationToken ct = default);
        Task<(bool Success, string Message, AuthResponse? Data)> LoginAsync(LoginRequest req, CancellationToken ct = default);
        Task<(bool Success, string Message, AuthResponse? Data)> VerifyOtpAndSignInAsync(VerifyOtpRequest req, CancellationToken ct = default);
        Task<object?> GetCurrentUserProfileAsync(int userId, CancellationToken ct = default);
    }
}
