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
        public DbSet<CandidateCertification> CandidateCertifications => Set<CandidateCertification>();
        public DbSet<JobApplication> JobApplications => Set<JobApplication>();
        public DbSet<SavedJob> SavedJobs => Set<SavedJob>();
        public DbSet<Assessment> Assessments => Set<Assessment>();
        public DbSet<Submission> Submissions => Set<Submission>();
        public DbSet<CvEvaluationResult> CvEvaluationResults => Set<CvEvaluationResult>();
        public DbSet<InterviewPrepGuide> InterviewPrepGuides => Set<InterviewPrepGuide>();
        public DbSet<Event> Events => Set<Event>();

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

            // JobApplication Entity Configurations
            modelBuilder.Entity<JobApplication>(entity =>
            {
                entity.ToTable("JobApplications", "public");
                entity.HasIndex(a => new { a.JobId, a.CandidateId }).IsUnique();
                entity.HasIndex(a => a.CandidateId);
                entity.HasIndex(a => a.JobId);
                entity.HasIndex(a => a.AppliedDate);

                entity.HasOne(a => a.Job)
                      .WithMany()
                      .HasForeignKey(a => a.JobId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(a => a.Candidate)
                      .WithMany()
                      .HasForeignKey(a => a.CandidateId)
                      .OnDelete(DeleteBehavior.Cascade);
            });


            modelBuilder.Entity<SavedJob>(entity =>
            {
                entity.ToTable("SavedJobs", "public");
                entity.HasIndex(saved => new { saved.CandidateId, saved.JobId }).IsUnique();
                entity.HasIndex(saved => saved.JobId);

                entity.HasOne(saved => saved.Candidate)
                      .WithMany()
                      .HasForeignKey(saved => saved.CandidateId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(saved => saved.Job)
                      .WithMany()
                      .HasForeignKey(saved => saved.JobId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Candidate Experience Configurations
            modelBuilder.Entity<CandidateExperience>(entity =>
            {
                entity.ToTable("CandidateExperiences", "public");
                entity.HasIndex(e => e.UserId);
                entity.HasOne(e => e.User)
                      .WithMany(user => user.Experiences)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Candidate Education Configurations
            modelBuilder.Entity<CandidateEducation>(entity =>
            {
                entity.ToTable("CandidateEducations", "public");
                entity.HasIndex(e => e.UserId);
                entity.HasOne(e => e.User)
                      .WithMany(user => user.Educations)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Candidate Project Configurations
            modelBuilder.Entity<CandidateProject>(entity =>
            {
                entity.ToTable("CandidateProjects", "public");
                entity.HasIndex(p => p.UserId);
                entity.HasOne(p => p.User)
                      .WithMany(user => user.Projects)
                      .HasForeignKey(p => p.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Candidate Skill Configurations
            modelBuilder.Entity<CandidateSkill>(entity =>
            {
                entity.ToTable("CandidateSkills", "public");
                entity.HasIndex(s => s.UserId);
                entity.HasOne(s => s.User)
                      .WithMany(user => user.Skills)
                      .HasForeignKey(s => s.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Candidate Certification Configurations
            modelBuilder.Entity<CandidateCertification>(entity =>
            {
                entity.ToTable("CandidateCertifications", "public");
                entity.HasIndex(c => c.UserId);
                entity.HasOne(c => c.User)
                      .WithMany(user => user.Certifications)
                      .HasForeignKey(c => c.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Assessment Entity Configurations
            modelBuilder.Entity<Assessment>(entity =>
            {
                entity.ToTable("Assessments", "public");
                entity.HasIndex(a => a.JobVacancyId);
                entity.HasIndex(a => a.CreatedBy);
                entity.HasIndex(a => a.Status);

                entity.Property(a => a.GeneratedQuestions).HasColumnType("jsonb");
                entity.Property(a => a.FinalQuestions).HasColumnType("jsonb");
                entity.Property(a => a.PassingThreshold).HasPrecision(5, 2);
            });

            // Submission Entity Configurations
            modelBuilder.Entity<Submission>(entity =>
            {
                entity.ToTable("Submissions", "public");
                entity.HasIndex(s => s.AssessmentId);
                entity.HasIndex(s => s.CandidateId);
                entity.HasIndex(s => s.ApplicationId);
                entity.HasIndex(s => s.JobVacancyId);
                entity.HasIndex(s => s.Status);

                entity.Property(s => s.Answers).HasColumnType("jsonb");
                entity.Property(s => s.ProctorFlags).HasColumnType("jsonb");
                entity.Property(s => s.ExamScore).HasPrecision(5, 2);
                entity.Property(s => s.CvScore).HasPrecision(5, 2);
                entity.Property(s => s.FinalWeightedScore).HasPrecision(5, 2);
                entity.Property(s => s.IsSelectedForInterview).HasDefaultValue(false);
                entity.Property(s => s.ReviewerFeedback);
                entity.Property(s => s.ReviewedBy);

                entity.HasOne(s => s.Assessment)
                      .WithMany(a => a.Submissions)
                      .HasForeignKey(s => s.AssessmentId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // CvEvaluationResult Entity Configurations
            modelBuilder.Entity<CvEvaluationResult>(entity =>
            {
                entity.ToTable("CvEvaluationResults", "public");

                // Index for the cache lookup: latest evaluation for a candidate/job pair
                entity.HasIndex(r => new { r.CandidateId, r.JobId, r.CreatedAt })
                      .HasDatabaseName("IX_CvEvaluationResults_Candidate_Job_Date");

                entity.HasIndex(r => r.ApprovalStatus)
                      .HasDatabaseName("IX_CvEvaluationResults_ApprovalStatus");

                // Store structured AI outputs as JSONB for efficient Postgres querying
                entity.Property(r => r.StrengthsJson).HasColumnType("jsonb");
                entity.Property(r => r.MissingSkillsJson).HasColumnType("jsonb");
                entity.Property(r => r.ExtractedDataJson).HasColumnType("jsonb");
                entity.Property(r => r.ValidationNotesJson).HasColumnType("jsonb");

                entity.Property(r => r.ApprovalStatus)
                      .HasMaxLength(20)
                      .HasDefaultValue("Pending");

                // Foreign key: Candidate (User)
                entity.HasOne(r => r.Candidate)
                      .WithMany()
                      .HasForeignKey(r => r.CandidateId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Foreign key: JobVacancy
                entity.HasOne(r => r.Job)
                      .WithMany()
                      .HasForeignKey(r => r.JobId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // InterviewPrepGuide Entity Configurations (Student 1)
            modelBuilder.Entity<InterviewPrepGuide>(entity =>
            {
                entity.ToTable("InterviewPrepGuides", "public");
                entity.HasIndex(g => new { g.CandidateId, g.CreatedAt });
                entity.HasIndex(g => g.JobId);

                entity.Property(g => g.TechnicalQuestionsJson).HasColumnType("jsonb");
                entity.Property(g => g.BehavioralQuestionsJson).HasColumnType("jsonb");
                entity.Property(g => g.ProTipsJson).HasColumnType("jsonb");
                entity.Property(g => g.ChecklistJson).HasColumnType("jsonb");

                entity.HasOne(g => g.Candidate)
                      .WithMany()
                      .HasForeignKey(g => g.CandidateId)
                      .IsRequired(false)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(g => g.JobApplication)
                      .WithMany()
                      .HasForeignKey(g => g.ApplicationId)
                      .IsRequired(false)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(g => g.Job)
                      .WithMany()
                      .HasForeignKey(g => g.JobId)
                      .IsRequired(false)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // Event Entity Configurations
            modelBuilder.Entity<Event>(entity =>
            {
                entity.ToTable("Events", "public");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).HasMaxLength(255).IsRequired();
                entity.Property(e => e.EventTime).HasMaxLength(50).IsRequired();

                entity.HasIndex(e => e.EventDate);
                entity.HasIndex(e => e.CreatedBy);
                entity.HasIndex(e => e.CompanyId);

                entity.HasOne(e => e.Company)
                      .WithMany()
                      .HasForeignKey(e => e.CompanyId)
                      .IsRequired(false)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
