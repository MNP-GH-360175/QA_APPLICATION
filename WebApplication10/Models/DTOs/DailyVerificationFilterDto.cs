// Models/DTOs/DailyVerificationFilterDto.cs
using System.ComponentModel.DataAnnotations;

namespace WebApplication10.Models.DTOs 
{
    public class DailyVerificationFilterDto
    {
        [Required(ErrorMessage = "Release Type is required")]
        public string ReleaseType { get; set; } = string.Empty;

        [Required(ErrorMessage = "From Date is required")]
        public string FromDate { get; set; } = string.Empty;   // Format: 01-DEC-2025

        [Required(ErrorMessage = "To Date is required")]
        public string ToDate { get; set; } = string.Empty;

        // These are OPTIONAL → No [Required] attribute!
        public string? TesterTL { get; set; }       // null or empty = All TLs
        public string? TesterName { get; set; }     // null or empty = All Testers
    }
}