using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skill_Hub_BackEnd.Migrations
{
    /// <inheritdoc />
    public partial class AddCvEvaluationResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "CompanyId",
                schema: "public",
                table: "Users",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "About",
                schema: "public",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Availability",
                schema: "public",
                table: "Users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                schema: "public",
                table: "Users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Experience",
                schema: "public",
                table: "Users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                schema: "public",
                table: "Users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Headline",
                schema: "public",
                table: "Users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KeyHighlights",
                schema: "public",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                schema: "public",
                table: "Users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                schema: "public",
                table: "Users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                schema: "public",
                table: "Users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Deadline",
                schema: "public",
                table: "JobVacancies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AiMatchResults",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchPercentage = table.Column<int>(type: "integer", nullable: false),
                    BreakdownJson = table.Column<string>(type: "text", nullable: true),
                    StrengthsJson = table.Column<string>(type: "text", nullable: true),
                    MissingSkillsJson = table.Column<string>(type: "text", nullable: true),
                    Recommendation = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiMatchResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiMatchResults_JobVacancies_JobId",
                        column: x => x.JobId,
                        principalSchema: "public",
                        principalTable: "JobVacancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AiMatchResults_Users_CandidateId",
                        column: x => x.CandidateId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Assessments",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobVacancyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    GeneratedQuestions = table.Column<string>(type: "jsonb", nullable: false),
                    FinalQuestions = table.Column<string>(type: "jsonb", nullable: false),
                    PassingThreshold = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    TimeLimitMinutes = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assessments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CandidateCertifications",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IssuingOrganization = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IssueDate = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CredentialUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateCertifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateCertifications_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CandidateEducations",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Degree = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Institution = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FieldOfStudy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    StartYear = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EndYear = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateEducations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateEducations_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CandidateExperiences",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Company = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Location = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    StartDate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EndDate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateExperiences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateExperiences_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CandidateProjects",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Link = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateProjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateProjects_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CandidateSkills",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateSkills_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CvEvaluationResults",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    MatchScore = table.Column<int>(type: "integer", nullable: false),
                    StrengthsJson = table.Column<string>(type: "jsonb", nullable: true),
                    MissingSkillsJson = table.Column<string>(type: "jsonb", nullable: true),
                    Recommendation = table.Column<string>(type: "text", nullable: true),
                    ExtractedDataJson = table.Column<string>(type: "jsonb", nullable: true),
                    ValidationNotesJson = table.Column<string>(type: "jsonb", nullable: true),
                    ApprovalStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    ReviewerNotes = table.Column<string>(type: "text", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CvEvaluationResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CvEvaluationResults_JobVacancies_JobId",
                        column: x => x.JobId,
                        principalSchema: "public",
                        principalTable: "JobVacancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CvEvaluationResults_Users_CandidateId",
                        column: x => x.CandidateId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobApplications",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppliedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CoverNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobApplications_JobVacancies_JobId",
                        column: x => x.JobId,
                        principalSchema: "public",
                        principalTable: "JobVacancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JobApplications_Users_CandidateId",
                        column: x => x.CandidateId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SavedJobs",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedJobs_JobVacancies_JobId",
                        column: x => x.JobId,
                        principalSchema: "public",
                        principalTable: "JobVacancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SavedJobs_Users_CandidateId",
                        column: x => x.CandidateId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Submissions",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobVacancyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Answers = table.Column<string>(type: "jsonb", nullable: true),
                    ExamScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    CvScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    FinalWeightedScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GradedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProctorFlags = table.Column<string>(type: "jsonb", nullable: true),
                    IsSelectedForInterview = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ReviewerFeedback = table.Column<string>(type: "text", nullable: true),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Submissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Submissions_Assessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalSchema: "public",
                        principalTable: "Assessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiMatchResults_CandidateId_JobId",
                schema: "public",
                table: "AiMatchResults",
                columns: new[] { "CandidateId", "JobId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiMatchResults_JobId",
                schema: "public",
                table: "AiMatchResults",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_CreatedBy",
                schema: "public",
                table: "Assessments",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_JobVacancyId",
                schema: "public",
                table: "Assessments",
                column: "JobVacancyId");

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_Status",
                schema: "public",
                table: "Assessments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateCertifications_UserId",
                schema: "public",
                table: "CandidateCertifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateEducations_UserId",
                schema: "public",
                table: "CandidateEducations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateExperiences_UserId",
                schema: "public",
                table: "CandidateExperiences",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateProjects_UserId",
                schema: "public",
                table: "CandidateProjects",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateSkills_UserId",
                schema: "public",
                table: "CandidateSkills",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CvEvaluationResults_ApprovalStatus",
                schema: "public",
                table: "CvEvaluationResults",
                column: "ApprovalStatus");

            migrationBuilder.CreateIndex(
                name: "IX_CvEvaluationResults_Candidate_Job_Date",
                schema: "public",
                table: "CvEvaluationResults",
                columns: new[] { "CandidateId", "JobId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CvEvaluationResults_JobId",
                schema: "public",
                table: "CvEvaluationResults",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_AppliedDate",
                schema: "public",
                table: "JobApplications",
                column: "AppliedDate");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_CandidateId",
                schema: "public",
                table: "JobApplications",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_JobId",
                schema: "public",
                table: "JobApplications",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_JobId_CandidateId",
                schema: "public",
                table: "JobApplications",
                columns: new[] { "JobId", "CandidateId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedJobs_CandidateId_JobId",
                schema: "public",
                table: "SavedJobs",
                columns: new[] { "CandidateId", "JobId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedJobs_JobId",
                schema: "public",
                table: "SavedJobs",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_ApplicationId",
                schema: "public",
                table: "Submissions",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_AssessmentId",
                schema: "public",
                table: "Submissions",
                column: "AssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_CandidateId",
                schema: "public",
                table: "Submissions",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_JobVacancyId",
                schema: "public",
                table: "Submissions",
                column: "JobVacancyId");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_Status",
                schema: "public",
                table: "Submissions",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiMatchResults",
                schema: "public");

            migrationBuilder.DropTable(
                name: "CandidateCertifications",
                schema: "public");

            migrationBuilder.DropTable(
                name: "CandidateEducations",
                schema: "public");

            migrationBuilder.DropTable(
                name: "CandidateExperiences",
                schema: "public");

            migrationBuilder.DropTable(
                name: "CandidateProjects",
                schema: "public");

            migrationBuilder.DropTable(
                name: "CandidateSkills",
                schema: "public");

            migrationBuilder.DropTable(
                name: "CvEvaluationResults",
                schema: "public");

            migrationBuilder.DropTable(
                name: "JobApplications",
                schema: "public");

            migrationBuilder.DropTable(
                name: "SavedJobs",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Submissions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Assessments",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "About",
                schema: "public",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Availability",
                schema: "public",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                schema: "public",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Experience",
                schema: "public",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "FirstName",
                schema: "public",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Headline",
                schema: "public",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "KeyHighlights",
                schema: "public",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastName",
                schema: "public",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Location",
                schema: "public",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Phone",
                schema: "public",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Deadline",
                schema: "public",
                table: "JobVacancies");

            migrationBuilder.AlterColumn<Guid>(
                name: "CompanyId",
                schema: "public",
                table: "Users",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
