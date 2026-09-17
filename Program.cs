using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Skill_Hub_BackEnd.Data;
using Skill_Hub_BackEnd.Services.Implementations;
using Skill_Hub_BackEnd.Services.Interfaces;

// ==========================================
// 0. NETWORK CONFIGURATION (FORCE IPV4 FOR CLOUD DBs)
// ==========================================
// Prevents unroutable ISP IPv6 socket timeouts when resolving Neon AWS endpoints on Windows
AppContext.SetSwitch("System.Net.DisableIPv6", true);

var builder = WebApplication.CreateBuilder(args);

// Avoid the Windows Event Log provider in local/dev hosting. It requires an
// elevated, pre-registered event source and can otherwise crash the process
// while attempting to report an unrelated startup error.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Local development convenience: the Python worker already owns the repository's
// untracked .env file. ASP.NET does not load dotenv files automatically, so import
// only the two Groq settings when they were not supplied by user-secrets, process
// environment variables, or deployment configuration. Never commit the .env file.
if (builder.Environment.IsDevelopment() &&
    string.IsNullOrWhiteSpace(builder.Configuration["Groq:ApiKey"]) &&
    string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GROQ_API_KEY")))
{
    var groqEnvPath = Path.GetFullPath(Path.Combine(
        builder.Environment.ContentRootPath,
        "..",
        "Skill-Hub-AI-Agent",
        ".env"));

    if (File.Exists(groqEnvPath))
    {
        foreach (var rawLine in File.ReadLines(groqEnvPath))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var separator = line.IndexOf('=');
            if (separator <= 0) continue;

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim().Trim('"', '\'');
            if (key == "GROQ_API_KEY") builder.Configuration["Groq:ApiKey"] = value;
            if (key == "GROQ_MODEL") builder.Configuration["Groq:Model"] = value;
            if (key == "GROQ_FALLBACK_MODEL") builder.Configuration["Groq:FallbackModel"] = value;
        }
    }
}

// ==========================================
// 1. DATABASE & EF CORE (POSTGRESQL / NEONDB)
// ==========================================
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null);
        npgsqlOptions.CommandTimeout(60);
    }));

// ==========================================
// 2. DEPENDENCY INJECTION (APPLICATION SERVICES)
// ==========================================
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAiAgentService, GroqAiAgentService>();
builder.Services.AddScoped<IMatchService, MatchService>();
builder.Services.AddScoped<IJobRecommendationService, JobRecommendationService>();
builder.Services.AddScoped<IApplicantScreeningService, ApplicantScreeningService>();

var aiAgentBaseUrl = builder.Configuration["AiAgent:BaseUrl"]
    ?? throw new InvalidOperationException(
        "Missing required configuration value: AiAgent:BaseUrl");

builder.Services.AddHttpClient(LangGraphAiAgentService.HttpClientName, client =>
{
    client.BaseAddress = new Uri(aiAgentBaseUrl, UriKind.Absolute);
    // Keep this below the frontend's 120-second ceiling so the API can return a
    // controlled error, while allowing the evaluator and policy pass to finish.
    client.Timeout = TimeSpan.FromSeconds(110);
    client.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
});

builder.Services.AddHttpClient(GroqAiAgentService.HttpClientName, client =>
{
    client.BaseAddress = new Uri("https://api.groq.com/openai/v1/", UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(110);
    client.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
});

builder.Services.AddHttpClient(JobRecommendationService.HttpClientName, client =>
{
    client.BaseAddress = new Uri(aiAgentBaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(90);
    client.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
});

// ==========================================
// 3. CONTROLLERS & JSON SERIALIZATION
// ==========================================
builder.Services.AddControllers();

// ==========================================
// 4. JWT BEARER AUTHENTICATION & AUTHORIZATION
// ==========================================
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] 
    ?? "SkillHub_Super_Secret_Production_Security_Key_2026_ATS_AI_Matching_Engine_Secure!";
var issuer = jwtSettings["Issuer"] ?? "SkillHubApi";
var audience = jwtSettings["Audience"] ?? "SkillHubClients";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // Set to true in strict production SSL environments
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ValidateIssuer = true,
        ValidIssuer = issuer,
        ValidateAudience = true,
        ValidAudience = audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// ==========================================
// 5. CORS CONFIGURATION (REACT VITE FRONTEND)
// ==========================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ==========================================
// 6. SWAGGER / OPENAPI (WITH JWT AUTHORIZATION)
// ==========================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Skill Hub Enterprise ATS API",
        Version = "v1",
        Description = "Production-grade B2B AI-Driven Applicant Tracking System & Talent Marketplace API."
    });

    // Add JWT Bearer Security Definition in Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer' [space] and then your valid JWT token.\r\n\r\nExample: \"Bearer eyJhbGciOiJIUzI1NiIsIn...\""
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ==========================================
// 7. HTTP REQUEST PIPELINE MIDDLEWARE
// ==========================================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Skill Hub API v1");
        c.RoutePrefix = "swagger";
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Redirect root URL directly to Swagger UI in development
app.MapGet("/", () => Results.Redirect("/swagger"));

