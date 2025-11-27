using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApplication10.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DailyVerificationController : ControllerBase
    {
        // GET yesterday's data or filtered data
        [HttpPost]
        [Authorize]
        public IActionResult GetReport([FromBody] object filters)
        {
            // Sample data - replace with real Oracle query later
            var data = new[]
            {
                new { crfId = "131001", subject = "Payment Gateway Fix", releaseDate = "2025-11-21", developer = "Suresh K", tester = "Rajendran N", workingStatus = (string)null },
                new { crfId = "131002", subject = "Export Report Issue", releaseDate = "2025-11-21", developer = "Anil M", tester = "Aswathy Chandran", workingStatus = "Working" },
                new { crfId = "131003", subject = "Login Timeout", releaseDate = "2025-11-20", developer = "Priya R", tester = "Adhitya T S", workingStatus = "Not Working" },
                new { crfId = "131003", subject = "Dashboard Load Slow", releaseDate = "2025-11-22", developer = "Vijay S", tester = "Anjitha K A", workingStatus = "In Progress" }
            };

            return Ok(data);
        }

        [HttpPost("Save")]
        [Authorize]
        public IActionResult Save([FromBody] object[] updates)
        {
            // TODO: Save to Oracle table
            return Ok(new { message = "All verifications saved successfully!" });
        }
    }
}