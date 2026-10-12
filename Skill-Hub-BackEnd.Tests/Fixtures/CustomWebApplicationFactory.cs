using System;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Skill_Hub_BackEnd.Data;
using Skill_Hub_BackEnd.Models.Admin;
using Skill_Hub_BackEnd.Models.Candidates;
using Skill_Hub_BackEnd.Models.Jobs;

namespace Skill_Hub_BackEnd.Tests.Fixtures
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"SkillHubIntegration_{Guid.NewGuid()}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((context, conf) =>
            {
                conf.AddInMemoryCollection(new System.Collections.Generic.Dictionary<string, string?>
                {
                    ["JwtSettings:SecretKey"] = AuthTokenTestHelper.DefaultSecretKey,
                    ["JwtSettings:Issuer"] = AuthTokenTestHelper.DefaultIssuer,
                    ["JwtSettings:Audience"] = AuthTokenTestHelper.DefaultAudience,
                    ["JwtSettings:ExpirationInHours"] = "24"
                });
            });

            builder.ConfigureServices(services =>
            {
                // Remove production EF Core DbContext registration
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Add InMemory database for isolated integration testing
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase(_databaseName);
                });

                // Build service provider and seed predictable integration test data
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var scopedServices = scope.ServiceProvider;
                var db = scopedServices.GetRequiredService<ApplicationDbContext>();
                var logger = scopedServices.GetRequiredService<ILogger<CustomWebApplicationFactory>>();

                try
                {
                    db.Database.EnsureCreated();
                    SeedIntegrationData(db);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred seeding the test database with test messages. Error: {Message}", ex.Message);
                }
            });
        }

        private static void SeedIntegrationData(ApplicationDbContext db)
        {
            // Seed Super Admin if not present or update password hash
            var existingAdmin = db.Users.FirstOrDefault(u => u.Email == "admin@skillhub.internal");
            if (existingAdmin != null)
            {
                existingAdmin.PasswordHash = BCrypt.Net.BCrypt.HashPassword("SkillHub@Admin2026");
            }
            else
            {
                db.Users.Add(new User
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                    FullName = "Super Administrator",
                    Email = "admin@skillhub.internal",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("SkillHub@Admin2026"),
                    Role = "Admin",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            // Seed Test Company
            var companyId = Guid.Parse("22222222-2222-2222-2222-222222222222");
            if (!db.Companies.Any(c => c.Id == companyId))
            {
                var company = new Company
                {
                    Id = companyId,
                    CompanyName = "Integration Test Corp",
                    ContactEmail = "contact@integrationcorp.test",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("CompanyPass@2026"),
                    Industry = "Cloud Technology",
                    Location = "San Francisco, CA",
                    Website = "https://integrationcorp.test",
                    CompanySize = "Enterprise",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                db.Companies.Add(company);

                // Seed Active Job for public endpoints
                db.JobVacancies.Add(new JobVacancy
                {
                    Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                    CompanyId = companyId,
                    Title = "Full-Stack Software Engineer",
                    Department = "Engineering",
                    Location = "San Francisco, CA",
                    EmploymentType = "Full-time",
                    ExperienceLevel = "Mid-Senior",
                    SalaryRange = "$130,000 - $170,000",
                    Status = "Active",
                    Description = "Core platform engineering with React and .NET Core.",
                    WhatWeOffer = "Competitive salary, 401k match, health benefits.",
                    Deadline = DateTime.UtcNow.AddMonths(2),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            db.SaveChanges();
        }
    }
}
