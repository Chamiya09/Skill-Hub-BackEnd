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
        public DbSet<CandidateExperience> CandidateExperiences => Set<CandidateExperience>();
        public DbSet<CandidateEducation> CandidateEducations => Set<CandidateEducation>();
        public DbSet<CandidateProject> CandidateProjects => Set<CandidateProject>();
        public DbSet<CandidateSkill> CandidateSkills => Set<CandidateSkill>();

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
                      .IsRequired(false)
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

            // Candidate Experience Configurations
            modelBuilder.Entity<CandidateExperience>(entity =>
            {
                entity.ToTable("CandidateExperiences", "public");
                entity.HasIndex(e => e.UserId);
                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Candidate Education Configurations
            modelBuilder.Entity<CandidateEducation>(entity =>
            {
                entity.ToTable("CandidateEducations", "public");
                entity.HasIndex(e => e.UserId);
                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Candidate Project Configurations
            modelBuilder.Entity<CandidateProject>(entity =>
            {
                entity.ToTable("CandidateProjects", "public");
                entity.HasIndex(p => p.UserId);
                entity.HasOne(p => p.User)
                      .WithMany()
                      .HasForeignKey(p => p.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Candidate Skill Configurations
            modelBuilder.Entity<CandidateSkill>(entity =>
            {
                entity.ToTable("CandidateSkills", "public");
                entity.HasIndex(s => s.UserId);
                entity.HasOne(s => s.User)
                      .WithMany()
                      .HasForeignKey(s => s.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
