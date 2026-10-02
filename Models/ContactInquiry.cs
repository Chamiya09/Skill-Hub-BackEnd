using System.ComponentModel.DataAnnotations;

namespace Skill_Hub_BackEnd.Models
{
    public class ContactInquiry
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(200)]
        public string Sender { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string SenderType { get; set; } = "Guest"; // Candidate, Company, Guest

        [MaxLength(200)]
        public string? Organization { get; set; }

        [Required]
        [EmailAddress]
        [MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(300)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        public string Message { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Status { get; set; } = "New"; // New, Resolved

        [MaxLength(50)]
        public string Priority { get; set; } = "Normal"; // High, Normal

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
