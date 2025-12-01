using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace WebApplication10.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class QAVerificationController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly string _connStr;

        public QAVerificationController(IConfiguration config)
        {
            _config = config;
            _connStr = config.GetConnectionString("OracleConnection");
        }

        [HttpPost("save")]
        public async Task<IActionResult> SaveVerification([FromBody] VerificationRequest request)
        {
            var empCode = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
            var empName = User.FindFirst(System.Security.Claims.ClaimTypes.GivenName)?.Value ?? "Unknown";

            if (string.IsNullOrEmpty(empCode))
                return Unauthorized("User not authenticated");

            if (string.IsNullOrEmpty(request.CrfId) || string.IsNullOrEmpty(request.WorkingStatus))
                return BadRequest("CRF ID and Working Status are required");

            // File size check (≤10 MB)
            if (!string.IsNullOrEmpty(request.AttachmentBase64))
            {
                try
                {
                    var bytes = Convert.FromBase64String(request.AttachmentBase64);
                    if (bytes.Length > 10 * 1024 * 1024)
                        return BadRequest("Attachment size must be ≤ 10 MB");
                }
                catch
                {
                    return BadRequest("Invalid attachment data");
                }
            }

            using (var conn = new OracleConnection(_connStr))
            {
                await conn.OpenAsync();
                using (var cmd = new OracleCommand("proc_qa_verification", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Input parameters
                    cmd.Parameters.Add("p_crf_id", OracleDbType.Varchar2).Value = request.CrfId;
                    cmd.Parameters.Add("p_working_status", OracleDbType.Varchar2).Value = request.WorkingStatus;
                    cmd.Parameters.Add("p_remarks", OracleDbType.Varchar2).Value = 
                        string.IsNullOrEmpty(request.Remarks) ? (object)DBNull.Value : request.Remarks.Length > 4000 
                            ? request.Remarks.Substring(0, 4000) : request.Remarks;

                    cmd.Parameters.Add("p_attachment_name", OracleDbType.Varchar2).Value = 
                        string.IsNullOrEmpty(request.AttachmentName) ? (object)DBNull.Value : request.AttachmentName;

                    if (!string.IsNullOrEmpty(request.AttachmentBase64))
                    {
                        var blobData = Convert.FromBase64String(request.AttachmentBase64);
                        cmd.Parameters.Add("p_attachment_data", OracleDbType.Blob).Value = blobData;
                    }
                    else
                    {
                        cmd.Parameters.Add("p_attachment_data", OracleDbType.Blob).Value = DBNull.Value;
                    }

                    cmd.Parameters.Add("p_attachment_mime", OracleDbType.Varchar2).Value = 
                        string.IsNullOrEmpty(request.AttachmentMime) ? (object)DBNull.Value : request.AttachmentMime;

                    cmd.Parameters.Add("p_verified_by", OracleDbType.Varchar2).Value = empCode;
                    cmd.Parameters.Add("p_verified_by_name", OracleDbType.Varchar2).Value = empName;

                    // Output parameters
                    var resultParam = cmd.Parameters.Add("p_result", OracleDbType.Varchar2);
                    resultParam.Direction = ParameterDirection.Output;
                    resultParam.Size = 4000;

                    var verIdParam = cmd.Parameters.Add("p_verification_id", OracleDbType.Decimal);
                    verIdParam.Direction = ParameterDirection.Output;

                    try
                    {
                        await cmd.ExecuteNonQueryAsync();

                        string result = resultParam.Value?.ToString() ?? "UNKNOWN";
                        string verId = verIdParam.Value?.ToString() ?? "0";

                        if (result == "SUCCESS")
                        {
                            return Ok(new
                            {
                                success = true,
                                message = "Verification saved successfully",
                                verification_id = verId
                            });
                        }
                        else
                        {
                            return BadRequest(new { success = false, message = result });
                        }
                    }
                    catch (Exception ex)
                    {
                        return StatusCode(500, new { success = false, message = "Database error: " + ex.Message });
                    }
                }
            }
        }
    }

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