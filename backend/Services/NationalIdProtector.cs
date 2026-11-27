using backend.Services.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;
using System.Text;

namespace backend.Services
{
    public class NationalIdProtector(IDataProtectionProvider provider, IConfiguration config) : INationalIdProtector
    {
        private readonly IDataProtector _protector = provider.CreateProtector("AgriFlow.NationalId.v1");
        private readonly byte[] _hmacKey = Encoding.UTF8.GetBytes(
            config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is missing from configuration."));

        public (byte[] Encrypted, byte[] Hash, string Masked) Protect(string rawNationalId)
        {
            var digits = new string(rawNationalId.Where(char.IsDigit).ToArray());
            if (digits.Length != 16)
                throw new ArgumentException("Rwanda National ID must contain exactly 16 digits.");

            var plainBytes = Encoding.UTF8.GetBytes(digits);
            var encrypted = _protector.Protect(plainBytes);

            using var hmac = new HMACSHA256(_hmacKey);
            var hash = hmac.ComputeHash(plainBytes);

            // Admin-safe format: 1 1985 8 ••••••• 31
            var masked = $"{digits[0]} {digits[1..5]} {digits[5]} ••••••• {digits[14..16]}";

            return (encrypted, hash, masked);
        }

        public string Unprotect(byte[] encryptedNationalId)
        {
            var plainBytes = _protector.Unprotect(encryptedNationalId);
            return Encoding.UTF8.GetString(plainBytes);
        }
    }
}
