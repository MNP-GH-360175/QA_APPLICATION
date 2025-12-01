namespace WebApplication10.Models
{
    public class VerificationSaveModel
    {
        public string crfId { get; set; }
        public string subject { get; set; }
        public string releaseDate { get; set; }
        public string developer { get; set; }
        public string tester { get; set; }
        public string testerTL { get; set; }
        public string workingStatus { get; set; }
        public string remarks { get; set; }
        public string? attachmentName { get; set; }
        public string? attachmentBase64 { get; set; }
        public string? attachmentMime { get; set; }
        public string releaseType { get; set; }  // "Daily Release Report" or "Exceptional and Expedite Report"
    }
}
