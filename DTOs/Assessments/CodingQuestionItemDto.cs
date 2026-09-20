using System.Text.Json.Serialization;

namespace Skill_Hub_BackEnd.DTOs.Assessments
{
    public sealed class TestCaseDto
    {
        [JsonPropertyName("input")]
        public string Input { get; set; } = string.Empty;

        [JsonPropertyName("expectedOutput")]
        public string ExpectedOutput { get; set; } = string.Empty;

        [JsonPropertyName("isHidden")]
        public bool IsHidden { get; set; } = false;
    }

    public sealed class CodingQuestionItemDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("problemStatement")]
        public string ProblemStatement { get; set; } = string.Empty;

        [JsonPropertyName("language")]
        public string Language { get; set; } = "csharp"; // csharp, python, javascript, sql, etc.

        [JsonPropertyName("difficulty")]
        public string Difficulty { get; set; } = "Medium"; // Easy, Medium, Hard

        [JsonPropertyName("starterCode")]
        public string StarterCode { get; set; } = string.Empty;

        [JsonPropertyName("solutionCode")]
        public string? SolutionCode { get; set; }

        [JsonPropertyName("sampleTestCases")]
        public List<TestCaseDto> SampleTestCases { get; set; } = new();

        [JsonPropertyName("hiddenTestCases")]
        public List<TestCaseDto> HiddenTestCases { get; set; } = new();

        [JsonPropertyName("points")]
        public int Points { get; set; } = 10;

        [JsonPropertyName("order")]
        public int Order { get; set; } = 1;
    }

    public sealed class CandidateCodingQuestionDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("problemStatement")]
        public string ProblemStatement { get; set; } = string.Empty;

        [JsonPropertyName("language")]
        public string Language { get; set; } = "csharp";

        [JsonPropertyName("difficulty")]
        public string Difficulty { get; set; } = "Medium";

        [JsonPropertyName("starterCode")]
        public string StarterCode { get; set; } = string.Empty;

        [JsonPropertyName("sampleTestCases")]
        public List<TestCaseDto> SampleTestCases { get; set; } = new();

        [JsonPropertyName("points")]
        public int Points { get; set; } = 10;

        [JsonPropertyName("order")]
        public int Order { get; set; } = 1;
    }
}

