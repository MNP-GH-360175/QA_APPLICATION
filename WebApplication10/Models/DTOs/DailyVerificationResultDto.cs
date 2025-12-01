// Models/DTOs/DailyVerificationResultDto.cs
namespace WebApplication10.Models.DTOs
{
    public class DailyVerificationResultDto
    {
        public string CRF_ID { get; set; } = string.Empty;
        public string CRF_NAME { get; set; } = string.Empty;
        public string RELEASE_DATE { get; set; } = string.Empty;
        public string RELEASE_TYPE { get; set; } = string.Empty;
        public string DEVELOPER_TL_NAME { get; set; } = string.Empty;
        public string DEVELOPER_NAME { get; set; } = string.Empty;
        public string TESTER_TL_NAME { get; set; } = string.Empty;
        public string TESTER_NAME { get; set; } = string.Empty;

        // Add these for frontend use (from your verification table)
        public string? WorkingStatus { get; set; }
        public string? Remarks { get; set; }
        public bool IsConfirmed { get; set; }
        public string? AttachmentName { get; set; }
        public string? AttachmentBase64 { get; set; }
        public List<VerificationHistoryDto>? History { get; set; }
    }

    public class VerificationHistoryDto
    {
        public string Employee { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public string DateTime { get; set; } = string.Empty;
    }
}