using backend.Data;
using backend.Data.Entities;
using backend.Services.Interfaces;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace backend.Services
{
    public class OtpService(AppDbContext db, IEmailService emailService, IConfiguration config) : IOtpService
    {
        private const int MaxAttempts = 5;
        private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);

        private readonly byte[] _hmacSecret = Encoding.UTF8.GetBytes(
            config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is missing from configuration."));

        public async Task GenerateAndSendAsync(string email, string purpose = "Verification", CancellationToken ct = default)
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();
            var now = DateTime.UtcNow;

            // 1. Invalidate any existing unused OTPs for this email & purpose
            var activeCodes = await db.OtpCodes
                .Where(o => o.Email == normalizedEmail && o.Purpose == purpose && !o.IsUsed)
                .ToListAsync(ct);

            foreach (var item in activeCodes)
                item.IsUsed = true;

            // 2. Periodically clean up expired codes older than 24 hours (keeps DB lightweight)
            var cutoff = now.AddHours(-24);
            var staleCodes = await db.OtpCodes
                .Where(o => o.ExpiresAtUtc < cutoff)
                .Take(50)
                .ToListAsync(ct);

            if (staleCodes.Count > 0)
                db.OtpCodes.RemoveRange(staleCodes);

            // 3. Generate cryptographically random 6-digit code
            var plainCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

            db.OtpCodes.Add(new OtpCode
            {
                Email = normalizedEmail,
                CodeHash = ComputeHash(normalizedEmail, purpose, plainCode),
                Purpose = purpose,
                ExpiresAtUtc = now.Add(CodeLifetime),
                IsUsed = false,
                Attempts = 0,
                CreatedAtUtc = now
            });

            await db.SaveChangesAsync(ct);

            // 4. Send via Resend
            await emailService.SendOtpEmailAsync(normalizedEmail, plainCode, purpose, ct);
        }

        public async Task<bool> VerifyAsync(string email, string code, string purpose = "Verification", CancellationToken ct = default)
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();
            var cleanCode = code.Trim();
            var now = DateTime.UtcNow;

            var record = await db.OtpCodes
                .Where(o => o.Email == normalizedEmail
                         && o.Purpose == purpose
                         && !o.IsUsed
                         && o.ExpiresAtUtc > now)
                .OrderByDescending(o => o.Id)
                .FirstOrDefaultAsync(ct);

            if (record is null || record.Attempts >= MaxAttempts)
                return false;

            record.Attempts++;

            var expectedHash = ComputeHash(normalizedEmail, purpose, cleanCode);
            var isMatch = CryptographicOperations.FixedTimeEquals(record.CodeHash, expectedHash);

            if (!isMatch)
            {
                if (record.Attempts >= MaxAttempts)
                    record.IsUsed = true; // Lock code after max failed attempts

                await db.SaveChangesAsync(ct);
                return false;
            }

            record.IsUsed = true;
            await db.SaveChangesAsync(ct);
            return true;
        }

        private byte[] ComputeHash(string email, string purpose, string code)
        {
            using var hmac = new HMACSHA256(_hmacSecret);
            return hmac.ComputeHash(Encoding.UTF8.GetBytes($"{email}:{purpose}:{code}"));
        }
    }
}
