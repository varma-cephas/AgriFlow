using System.Data;
using AgriFlow.Models.ViewModels;
using AgriFlow.Security;
using Microsoft.Data.SqlClient;

namespace AgriFlow.Data;

public record AuthResult(bool Success, int UserId = 0, string FullName = "", string Role = "", int? BuyerBusinessId = null, string? ErrorField = null, string? ErrorMessage = null);

public record PendingFarmerInfo(string FarmName, string FullName, string LandUpi, string District, string Status, DateTime CreatedAt);

public record PendingBusinessInfo(string BusinessName, string FullName, string RdbCompanyCode, string Email, string Status, DateTime CreatedAt);

public class AuthRepository
{
    private readonly DbConnectionFactory _db;
    private readonly NationalIdProtector _nidProtector;

    public AuthRepository(DbConnectionFactory db, NationalIdProtector nidProtector)
    {
        _db = db;
        _nidProtector = nidProtector;
    }

    public async Task<AuthResult> RegisterFarmerAsync(FarmerRegisterVm vm)
    {
        var (nidEncrypted, nidHash, nidMasked) = _nidProtector.Protect(vm.NationalId);
        var passwordHash = PasswordHasher.Hash(vm.Password);

        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();

        try
        {
            // 1. Insert into app_user
            const string insertUserSql = @"
                INSERT INTO app_user (FullName, Email, Phone, PasswordHash, Role, IsActive)
                OUTPUT INSERTED.Id
                VALUES (@FullName, @Email, @Phone, @PasswordHash, 'Farmer', 1);";

            int userId;
            await using (var cmd = new SqlCommand(insertUserSql, conn, tx))
            {
                cmd.Parameters.AddWithValue("@FullName", vm.FullName.Trim());
                cmd.Parameters.AddWithValue("@Email", string.IsNullOrWhiteSpace(vm.Email) ? DBNull.Value : vm.Email.Trim().ToLowerInvariant());
                cmd.Parameters.AddWithValue("@Phone", vm.Phone.Trim());
                cmd.Parameters.AddWithValue("@PasswordHash", passwordHash);
                userId = (int)(await cmd.ExecuteScalarAsync())!;
            }

            // 2. Insert into farmer_profile
            const string insertProfileSql = @"
                INSERT INTO farmer_profile (
                    UserId, FarmName, NationalIdEncrypted, NationalIdHash, NationalIdMasked,
                    LandUpi, Province, District, Sector, Cell, Village,
                    MainCrops, FarmSizeHa, CooperativeName, VerificationStatus
                )
                VALUES (
                    @UserId, @FarmName, @NidEnc, @NidHash, @NidMasked,
                    @LandUpi, @Province, @District, @Sector, @Cell, @Village,
                    @MainCrops, @FarmSizeHa, @CooperativeName, 'Unverified'
                );";

            await using (var cmd = new SqlCommand(insertProfileSql, conn, tx))
            {
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@FarmName", vm.FarmName.Trim());
                cmd.Parameters.Add("@NidEnc", SqlDbType.VarBinary, 512).Value = nidEncrypted;
                cmd.Parameters.Add("@NidHash", SqlDbType.VarBinary, 32).Value = nidHash;
                cmd.Parameters.AddWithValue("@NidMasked", nidMasked);
                cmd.Parameters.AddWithValue("@LandUpi", vm.LandUpi.Trim());
                cmd.Parameters.AddWithValue("@Province", vm.Province.Trim());
                cmd.Parameters.AddWithValue("@District", vm.District.Trim());
                cmd.Parameters.AddWithValue("@Sector", vm.Sector.Trim());
                cmd.Parameters.AddWithValue("@Cell", vm.Cell.Trim());
                cmd.Parameters.AddWithValue("@Village", vm.Village.Trim());
                cmd.Parameters.AddWithValue("@MainCrops", vm.MainCrops.Trim());
                cmd.Parameters.AddWithValue("@FarmSizeHa", vm.FarmSizeHa.HasValue ? vm.FarmSizeHa.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@CooperativeName", string.IsNullOrWhiteSpace(vm.Cooperative) ? DBNull.Value : vm.Cooperative.Trim());
                await cmd.ExecuteNonQueryAsync();
            }

            // 3. Write audit log
            await InsertAuditAsync(conn, tx, userId, "FarmerProfile", userId.ToString(), "Self-registered farmer account", $"{vm.FarmName} ({vm.District})");

            await tx.CommitAsync();
            return new AuthResult(true, userId, vm.FullName.Trim(), "Farmer");
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            await tx.RollbackAsync();
            return MapDuplicateKeyError(ex);
        }
    }

    public async Task<AuthResult> RegisterBusinessAsync(BusinessRegisterVm vm)
    {
        var passwordHash = PasswordHasher.Hash(vm.Password);

        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();

        try
        {
            // 1. Insert into buyer_business
            const string insertBizSql = @"
                INSERT INTO buyer_business (Name, BusinessType, RdbCompanyCode, Address, City, Phone, VerificationStatus)
                OUTPUT INSERTED.Id
                VALUES (@Name, @BusinessType, @RdbCode, @Address, @City, @Phone, 'Pending');";

            int businessId;
            await using (var cmd = new SqlCommand(insertBizSql, conn, tx))
            {
                cmd.Parameters.AddWithValue("@Name", vm.BusinessName.Trim());
                cmd.Parameters.AddWithValue("@BusinessType", vm.BusinessType.Trim());
                cmd.Parameters.AddWithValue("@RdbCode", vm.RdbCompanyCode.Trim());
                cmd.Parameters.AddWithValue("@Address", vm.Address.Trim());
                cmd.Parameters.AddWithValue("@City", vm.City.Trim());
                cmd.Parameters.AddWithValue("@Phone", vm.Phone.Trim());
                businessId = (int)(await cmd.ExecuteScalarAsync())!;
            }

            // 2. Insert into app_user linked to buyer_business
            const string insertUserSql = @"
                INSERT INTO app_user (FullName, Email, Phone, PasswordHash, Role, IsActive, BuyerBusinessId, JobTitle)
                OUTPUT INSERTED.Id
                VALUES (@FullName, @Email, @Phone, @PasswordHash, 'Buyer', 1, @BuyerBusinessId, @JobTitle);";

            int userId;
            await using (var cmd = new SqlCommand(insertUserSql, conn, tx))
            {
                cmd.Parameters.AddWithValue("@FullName", vm.FullName.Trim());
                cmd.Parameters.AddWithValue("@Email", vm.Email.Trim().ToLowerInvariant());
                cmd.Parameters.AddWithValue("@Phone", vm.Phone.Trim());
                cmd.Parameters.AddWithValue("@PasswordHash", passwordHash);
                cmd.Parameters.AddWithValue("@BuyerBusinessId", businessId);
                cmd.Parameters.AddWithValue("@JobTitle", string.IsNullOrWhiteSpace(vm.JobTitle) ? DBNull.Value : vm.JobTitle.Trim());
                userId = (int)(await cmd.ExecuteScalarAsync())!;
            }

            // 3. Write audit log
            await InsertAuditAsync(conn, tx, userId, "BuyerBusiness", businessId.ToString(), "Registered buyer business", $"{vm.BusinessName} (RDB: {vm.RdbCompanyCode})");

            await tx.CommitAsync();
            return new AuthResult(true, userId, vm.FullName.Trim(), "Buyer", businessId);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            await tx.RollbackAsync();
            return MapDuplicateKeyError(ex);
        }
    }

    public async Task<AuthResult> AuthenticateAsync(string email, string password)
    {
        const string sql = @"
            SELECT Id, FullName, PasswordHash, Role, IsActive, SuspendedReason, BuyerBusinessId
            FROM app_user
            WHERE Email = @Email AND PasswordHash IS NOT NULL;";

        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        int id;
        string fullName, hash, role;
        bool isActive;
        string? suspendedReason;
        int? buyerBusinessId;

        await using (var cmd = new SqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("@Email", email.Trim().ToLowerInvariant());
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return new AuthResult(false, ErrorMessage: "Invalid email address or password.");
            }

            id = reader.GetInt32(0);
            fullName = reader.GetString(1);
            hash = reader.GetString(2);
            role = reader.GetString(3);
            isActive = reader.GetBoolean(4);
            suspendedReason = reader.IsDBNull(5) ? null : reader.GetString(5);
            buyerBusinessId = reader.IsDBNull(6) ? null : reader.GetInt32(6);
        }

        if (!PasswordHasher.Verify(password, hash))
        {
            return new AuthResult(false, ErrorMessage: "Invalid email address or password.");
        }

        if (!isActive)
        {
            var reason = string.IsNullOrWhiteSpace(suspendedReason) ? "Contact an AgriFlow administrator." : suspendedReason;
            return new AuthResult(false, ErrorMessage: $"Your account has been suspended. {reason}");
        }

        await using (var updateCmd = new SqlCommand("UPDATE app_user SET LastLoginAt = SYSUTCDATETIME() WHERE Id = @Id;", conn))
        {
            updateCmd.Parameters.AddWithValue("@Id", id);
            await updateCmd.ExecuteNonQueryAsync();
        }

        return new AuthResult(true, id, fullName, role, buyerBusinessId);
    }

