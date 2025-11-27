using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class ReleaseReportController : ControllerBase
{
    [HttpPost]
    [Authorize]
    public IActionResult GetReport([FromBody] dynamic filters)
    {
        // TODO: Connect to Oracle and apply filters
        // For now, return sample data
        var sampleData = new[]
        {
            new { crfId = "CRF001", requestId = "REQ123", subject = "Login Fix", currentStatus = "Released", releaseDate = "2025-11-20", tester = "Rajendran N", testerTl = "NIKHIL SEKHAR", developer = "Dev A", techLead = "TL X", contactNumber = "9999999999", updatedBy = "Admin", cabId = "CAB001", cabDate = "2025-11-18", parentApp = "MainApp", codeReviewer = "CR1", repositoryName = "GitHub/Main", changesObjects = "10", qaRecommendedBy = "QA1", qaRecommendedOn = "2025-11-19", qaRemark = "OK", releaseStatus = "Approved", releaseTeamRemark = "Done", releasedOn = "2025-11-20", releasedBy = "ReleaseTeam", status = "Success", remarks = "Good", date = "2025-11-20" }
            // Add more rows...
        };

        return Ok(sampleData);
    }
}