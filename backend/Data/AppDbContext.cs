using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<AppUser> Users => Set<AppUser>();
        public DbSet<BuyerBusiness> BuyerBusinesses => Set<BuyerBusiness>();
        public DbSet<FarmerProfile> FarmerProfiles => Set<FarmerProfile>();
        public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AppUser>(entity =>
            {
                entity.HasIndex(u => u.Email)
                      .IsUnique()
                      .HasFilter("[Email] IS NOT NULL");

                entity.HasOne(u => u.BuyerBusiness)
                      .WithMany(b => b.Users)
                      .HasForeignKey(u => u.BuyerBusinessId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<BuyerBusiness>(entity =>
            {
                entity.HasIndex(b => b.RdbCompanyCode).IsUnique();
            });

            modelBuilder.Entity<FarmerProfile>(entity =>
            {
                entity.HasIndex(f => f.NationalIdHash).IsUnique();
                entity.HasIndex(f => f.LandUpi).IsUnique();

                entity.HasOne(f => f.User)
                      .WithOne(u => u.FarmerProfile)
                      .HasForeignKey<FarmerProfile>(f => f.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<OtpCode>(entity =>
            {
                entity.HasIndex(o => new { o.Email, o.IsUsed, o.ExpiresAtUtc });
            });
        }
    }
}