    public async Task<PendingFarmerInfo?> GetPendingFarmerAsync(int userId)
    {
        const string sql = @"
            SELECT fp.FarmName, u.FullName, fp.LandUpi, fp.District, fp.VerificationStatus, fp.CreatedAt
            FROM farmer_profile fp
            INNER JOIN app_user u ON u.Id = fp.UserId
            WHERE fp.UserId = @UserId;";

        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);

        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        return new PendingFarmerInfo(
            r.GetString(0), r.GetString(1), r.GetString(2),
            r.GetString(3), r.GetString(4), r.GetDateTime(5)
        );
    }

    public async Task<PendingBusinessInfo?> GetPendingBusinessAsync(int userId)
    {
        const string sql = @"
            SELECT bb.Name, u.FullName, bb.RdbCompanyCode, ISNULL(u.Email, ''), bb.VerificationStatus, bb.CreatedAt
            FROM app_user u
            INNER JOIN buyer_business bb ON bb.Id = u.BuyerBusinessId
            WHERE u.Id = @UserId;";

        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);

        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        return new PendingBusinessInfo(
            r.GetString(0), r.GetString(1), r.GetString(2),
            r.GetString(3), r.GetString(4), r.GetDateTime(5)
        );
    }

    private static async Task InsertAuditAsync(SqlConnection conn, SqlTransaction tx, int actorId, string entity, string entityId, string action, string summary)
    {
        const string sql = @"
            INSERT INTO audit_log (ActorUserId, Entity, EntityId, Action, Summary, Result)
            VALUES (@ActorId, @Entity, @EntityId, @Action, @Summary, 'Success');";

        await using var cmd = new SqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("@ActorId", actorId);
        cmd.Parameters.AddWithValue("@Entity", entity);
        cmd.Parameters.AddWithValue("@EntityId", entityId);
        cmd.Parameters.AddWithValue("@Action", action);
        cmd.Parameters.AddWithValue("@Summary", summary);
        await cmd.ExecuteNonQueryAsync();
    }

    private static AuthResult MapDuplicateKeyError(SqlException ex)
    {
        if (ex.Message.Contains("UX_user_email", StringComparison.OrdinalIgnoreCase))
            return new AuthResult(false, ErrorField: "Input.Email", ErrorMessage: "An account with this email already exists.");

        if (ex.Message.Contains("UX_fp_nid", StringComparison.OrdinalIgnoreCase))
            return new AuthResult(false, ErrorField: "Input.NationalId", ErrorMessage: "This National ID is already registered on AgriFlow.");

        if (ex.Message.Contains("UX_fp_upi", StringComparison.OrdinalIgnoreCase))
            return new AuthResult(false, ErrorField: "Input.LandUpi", ErrorMessage: "This Land UPI number is already registered.");

        if (ex.Message.Contains("UX_bb_rdb", StringComparison.OrdinalIgnoreCase))
            return new AuthResult(false, ErrorField: "Input.RdbCompanyCode", ErrorMessage: "A business with this RDB company code is already registered.");

        return new AuthResult(false, ErrorMessage: "A record with these details already exists.");
    }
}