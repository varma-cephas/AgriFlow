namespace AgriFlow.Security;

public static class PasswordHasher
{
    public static string Hash(string plainPassword) =>
        BCrypt.Net.BCrypt.HashPassword(plainPassword, workFactor: 11);

    public static bool Verify(string plainPassword, string storedHash) =>
        BCrypt.Net.BCrypt.Verify(plainPassword, storedHash);
}