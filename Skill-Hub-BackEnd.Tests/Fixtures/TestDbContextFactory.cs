using System;
using Microsoft.EntityFrameworkCore;
using Skill_Hub_BackEnd.Data;
using Skill_Hub_BackEnd.Models.Admin;
using Skill_Hub_BackEnd.Models.Candidates;
using Skill_Hub_BackEnd.Models.Jobs;
using Skill_Hub_BackEnd.Models.Assessments;
using Skill_Hub_BackEnd.Models.Interviews;

namespace Skill_Hub_BackEnd.Tests.Fixtures
{
    public static class TestDbContextFactory
    {
        public static ApplicationDbContext CreateInMemoryDbContext(string? databaseName = null)
        {
            var dbName = databaseName ?? $"SkillHub_TestDb_{Guid.NewGuid()}";
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .EnableSensitiveDataLogging()
                .Options;

            var context = new ApplicationDbContext(options);
            context.Database.EnsureCreated();

            var admin = System.Linq.Enumerable.FirstOrDefault(context.Users, u => u.Email == "admin@skillhub.internal");
            if (admin != null)
            {
                admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword("SkillHub@Admin2026");
                context.SaveChanges();
            }

            return context;
        }

        public static void SeedTestData(ApplicationDbContext context, out Company company, out User candidate, out JobVacancy job)
        {
            company = new Company
            {
                Id = Guid.NewGuid(),
                CompanyName = "Acme Global Tech",
                ContactEmail = "careers@acmeglobal.test",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("TestPass@123"),
                Industry = "Enterprise Cloud Software",
                Location = "San Francisco, CA",
                Website = "https://acmeglobal.test",
                CompanySize = "Enterprise",
                IsSuspended = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Companies.Add(company);

            candidate = new User
            {
                Id = Guid.NewGuid(),
                FirstName = "Jane",
                LastName = "Doe",
                FullName = "Jane Doe",
                Email = "jane.doe@candidate.test",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("CandidatePass@123"),
                Role = "CANDIDATE",
                Headline = "Senior Full-Stack Engineer",
                Location = "Remote, USA",
                IsSuspended = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Users.Add(candidate);

            job = new JobVacancy
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Title = "Senior C# .NET Engineer",
                Department = "Engineering",
                Location = "Remote",
                EmploymentType = "Full-time",
                ExperienceLevel = "Senior",
                SalaryRange = "$120,000 - $160,000",
                Status = "Active",
                Description = "Seeking an experienced .NET architect to build scalable microservices.",
                WhatWeOffer = "Full health coverage, flexible hours, remote budget.",
                Deadline = DateTime.UtcNow.AddMonths(1),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.JobVacancies.Add(job);

            context.SaveChanges();
        }
    }
}
