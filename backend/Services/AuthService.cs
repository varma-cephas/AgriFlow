using backend.Data;
using backend.Data.Entities;
using backend.Models.Auth;
using backend.Services.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace backend.Services
{
    public class AuthService(
        AppDbContext db,
        INationalIdProtector nidProtector,
        IOtpService otpService,
        ITokenService tokenService) : IAuthService
    {
        public async Task<(bool Success, string Message)> RegisterFarmerAsync(RegisterFarmerRequest req, CancellationToken ct = default)
        {
            var email = req.Email.Trim().ToLowerInvariant();
            var cleanUpi = req.LandUpi.Trim();

            if (await db.Users.AnyAsync(u => u.Email == email, ct))
                return (false, "An account with this email already exists.");

            var (encrypted, hash, masked) = nidProtector.Protect(req.NationalId);

            if (await db.FarmerProfiles.AnyAsync(f => f.NationalIdHash == hash, ct))
                return (false, "This National ID is already registered to another farmer.");

            if (await db.FarmerProfiles.AnyAsync(f => f.LandUpi == cleanUpi, ct))
                return (false, "This Land UPI is already registered.");

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var user = new AppUser
                {
                    FullName = req.FullName.Trim(),
                    Email = email,
                    Phone = req.Phone.Trim(),
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
                    Role = "Farmer",
                    IsActive = true
                };

                db.Users.Add(user);
                await db.SaveChangesAsync(ct);

                db.FarmerProfiles.Add(new FarmerProfile
                {
                    UserId = user.Id,
                    FarmName = req.FarmName.Trim(),
                    NationalIdEncrypted = encrypted,
                    NationalIdHash = hash,
                    NationalIdMasked = masked,
                    LandUpi = cleanUpi,
                    Province = req.Province.Trim(),
                    District = req.District.Trim(),
                    Sector = req.Sector.Trim(),
                    Cell = req.Cell.Trim(),
                    Village = req.Village.Trim(),
                    MainCrops = req.MainCrops.Trim(),
                    FarmSizeHa = req.FarmSizeHa,
                    CooperativeName = req.CooperativeName?.Trim(),
                    CooperativeMemberNo = req.CooperativeMemberNo?.Trim(),
                    PreferredLanguage = req.PreferredLanguage.Trim(),
                    VerificationStatus = "Unverified"
                });

                db.AuditLogs.Add(new AuditLog
                {
                    ActorUserId = user.Id,
                    Entity = "FarmerProfile",
                    EntityId = user.Id.ToString(),
                    Action = "Registered farmer account",
                    Summary = $"{req.FarmName.Trim()} ({req.District.Trim()}, {req.Sector.Trim()})",
                    Result = "Success"
                });

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                await tx.RollbackAsync(ct);
                return (false, "Duplicate email, National ID, or Land UPI detected.");
            }

            await otpService.GenerateAndSendAsync(email, "Verification", ct);
            return (true, "Registration successful. Check your email for the 6-digit verification code.");
        }

        public async Task<(bool Success, string Message)> RegisterBuyerAsync(RegisterBuyerRequest req, CancellationToken ct = default)
        {
            var email = req.Email.Trim().ToLowerInvariant();
            var cleanRdb = req.RdbCompanyCode.Trim().ToUpperInvariant();

            if (await db.Users.AnyAsync(u => u.Email == email, ct))
                return (false, "An account with this email already exists.");

            if (await db.BuyerBusinesses.AnyAsync(b => b.RdbCompanyCode == cleanRdb, ct))
                return (false, "This RDB Company Code is already registered.");

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var business = new BuyerBusiness
                {
                    Name = req.BusinessName.Trim(),
                    BusinessType = req.BusinessType,
                    RdbCompanyCode = cleanRdb,
                    Address = req.Address.Trim(),
                    City = req.City.Trim(),
                    Phone = req.Phone.Trim(),
                    VerificationStatus = "Pending"
                };

                db.BuyerBusinesses.Add(business);
                await db.SaveChangesAsync(ct);

                var user = new AppUser
                {
                    FullName = req.FullName.Trim(),
                    Email = email,
                    Phone = req.Phone.Trim(),
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
                    Role = "Buyer",
                    IsActive = true,
                    BuyerBusinessId = business.Id,
                    JobTitle = req.JobTitle?.Trim()
                };

                db.Users.Add(user);
                await db.SaveChangesAsync(ct);

                db.AuditLogs.Add(new AuditLog
                {
                    ActorUserId = user.Id,
                    Entity = "BuyerBusiness",
                    EntityId = business.Id.ToString(),
                    Action = "Registered buyer business",
                    Summary = $"{business.Name} ({business.BusinessType} · RDB {business.RdbCompanyCode})",
                    Result = "Success"
                });

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                await tx.RollbackAsync(ct);
                return (false, "Duplicate email or RDB Company Code detected.");
            }

            await otpService.GenerateAndSendAsync(email, "Verification", ct);
            return (true, "Registration successful. Check your email for the 6-digit verification code.");
        }

        public async Task<(bool Success, string Message, AuthResponse? Data)> LoginAsync(LoginRequest req, CancellationToken ct = default)
        {
            var email = req.Email.Trim().ToLowerInvariant();
            var user = await db.Users
                .Include(u => u.FarmerProfile)
                .Include(u => u.BuyerBusiness)
                .FirstOrDefaultAsync(u => u.Email == email, ct);

            if (user is null || string.IsNullOrEmpty(user.PasswordHash) ||
                !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            {
                return (false, "Invalid email or password.", null);
            }

            if (!user.IsActive)
                return (false, $"Account suspended: {user.SuspendedReason ?? "Contact support."}", null);

            user.LastLoginAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            var status = ResolveVerificationStatus(user);
            var token = tokenService.GenerateToken(user, status);

            return (true, "Signed in.", new AuthResponse(
                token, user.Id, user.FullName, user.Email!, user.Role, status, user.BuyerBusinessId));
        }

        public async Task<(bool Success, string Message, AuthResponse? Data)> VerifyOtpAndSignInAsync(VerifyOtpRequest req, CancellationToken ct = default)
        {
            var isValid = await otpService.VerifyAsync(req.Email, req.Code, req.Purpose, ct);
            if (!isValid)
                return (false, "Invalid, expired, or locked OTP code.", null);

            var email = req.Email.Trim().ToLowerInvariant();
            var user = await db.Users
                .Include(u => u.FarmerProfile)
                .Include(u => u.BuyerBusiness)
                .FirstOrDefaultAsync(u => u.Email == email, ct);

            if (user is null)
                return (false, "User account not found.", null);

            if (!user.IsActive)
                return (false, $"Account suspended: {user.SuspendedReason ?? "Contact support."}", null);

            user.LastLoginAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            var status = ResolveVerificationStatus(user);
            var token = tokenService.GenerateToken(user, status);

            return (true, "Verification successful.", new AuthResponse(
                token, user.Id, user.FullName, user.Email!, user.Role, status, user.BuyerBusinessId));
        }

        public async Task<object?> GetCurrentUserProfileAsync(int userId, CancellationToken ct = default)
        {
            var user = await db.Users
                .AsNoTracking()
                .Include(u => u.FarmerProfile)
                .Include(u => u.BuyerBusiness)
                .FirstOrDefaultAsync(u => u.Id == userId, ct);

            if (user is null) return null;

            return new
            {
                user.Id,
                user.FullName,
                user.Email,
                user.Phone,
                user.Role,
                user.IsActive,
                user.JobTitle,
                VerificationStatus = ResolveVerificationStatus(user),
                FarmerProfile = user.FarmerProfile is null ? null : new
                {
                    user.FarmerProfile.FarmName,
                    user.FarmerProfile.LandUpi,
                    user.FarmerProfile.Province,
                    user.FarmerProfile.District,
                    user.FarmerProfile.Sector,
                    user.FarmerProfile.Cell,
                    user.FarmerProfile.Village,
                    user.FarmerProfile.MainCrops,
                    user.FarmerProfile.FarmSizeHa,
                    user.FarmerProfile.VerificationStatus,
                    user.FarmerProfile.RejectionReason
                },
                BuyerBusiness = user.BuyerBusiness is null ? null : new
                {
                    user.BuyerBusiness.Id,
                    user.BuyerBusiness.Name,
                    user.BuyerBusiness.BusinessType,
                    user.BuyerBusiness.RdbCompanyCode,
                    user.BuyerBusiness.City,
                    user.BuyerBusiness.Address,
                    user.BuyerBusiness.VerificationStatus,
                    user.BuyerBusiness.RejectionReason
                }
            };
        }

        private static string ResolveVerificationStatus(AppUser user) => user.Role switch
        {
            "Farmer" => user.FarmerProfile?.VerificationStatus ?? "Unverified",
            "Buyer" => user.BuyerBusiness?.VerificationStatus ?? "Pending",
            _ => "Verified"
        };

        private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
            ex.InnerException is SqlException { Number: 2601 or 2627 };
    }
}
