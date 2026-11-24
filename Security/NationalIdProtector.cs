using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace AgriFlow.Security;

public class NationalIdProtector
{
    private readonly IDataProtector _protector;
    private readonly byte[] _hmacKey;

    public NationalIdProtector(IDataProtectionProvider provider, IConfiguration config)
    {
        _protector = provider.CreateProtector("AgriFlow.NationalId.v1");
        var secret = config["Security:NationalIdHmacKey"]
            ?? throw new InvalidOperationException("Security:NationalIdHmacKey is missing from appsettings.json.");
        _hmacKey = Encoding.UTF8.GetBytes(secret);
    }

    public (byte[] Encrypted, byte[] Hash, string Masked) Protect(string raw16Digits)
    {
        var digits = new string(raw16Digits.Where(char.IsDigit).ToArray());
        if (digits.Length != 16)
            throw new ArgumentException("National ID must be 16 digits.", nameof(raw16Digits));

        // 1. Encrypt bytes (VARBINARY(512))
        var plainBytes = Encoding.UTF8.GetBytes(digits);
        var encrypted = _protector.Protect(plainBytes);

        // 2. Deterministic HMAC-SHA256 hash for SQL UNIQUE check (VARBINARY(32))
        using var hmac = new HMACSHA256(_hmacKey);
        var hash = hmac.ComputeHash(plainBytes);

        // 3. Masked display for Admin view: "1 1985 8 ••••••• 31"
        var masked = $"{digits[0]} {digits.Substring(1, 4)} {digits[5]} ••••••• {digits.Substring(14, 2)}";

        return (encrypted, hash, masked);
    }
}