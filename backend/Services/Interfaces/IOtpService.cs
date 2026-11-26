namespace backend.Services.Interfaces
{
    public interface IOtpService
    {
        Task GenerateAndSendAsync(string email, string purpose = "Verification", CancellationToken ct = default);
        Task<bool> VerifyAsync(string email, string code, string purpose = "Verification", CancellationToken ct = default);
    }
}
