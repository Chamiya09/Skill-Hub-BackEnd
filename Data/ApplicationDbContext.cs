using Microsoft.EntityFrameworkCore;
using Skill_Hub_BackEnd.Models;

namespace Skill_Hub_BackEnd.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Company> Companies => Set<Company>();
        public DbSet<User> Users => Set<User>();
        public DbSet<JobVacancy> JobVacancies => Set<JobVacancy>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Company Entity Configurations
            modelBuilder.Entity<Company>(entity =>
            {
                entity.ToTable("Companies", "public");
                entity.HasIndex(c => c.ContactEmail).IsUnique();
            });

            // User Entity Configurations
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users", "public");
                entity.HasIndex(u => u.Email).IsUnique();

                entity.HasOne(u => u.Company)
                      .WithMany(c => c.Users)
                      .HasForeignKey(u => u.CompanyId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // JobVacancy Entity Configurations
            modelBuilder.Entity<JobVacancy>(entity =>
            {
                entity.ToTable("JobVacancies", "public");
                entity.HasIndex(j => j.CompanyId);
                entity.HasIndex(j => j.Status);
                entity.HasIndex(j => j.CreatedAt);

                entity.HasOne(j => j.Company)
                      .WithMany(c => c.JobVacancies)
                      .HasForeignKey(j => j.CompanyId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
