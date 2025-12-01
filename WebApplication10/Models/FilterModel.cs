// Models/FilterModel.cs
namespace WebApplication10.Models
{
    public class FilterModel
    {
        public string? releaseType { get; set; }     // Can be null/empty
        public string fromDate { get; set; }         // Required → "01-DEC-2025"
        public string toDate { get; set; }           // Required
        public string? testerTL { get; set; }        // Optional → null = All TLs
        public string? testerName { get; set; }      // Optional → null = All Testers
    }
}