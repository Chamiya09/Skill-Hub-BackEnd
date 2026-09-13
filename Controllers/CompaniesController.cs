using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Skill_Hub_BackEnd.Data;
using Skill_Hub_BackEnd.DTOs.Jobs;

namespace Skill_Hub_BackEnd.Controllers
{
    [ApiController]
    [Route("api/companies")]
    [AllowAnonymous]
    [Produces("application/json")]
    public class CompaniesController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<CompaniesController> _logger;

        public CompaniesController(ApplicationDbContext dbContext, ILogger<CompaniesController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves the public details of a company by unique ID or Name, along with its active job vacancies.
        /// Endpoint: GET /api/companies/{idOrName}
        /// </summary>
        [HttpGet("{idOrName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetCompanyByIdOrName(string idOrName)
        {
            var decoded = System.Net.WebUtility.UrlDecode(idOrName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(decoded))
            {
                return NotFound(new { message = "Company identifier cannot be empty." });
            }

            Skill_Hub_BackEnd.Models.Company? company = null;

            if (Guid.TryParse(decoded, out var companyGuid))
            {
                company = await _dbContext.Companies.FindAsync(companyGuid);
            }

            if (company == null)
            {
                var lowerTerm = decoded.ToLower();
                var unslugged = lowerTerm.Replace("-", " ");
                company = await _dbContext.Companies
                    .FirstOrDefaultAsync(c =>
                        c.CompanyName.ToLower() == lowerTerm ||
                        c.CompanyName.ToLower() == unslugged ||
                        c.ContactEmail.ToLower() == lowerTerm);
            }

            if (company == null)
            {
                _logger.LogWarning("Public company profile not found for identifier: '{Identifier}'", decoded);
                return NotFound(new { message = $"Company with identifier '{decoded}' was not found." });
            }

            var jobs = await _dbContext.JobVacancies
                .Where(j => j.CompanyId == company.Id && j.Status == "Active")
                .OrderByDescending(j => j.CreatedAt)
                .Select(j => new JobResponseDto
                {
                    Id = j.Id,
                    CompanyId = j.CompanyId,
                    CompanyName = company.CompanyName,
                    Title = j.Title,
                    Department = j.Department,
                    Location = j.Location,
                    EmploymentType = j.EmploymentType,
                    ExperienceLevel = j.ExperienceLevel,
                    SalaryRange = j.SalaryRange,
                    Status = j.Status,
                    Description = j.Description,
                    WhatWeOffer = j.WhatWeOffer,
                    CreatedAt = j.CreatedAt,
                    UpdatedAt = j.UpdatedAt
                })
                .ToListAsync();

            return Ok(new
            {
                company = new
                {
                    id = company.Id,
                    companyName = company.CompanyName,
                    adminName = company.AdminName ?? company.CompanyName,
                    contactEmail = company.ContactEmail,
                    phone = company.Phone,
                    companySize = company.CompanySize,
                    foundedYear = company.FoundedYear,
                    logoUrl = company.LogoUrl,
                    website = company.Website,
                    linkedinUrl = company.LinkedinUrl,
                    twitterUrl = company.TwitterUrl,
                    githubUrl = company.GithubUrl,
                    location = company.Location,
                    industry = company.Industry,
                    about = company.About,
                    createdAt = company.CreatedAt,
                    updatedAt = company.UpdatedAt
                },
                jobs = jobs
            });
        }
    }
}