// ==========================================
// 8. AUTOMATIC DATABASE SCHEMA SYNCHRONIZATION
// ==========================================
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        logger.LogInformation("Synchronizing database schema and tables in public schema...");

        var ddlStatements = new[]
        {
            // 1. Companies Table
            @"CREATE TABLE IF NOT EXISTS public.""Companies"" (
                ""Id"" uuid NOT NULL PRIMARY KEY,
                ""CompanyName"" character varying(200) NOT NULL,
                ""AdminName"" character varying(150),
                ""ContactEmail"" character varying(255) NOT NULL,
                ""PasswordHash"" text NOT NULL DEFAULT '',
                ""Phone"" character varying(50),
                ""CompanySize"" character varying(100),
                ""FoundedYear"" character varying(50),
                ""LogoUrl"" character varying(500),
                ""Website"" character varying(255),
                ""LinkedinUrl"" character varying(255),
                ""TwitterUrl"" character varying(255),
                ""GithubUrl"" character varying(255),
                ""Location"" character varying(200),
                ""Industry"" character varying(100),
                ""About"" text,
                ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
                ""UpdatedAt"" timestamp with time zone NOT NULL DEFAULT NOW()
            );",

            // 1b. Ensure Extended Columns exist on Companies table for existing databases
            @"DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Companies' AND column_name = 'PasswordHash') THEN
                    ALTER TABLE public.""Companies"" ADD COLUMN ""PasswordHash"" text NOT NULL DEFAULT '';
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Companies' AND column_name = 'AdminName') THEN
                    ALTER TABLE public.""Companies"" ADD COLUMN ""AdminName"" character varying(150);
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Companies' AND column_name = 'Phone') THEN
                    ALTER TABLE public.""Companies"" ADD COLUMN ""Phone"" character varying(50);
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Companies' AND column_name = 'CompanySize') THEN
                    ALTER TABLE public.""Companies"" ADD COLUMN ""CompanySize"" character varying(100);
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Companies' AND column_name = 'FoundedYear') THEN
                    ALTER TABLE public.""Companies"" ADD COLUMN ""FoundedYear"" character varying(50);
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Companies' AND column_name = 'LogoUrl') THEN
                    ALTER TABLE public.""Companies"" ADD COLUMN ""LogoUrl"" character varying(500);
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Companies' AND column_name = 'Website') THEN
                    ALTER TABLE public.""Companies"" ADD COLUMN ""Website"" character varying(255);
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Companies' AND column_name = 'LinkedinUrl') THEN
                    ALTER TABLE public.""Companies"" ADD COLUMN ""LinkedinUrl"" character varying(255);
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Companies' AND column_name = 'TwitterUrl') THEN
                    ALTER TABLE public.""Companies"" ADD COLUMN ""TwitterUrl"" character varying(255);
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Companies' AND column_name = 'GithubUrl') THEN
                    ALTER TABLE public.""Companies"" ADD COLUMN ""GithubUrl"" character varying(255);
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Companies' AND column_name = 'Location') THEN
                    ALTER TABLE public.""Companies"" ADD COLUMN ""Location"" character varying(200);
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Companies' AND column_name = 'Industry') THEN
                    ALTER TABLE public.""Companies"" ADD COLUMN ""Industry"" character varying(100);
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Companies' AND column_name = 'About') THEN
                    ALTER TABLE public.""Companies"" ADD COLUMN ""About"" text;
                END IF;
            END $$;",

            // 1c. Unique Index on ContactEmail
            @"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Companies_ContactEmail"" ON public.""Companies"" (""ContactEmail"");",

            // 2. Users Table
            @"CREATE TABLE IF NOT EXISTS public.""Users"" (
                ""Id"" uuid NOT NULL PRIMARY KEY,
                ""CompanyId"" uuid REFERENCES public.""Companies"" (""Id"") ON DELETE CASCADE,
                ""FirstName"" character varying(100),
                ""LastName"" character varying(100),
                ""FullName"" character varying(150) NOT NULL,
                ""Email"" character varying(255) NOT NULL,
                ""PasswordHash"" text NOT NULL,
                ""Role"" character varying(50) NOT NULL DEFAULT 'CANDIDATE',
                ""Headline"" character varying(200),
                ""Phone"" character varying(50),
                ""Location"" character varying(200),
                ""AvatarUrl"" character varying(500),
                ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
                ""UpdatedAt"" timestamp with time zone NOT NULL DEFAULT NOW()
            );",

            // 2b. Ensure Candidate Columns & Nullable CompanyId on existing Users table
            @"DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Users' AND column_name = 'CompanyId' AND is_nullable = 'NO') THEN
                    ALTER TABLE public.""Users"" ALTER COLUMN ""CompanyId"" DROP NOT NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Users' AND column_name = 'FirstName') THEN
                    ALTER TABLE public.""Users"" ADD COLUMN ""FirstName"" character varying(100);
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Users' AND column_name = 'LastName') THEN
                    ALTER TABLE public.""Users"" ADD COLUMN ""LastName"" character varying(100);
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Users' AND column_name = 'Headline') THEN
                    ALTER TABLE public.""Users"" ADD COLUMN ""Headline"" character varying(200);
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Users' AND column_name = 'Phone') THEN
                    ALTER TABLE public.""Users"" ADD COLUMN ""Phone"" character varying(50);
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Users' AND column_name = 'Location') THEN
                    ALTER TABLE public.""Users"" ADD COLUMN ""Location"" character varying(200);
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Users' AND column_name = 'Experience') THEN
                    ALTER TABLE public.""Users"" ADD COLUMN ""Experience"" character varying(100);
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Users' AND column_name = 'Availability') THEN
                    ALTER TABLE public.""Users"" ADD COLUMN ""Availability"" character varying(100);
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Users' AND column_name = 'AvatarUrl') THEN
                    ALTER TABLE public.""Users"" ADD COLUMN ""AvatarUrl"" character varying(500);
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Users' AND column_name = 'About') THEN
                    ALTER TABLE public.""Users"" ADD COLUMN ""About"" text;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Users' AND column_name = 'KeyHighlights') THEN
                    ALTER TABLE public.""Users"" ADD COLUMN ""KeyHighlights"" text;
                END IF;
            END $$;",

            // 2c. Indexes on Users
            @"CREATE INDEX IF NOT EXISTS ""IX_Users_CompanyId"" ON public.""Users"" (""CompanyId"");",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Users_Email"" ON public.""Users"" (""Email"");",

            // 3. JobVacancies Table
            @"CREATE TABLE IF NOT EXISTS public.""JobVacancies"" (
                ""Id"" uuid NOT NULL PRIMARY KEY,
                ""CompanyId"" uuid NOT NULL REFERENCES public.""Companies"" (""Id"") ON DELETE CASCADE,
                ""Title"" character varying(200) NOT NULL,
                ""Department"" character varying(100) NOT NULL,
                ""Location"" character varying(150) NOT NULL,
                ""EmploymentType"" character varying(50) NOT NULL,
                ""ExperienceLevel"" character varying(50) NOT NULL,
                ""SalaryRange"" character varying(100),
                ""Status"" character varying(50) NOT NULL DEFAULT 'Active',
                ""Description"" text NOT NULL,
                ""WhatWeOffer"" text,
                ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
                ""UpdatedAt"" timestamp with time zone NOT NULL DEFAULT NOW()
            );",

            // 3b. Indexes on JobVacancies
            @"CREATE INDEX IF NOT EXISTS ""IX_JobVacancies_CompanyId"" ON public.""JobVacancies"" (""CompanyId"");",
            @"CREATE INDEX IF NOT EXISTS ""IX_JobVacancies_Status"" ON public.""JobVacancies"" (""Status"");",
            @"CREATE INDEX IF NOT EXISTS ""IX_JobVacancies_CreatedAt"" ON public.""JobVacancies"" (""CreatedAt"");",

            // 4. CandidateExperiences Table
            @"CREATE TABLE IF NOT EXISTS public.""CandidateExperiences"" (
                ""Id"" uuid NOT NULL PRIMARY KEY,
                ""UserId"" uuid NOT NULL REFERENCES public.""Users"" (""Id"") ON DELETE CASCADE,
                ""Title"" character varying(200) NOT NULL,
                ""Company"" character varying(200) NOT NULL,
                ""Location"" character varying(150),
                ""StartDate"" character varying(50) NOT NULL,
                ""EndDate"" character varying(50),
                ""IsCurrent"" boolean NOT NULL DEFAULT false,
                ""Description"" text,
                ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
                ""UpdatedAt"" timestamp with time zone NOT NULL DEFAULT NOW()
            );",
            @"CREATE INDEX IF NOT EXISTS ""IX_CandidateExperiences_UserId"" ON public.""CandidateExperiences"" (""UserId"");",

            // 5. CandidateEducations Table
            @"CREATE TABLE IF NOT EXISTS public.""CandidateEducations"" (
                ""Id"" uuid NOT NULL PRIMARY KEY,
                ""UserId"" uuid NOT NULL REFERENCES public.""Users"" (""Id"") ON DELETE CASCADE,
                ""Degree"" character varying(200) NOT NULL,
                ""Institution"" character varying(200) NOT NULL,
                ""FieldOfStudy"" character varying(150),
                ""StartYear"" character varying(50) NOT NULL,
                ""EndYear"" character varying(50),
                ""Description"" text,
                ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
                ""UpdatedAt"" timestamp with time zone NOT NULL DEFAULT NOW()
            );",
            @"CREATE INDEX IF NOT EXISTS ""IX_CandidateEducations_UserId"" ON public.""CandidateEducations"" (""UserId"");",

            // 6. CandidateProjects Table
            @"CREATE TABLE IF NOT EXISTS public.""CandidateProjects"" (
                ""Id"" uuid NOT NULL PRIMARY KEY,
                ""UserId"" uuid NOT NULL REFERENCES public.""Users"" (""Id"") ON DELETE CASCADE,
                ""ProjectName"" character varying(200) NOT NULL,
                ""Role"" character varying(150),
                ""Description"" text,
                ""Link"" character varying(500),
                ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
                ""UpdatedAt"" timestamp with time zone NOT NULL DEFAULT NOW()
            );",
            @"CREATE INDEX IF NOT EXISTS ""IX_CandidateProjects_UserId"" ON public.""CandidateProjects"" (""UserId"");",

            // 7. CandidateSkills Table
            @"CREATE TABLE IF NOT EXISTS public.""CandidateSkills"" (
                ""Id"" uuid NOT NULL PRIMARY KEY,
                ""UserId"" uuid NOT NULL REFERENCES public.""Users"" (""Id"") ON DELETE CASCADE,
                ""SkillName"" character varying(100) NOT NULL,
                ""Category"" character varying(100),
                ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT NOW()
            );",
            @"CREATE INDEX IF NOT EXISTS ""IX_CandidateSkills_UserId"" ON public.""CandidateSkills"" (""UserId"");",

            // 8. CandidateCertifications Table
            @"CREATE TABLE IF NOT EXISTS public.""CandidateCertifications"" (
                ""Id"" uuid NOT NULL PRIMARY KEY,
                ""UserId"" uuid NOT NULL REFERENCES public.""Users"" (""Id"") ON DELETE CASCADE,
                ""Title"" character varying(200) NOT NULL,
                ""IssuingOrganization"" character varying(200) NOT NULL,
                ""IssueDate"" character varying(100),
                ""CredentialUrl"" character varying(500),
                ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
                ""UpdatedAt"" timestamp with time zone NOT NULL DEFAULT NOW()
            );",
            @"CREATE INDEX IF NOT EXISTS ""IX_CandidateCertifications_UserId"" ON public.""CandidateCertifications"" (""UserId"");",

            // 9. JobApplications Table
            @"CREATE TABLE IF NOT EXISTS public.""JobApplications"" (
                ""Id"" uuid NOT NULL PRIMARY KEY,
                ""JobId"" uuid NOT NULL REFERENCES public.""JobVacancies"" (""Id"") ON DELETE CASCADE,
                ""CandidateId"" uuid NOT NULL REFERENCES public.""Users"" (""Id"") ON DELETE CASCADE,
                ""AppliedDate"" timestamp with time zone NOT NULL DEFAULT NOW(),
                ""Status"" character varying(50) NOT NULL DEFAULT 'Applied',
                ""CoverNote"" character varying(2000),
                ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
                ""UpdatedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
                CONSTRAINT ""UQ_JobApplications_JobId_CandidateId"" UNIQUE (""JobId"", ""CandidateId"")
            );",
            @"CREATE INDEX IF NOT EXISTS ""IX_JobApplications_JobId"" ON public.""JobApplications"" (""JobId"");",
            @"CREATE INDEX IF NOT EXISTS ""IX_JobApplications_CandidateId"" ON public.""JobApplications"" (""CandidateId"");",
            @"CREATE INDEX IF NOT EXISTS ""IX_JobApplications_AppliedDate"" ON public.""JobApplications"" (""AppliedDate"");",

            // 10. Deterministic AI Match Cache
            @"CREATE TABLE IF NOT EXISTS public.""AiMatchResults"" (
                ""Id"" uuid NOT NULL PRIMARY KEY,
                ""CandidateId"" uuid NOT NULL REFERENCES public.""Users"" (""Id"") ON DELETE CASCADE,
                ""JobId"" uuid NOT NULL REFERENCES public.""JobVacancies"" (""Id"") ON DELETE CASCADE,
                ""MatchPercentage"" integer NOT NULL CHECK (""MatchPercentage"" BETWEEN 0 AND 100),
                ""BreakdownJson"" text,
                ""StrengthsJson"" text,
                ""MissingSkillsJson"" text,
                ""Recommendation"" text,
                ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
                CONSTRAINT ""UQ_AiMatchResults_CandidateId_JobId"" UNIQUE (""CandidateId"", ""JobId"")
            );",
            @"ALTER TABLE public.""AiMatchResults"" ADD COLUMN IF NOT EXISTS ""BreakdownJson"" text;",
            @"ALTER TABLE public.""AiMatchResults"" ADD COLUMN IF NOT EXISTS ""StrengthsJson"" text;",
            @"ALTER TABLE public.""AiMatchResults"" ADD COLUMN IF NOT EXISTS ""MissingSkillsJson"" text;",
            @"ALTER TABLE public.""AiMatchResults"" ADD COLUMN IF NOT EXISTS ""Recommendation"" text;",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_AiMatchResults_CandidateId_JobId"" ON public.""AiMatchResults"" (""CandidateId"", ""JobId"");",
            @"CREATE INDEX IF NOT EXISTS ""IX_AiMatchResults_JobId"" ON public.""AiMatchResults"" (""JobId"");",

            // 11. Candidate-owned saved job bookmarks
            @"CREATE TABLE IF NOT EXISTS public.""SavedJobs"" (
                ""Id"" uuid NOT NULL PRIMARY KEY,
                ""CandidateId"" uuid NOT NULL REFERENCES public.""Users"" (""Id"") ON DELETE CASCADE,
                ""JobId"" uuid NOT NULL REFERENCES public.""JobVacancies"" (""Id"") ON DELETE CASCADE,
                ""SavedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
                CONSTRAINT ""UQ_SavedJobs_CandidateId_JobId"" UNIQUE (""CandidateId"", ""JobId"")
            );",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_SavedJobs_CandidateId_JobId"" ON public.""SavedJobs"" (""CandidateId"", ""JobId"");",
            @"CREATE INDEX IF NOT EXISTS ""IX_SavedJobs_JobId"" ON public.""SavedJobs"" (""JobId"");"
        };

        foreach (var ddl in ddlStatements)
        {
            dbContext.Database.ExecuteSqlRaw(ddl);
        }

        logger.LogInformation("Database schema synchronized successfully (Companies, Users, JobVacancies, Candidate CV Tables verified in 'public').");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while synchronizing the database schema: {Message}", ex.Message);
    }
}

app.Run();

