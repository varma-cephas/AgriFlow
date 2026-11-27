using backend.Services.Interfaces;
using Resend;

namespace backend.Services
{
    public class ResendEmailService(
        IResend resend,
        IConfiguration config,
        IHostEnvironment env,
        ILogger<ResendEmailService> logger) : IEmailService
    {
        public async Task SendOtpEmailAsync(string toEmail, string code, string purpose, CancellationToken ct = default)
        {
            var apiKey = config["Resend:ApiKey"];

            // Local dev fallback: print OTP to console when no Resend API key is configured
            if (string.IsNullOrWhiteSpace(apiKey) && env.IsDevelopment())
            {
                logger.LogWarning(
                    "================ LOCAL DEV OTP ================\n" +
                    "To: {Email} | Purpose: {Purpose} | Code: {Code}\n" +
                    "===============================================",
                    toEmail, purpose, code);
                return;
            }

            var fromEmail = config["Resend:FromEmail"] ?? "AgriFlow <onboarding@resend.dev>";
            var subject = purpose switch
            {
                "Login" => $"Your AgriFlow sign-in code: {code}",
                "PasswordReset" => $"Reset your AgriFlow password: {code}",
                _ => $"Verify your AgriFlow account: {code}"
            };

            var htmlBody = $"""
            <div style="font-family: Arial, sans-serif; max-width: 480px; margin: 0 auto; padding: 24px; border: 1px solid #e5e7eb; border-radius: 8px;">
              <h2 style="color: #15803d; margin-top: 0;">AgriFlow Rwanda</h2>
              <p style="color: #374151; font-size: 15px;">Use the verification code below to complete your request:</p>
              <div style="background: #f0fdf4; border: 1px solid #bbf7d0; border-radius: 6px; padding: 16px; text-align: center; margin: 20px 0;">
                <span style="font-size: 28px; font-weight: bold; letter-spacing: 6px; color: #166534;">{code}</span>
              </div>
              <p style="color: #6b7280; font-size: 13px;">This code expires in <strong>5 minutes</strong>.</p>
            </div>
            """;

            var response = await resend.EmailSendAsync(new EmailMessage
            {
                From = fromEmail,
                To = toEmail,
                Subject = subject,
                HtmlBody = htmlBody
            }, ct);

            logger.LogInformation("Sent {Purpose} OTP email to {Email} (Resend ID: {MessageId})", purpose, toEmail, response.Content);
        }
    }
}


