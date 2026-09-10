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
// 8. AUTOMATIC DATABASE SCHEMA INITIALIZATION
// ==========================================
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        logger.LogInformation("Verifying and ensuring database schema exists...");
        dbContext.Database.EnsureCreated();
        logger.LogInformation("Database schema initialized successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while ensuring the database schema exists: {Message}", ex.Message);
    }
}

app.Run();

