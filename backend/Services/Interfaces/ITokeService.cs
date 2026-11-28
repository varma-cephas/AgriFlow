using backend.Data.Entities;

namespace backend.Services.Interfaces
{
    public interface ITokenService
    {
        string GenerateToken(AppUser user, string verificationStatus);
    }
}
