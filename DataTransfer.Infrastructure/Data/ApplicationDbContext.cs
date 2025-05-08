using DataTransfer.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataTransfer.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<DataSource> DataSources { get; set; }
        public DbSet<UserLogin> UserLogins { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<DataSource>(entity =>
            {
                entity.ToTable("DataSource");
                entity.HasKey(e => e.DataSourceId);
                entity.Property(e => e.DataSourceId).HasColumnName("DataSourceId").UseIdentityColumn();
                entity.Property(e => e.ServerName).HasColumnName("ServerName");
                entity.Property(e => e.UserName).HasColumnName("UserName");
                entity.Property(e => e.Password).HasColumnName("Password");
                entity.Property(e => e.AuthenticationType).HasColumnName("Authentication");
                entity.Property(e => e.DefaultDatabaseName).HasColumnName("DefaultDatabaseName");
                entity.Property(e => e.UserId).HasColumnName("UserId");
                entity.Property(e => e.CreatedDate).HasColumnName("CreatedDate").HasDefaultValueSql("GETDATE()");
                entity.Ignore(e => e.IsActive);
            });

            modelBuilder.Entity<UserLogin>(entity =>
            {
                entity.ToTable("UserLogin");
                entity.HasKey(e => e.UserId);
                entity.Property(e => e.UserId).HasColumnName("UserId").UseIdentityColumn();
                entity.Property(e => e.Name).HasColumnName("Name");
                entity.Property(e => e.UserName).HasColumnName("UserName");
                entity.Property(e => e.Password).HasColumnName("Password");
                entity.Property(e => e.EmailAddress).HasColumnName("EmailAddress");
                entity.Property(e => e.CreatedDate).HasColumnName("CreatedDate").HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.LastLoginDate).HasColumnName("LastLoginDate");
            });
        }
    }
} 