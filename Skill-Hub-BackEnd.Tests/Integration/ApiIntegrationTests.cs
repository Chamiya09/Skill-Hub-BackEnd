using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Skill_Hub_BackEnd.DTOs.Admin;
using Skill_Hub_BackEnd.DTOs.Candidates;
using Skill_Hub_BackEnd.DTOs.Jobs;
using Skill_Hub_BackEnd.Tests.Fixtures;
using Xunit;

namespace Skill_Hub_BackEnd.Tests.Integration
{
    public class ApiIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly HttpClient _client;
        private readonly CustomWebApplicationFactory _factory;

        public ApiIntegrationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task HealthCheck_ReturnsHealthyStatus200()
        {
            // Act
            var response = await _client.GetAsync("/health");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("Healthy", content);
            Assert.Contains("SkillHub.Backend", content);
        }

        [Fact]
        public async Task PublicJobs_AnonymousUser_ReturnsOk200WithJobs()
        {
            // Act
            var response = await _client.GetAsync("/api/public/jobs");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var jobs = await response.Content.ReadFromJsonAsync<List<JobResponseDto>>();
            Assert.NotNull(jobs);
            Assert.NotEmpty(jobs);
        }

        [Fact]
        public async Task ProtectedEndpoint_WithoutToken_ReturnsUnauthorized401()
        {
            // Act: Access protected dashboard stats without Authorization header
            var response = await _client.GetAsync("/api/dashboard/stats");

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task CandidateToken_AccessingAdminEndpoint_ReturnsForbidden403()
        {
            // Arrange: Generate Candidate token
            var candidateToken = AuthTokenTestHelper.GenerateTestToken(
                Guid.NewGuid(), "candidate@student.test", "CANDIDATE");

            var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/dashboard-stats");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", candidateToken);

            // Act: Attempt to access Admin-only route with Candidate token
            var response = await _client.SendAsync(request);

            // Assert: Role restriction enforced!
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task AdminLogin_ValidCredentials_AndAccessAdminStats_ReturnsOk200()
        {
            // Arrange: Admin login request
            var loginRequest = new AdminLoginRequestDto
            {
                Email = "admin@skillhub.internal",
                Password = "SkillHub@Admin2026"
            };

            // Act 1: Perform Admin Login
            var loginResponse = await _client.PostAsJsonAsync("/api/admin/auth/login", loginRequest);
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

            var authResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
            Assert.NotNull(authResult);
            Assert.NotNull(authResult.Token);
            Assert.Equal("Admin", authResult.User.Role);

            // Act 2: Access Admin Dashboard Stats with received token
            var statsRequest = new HttpRequestMessage(HttpMethod.Get, "/api/admin/dashboard-stats");
            statsRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResult.Token);

            var statsResponse = await _client.SendAsync(statsRequest);

            // Assert
            Assert.Equal(HttpStatusCode.OK, statsResponse.StatusCode);
            var stats = await statsResponse.Content.ReadFromJsonAsync<AdminDashboardStatsDto>();
            Assert.NotNull(stats);
            Assert.True(stats.SystemUptimePercent > 90);
        }

        [Fact]
        public async Task Candidate_Register_Login_AndGetProfile_CompleteFlow()
        {
            // Arrange: unique test email
            var uniqueEmail = $"integration.candidate.{Guid.NewGuid():N}@test.com";
            var password = "IntegrationPass@2026";

            var registerDto = new RegisterCandidateDto
            {
                FirstName = "Integration",
                LastName = "Tester",
                Email = uniqueEmail,
                Password = password,
                Headline = "Lead QA Engineer",
                Phone = "+15551234567"
            };

            // Step 1: Candidate Registration
            var regResponse = await _client.PostAsJsonAsync("/api/candidate/register", registerDto);
            Assert.Equal(HttpStatusCode.Created, regResponse.StatusCode);

            var regResult = await regResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
            Assert.NotNull(regResult);
            Assert.Equal("CANDIDATE", regResult.User.Role);

            // Step 2: Candidate Login
            var loginDto = new LoginDto { Email = uniqueEmail, Password = password };
            var loginResponse = await _client.PostAsJsonAsync("/api/candidate/login", loginDto);
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

            var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
            Assert.NotNull(loginResult);
            Assert.NotNull(loginResult.Token);

            // Step 3: Candidate /me Profile Retrieval
            var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/candidate/me");
            meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginResult.Token);

