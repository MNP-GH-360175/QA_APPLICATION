namespace QA.API.Models
{
    public class VerificationRequest
    {
        public string CrfId { get; set; } = string.Empty;
        public string WorkingStatus { get; set; } = string.Empty;
        public string? Remarks { get; set; }
        public string? AttachmentName { get; set; }
        public string? AttachmentBase64 { get; set; }
        public string? AttachmentMime { get; set; }
    }
}
