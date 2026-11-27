namespace backend.Services.Interfaces
{
    public interface INationalIdProtector
    {
        (byte[] Encrypted, byte[] Hash, string Masked) Protect(string rawNationalId);
        string Unprotect(byte[] encryptedNationalId);
    }
}