            var meResponse = await _client.SendAsync(meRequest);
            Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

            var profile = await meResponse.Content.ReadFromJsonAsync<UserResponseDto>();
            Assert.NotNull(profile);
            Assert.Equal(uniqueEmail, profile.Email);
            Assert.Equal("Integration Tester", profile.FullName);
        }

        [Fact]
        public async Task EndToEnd_CompanyCreatesJob_AndCandidateApplies_Flow()
        {
            // 1. Register a new employer company
            var compEmail = $"company.{Guid.NewGuid():N}@hiring.test";
            var compPass = "EnterprisePass@2026";

            var companyReg = new RegisterCompanyDto
            {
                CompanyName = "HyperScale AI Corp",
                CompanyEmail = compEmail,
                Password = compPass,
                Industry = "Artificial Intelligence",
                Website = "https://hyperscale.test"
            };

            var compRegResponse = await _client.PostAsJsonAsync("/api/company/register", companyReg);
            Assert.Equal(HttpStatusCode.Created, compRegResponse.StatusCode);

            var compAuth = await compRegResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
            Assert.NotNull(compAuth);
            var compToken = compAuth.Token;

            // 2. Company creates a job requisition
            var createJobDto = new CreateJobDto
            {
                Title = "Staff Distributed Systems Engineer",
                Department = "Infrastructure",
                Location = "San Francisco, CA",
                EmploymentType = "Full-time",
                ExperienceLevel = "Staff",
                SalaryRange = "$180,000 - $240,000",
                Description = "High-throughput real-time streaming engines."
            };

            var jobRequest = new HttpRequestMessage(HttpMethod.Post, "/api/jobs")
            {
                Content = JsonContent.Create(createJobDto)
            };
            jobRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", compToken);

            var jobResponse = await _client.SendAsync(jobRequest);
            Assert.Equal(HttpStatusCode.Created, jobResponse.StatusCode);

            var createdJob = await jobResponse.Content.ReadFromJsonAsync<JobResponseDto>();
            Assert.NotNull(createdJob);
            var jobId = createdJob.Id;

            // 3. Register a candidate
            var candEmail = $"applicant.{Guid.NewGuid():N}@candidate.test";
            var candPass = "CandidatePass@2026";
            var candReg = new RegisterCandidateDto
            {
                FirstName = "David",
                LastName = "Miller",
                Email = candEmail,
                Password = candPass
            };

            var candRegResponse = await _client.PostAsJsonAsync("/api/candidate/register", candReg);
            Assert.Equal(HttpStatusCode.Created, candRegResponse.StatusCode);

            var candAuth = await candRegResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
            Assert.NotNull(candAuth);
            var candToken = candAuth.Token;

            // 4. Candidate applies for the job vacancy
            var applyRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/jobs/{jobId}/apply")
            {
                Content = JsonContent.Create(new ApplyJobDto { CoverNote = "Experienced with distributed streaming architectures." })
            };
            applyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", candToken);

            var applyResponse = await _client.SendAsync(applyRequest);
            Assert.Equal(HttpStatusCode.Created, applyResponse.StatusCode);

            var appResult = await applyResponse.Content.ReadFromJsonAsync<CandidateApplicationResponseDto>();
            Assert.NotNull(appResult);
            Assert.Equal("Staff Distributed Systems Engineer", appResult.JobTitle);

            // 5. Company inspects the applicants pipeline for this job
            var viewApplicantsReq = new HttpRequestMessage(HttpMethod.Get, $"/api/jobs/{jobId}/applications");
            viewApplicantsReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", compToken);

            var viewApplicantsResp = await _client.SendAsync(viewApplicantsReq);
            Assert.Equal(HttpStatusCode.OK, viewApplicantsResp.StatusCode);

            var applicants = await viewApplicantsResp.Content.ReadFromJsonAsync<List<JobApplicantDto>>();
            Assert.NotNull(applicants);
            Assert.Single(applicants);
            Assert.Equal("David Miller", applicants[0].FullName);
        }
    }
}
