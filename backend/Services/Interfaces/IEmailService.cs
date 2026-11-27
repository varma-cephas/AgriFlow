namespace backend.Services.Interfaces
{
    public interface IEmailService
    {
        Task SendOtpEmailAsync(string toEmail, string code, string purpose, CancellationToken ct = default);
    }
}
