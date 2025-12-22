using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using Newtonsoft.Json;
using System.Security.Claims;
using Oracle.ManagedDataAccess.Types;

namespace WebApplication10.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class VerificationController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly string _connStr;

        public VerificationController(IConfiguration config)
        {
            _config = config;
            _connStr = _config.GetConnectionString("OracleConnection");
        }
        
        // FETCH: Daily Verification List
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] FilterModel filters)
        {
            if (filters == null || string.IsNullOrWhiteSpace(filters.fromDate) || string.IsNullOrWhiteSpace(filters.toDate))
                return BadRequest("Dates required");

            // Get current logged-in tester name from JWT
            var currentTester = User.FindFirst(ClaimTypes.GivenName)?.Value ??
                                User.FindFirst(ClaimTypes.Name)?.Value ??
                                User.FindFirst("unique_name")?.Value ?? "UNKNOWN";

            var list = new List<object>();

            using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();

            using var cmd = new OracleCommand("PROC_DAILY_REPORTS_MASTER", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.Add("p_flag", OracleDbType.Varchar2).Value = "DAILY_VERIFICATION";
            cmd.Parameters.Add("p_sub_flag", OracleDbType.Varchar2).Value = "FETCH";
            cmd.Parameters.Add("p_from_date", OracleDbType.Varchar2).Value = filters.fromDate;
            cmd.Parameters.Add("p_to_date", OracleDbType.Varchar2).Value = filters.toDate;
            cmd.Parameters.Add("p_release_type", OracleDbType.Varchar2).Value = filters.releaseType ?? (object)DBNull.Value;
            cmd.Parameters.Add("p_tester_tl", OracleDbType.Varchar2).Value = filters.testerTL ?? (object)DBNull.Value;
            cmd.Parameters.Add("p_tester_name", OracleDbType.Varchar2).Value = currentTester; // ← Filter by current user
            cmd.Parameters.Add("p_json_input", OracleDbType.Clob).Value = DBNull.Value;
            cmd.Parameters.Add("p_updated_by", OracleDbType.Varchar2).Value = DBNull.Value;
            cmd.Parameters.Add("p_result", OracleDbType.RefCursor).Direction = ParameterDirection.Output;

            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                list.Add(new
                {
                    CRF_ID = rdr["CRF_ID"]?.ToString() ?? "N/A",
                    REQUEST_ID = rdr["REQUEST_ID"]?.ToString() ?? "",
                    CRF_NAME = rdr["CRF_NAME"]?.ToString() ?? "Unknown CRF",
                    RELEASE_DATE = rdr["RELEASE_DATE"]?.ToString() ?? "",
                    TECHLEAD_NAME = rdr["TECHLEAD_NAME"]?.ToString() ?? "",
                    DEVELOPER_NAME = rdr["DEVELOPER_NAME"]?.ToString() ?? "",
                    TESTER_NAME = rdr["TESTER_NAME"]?.ToString() ?? "",
                    TESTER_TL_NAME = rdr["TESTER_TL_NAME"]?.ToString() ?? "",
                    RELEASE_TYPE = rdr["RELEASE_TYPE"]?.ToString() ?? "",
                    WORKING_STATUS = rdr["WORKING_STATUS"]?.ToString() ?? "",
                    REMARKS = rdr["REMARKS"]?.ToString() ?? "",
                    ATTACHMENT_NAME = rdr["ATTACHMENT_NAME"]?.ToString() ?? "",
                    VERIFIED_BY = rdr["VERIFIED_BY"]?.ToString() ?? "",
                    VERIFIED_ON = rdr["VERIFIED_ON"]?.ToString() ?? "",
                    HISTORY_JSON = rdr["HISTORY_JSON"]?.ToString() ?? "[]"
                });
            }

            return Ok(list);
        }

        // SAVE: Daily Verification + Attachment
        [HttpPost("Save")]
        public async Task<IActionResult> Save([FromBody] List<SaveModel> items)
        {
            if (items == null || items.Count == 0)
                return BadRequest("No data to save");

            var userName = User.FindFirst(ClaimTypes.GivenName)?.Value ??
                           User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown User";

            using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();

            using var cmd = new OracleCommand("PROC_DAILY_REPORTS_MASTER", conn) // ← NOW USING MASTER
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.Add("p_flag", OracleDbType.Varchar2).Value = "DAILY_VERIFICATION";
            cmd.Parameters.Add("p_sub_flag", OracleDbType.Varchar2).Value = "SAVE";
            cmd.Parameters.Add("p_from_date", OracleDbType.Varchar2).Value = DBNull.Value;
            cmd.Parameters.Add("p_to_date", OracleDbType.Varchar2).Value = DBNull.Value;
            cmd.Parameters.Add("p_release_type", OracleDbType.Varchar2).Value = DBNull.Value;
            cmd.Parameters.Add("p_tester_tl", OracleDbType.Varchar2).Value = DBNull.Value;
            cmd.Parameters.Add("p_tester_name", OracleDbType.Varchar2).Value = DBNull.Value;
            cmd.Parameters.Add("p_json_input", OracleDbType.Clob).Value = JsonConvert.SerializeObject(items);
            cmd.Parameters.Add("p_updated_by", OracleDbType.Varchar2).Value = userName;
            cmd.Parameters.Add("p_result", OracleDbType.RefCursor).Direction = ParameterDirection.Output;

            await cmd.ExecuteNonQueryAsync();

            return Ok(new { message = "Saved successfully" });
        }

        // GET FILTERS
        [HttpGet("filters")]
        public async Task<IActionResult> GetFilters()
        {
            var tlList = new List<object>();
            var testerList = new List<object>();

            using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();

            using (var cmd = new OracleCommand(@"
                SELECT DISTINCT est.emp_name AS name
                FROM mana0809.srm_dailyrelease_updn n
                JOIN mana0809.srm_testing st ON st.request_id = n.request_id
                JOIN mana0809.employee_master est ON est.emp_code = st.test_lead
                ORDER BY est.emp_name", conn))
            {
                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    var name = rdr["name"]?.ToString();
                    if (!string.IsNullOrEmpty(name))
                        tlList.Add(new { text = name, value = name });
                }
            }

            using (var cmd = new OracleCommand(@"
                SELECT DISTINCT TRIM(REGEXP_SUBSTR(tester, '[^,]+', 1, level)) AS name
                FROM mana0809.srm_dailyrelease_updn
                WHERE tester IS NOT NULL
                CONNECT BY REGEXP_SUBSTR(tester, '[^,]+', 1, level) IS NOT NULL
                  AND PRIOR dbms_random.value IS NOT NULL
                  AND PRIOR sys_guid() IS NOT NULL
                ORDER BY name", conn))
            {
                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    var name = rdr["name"]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(name))
                        testerList.Add(new { text = name, value = name });
                }
            }

            return Ok(new { testerTLs = tlList, testerNames = testerList });
        }

        [HttpGet("testers/{testerTL}")]
        public async Task<IActionResult> GetTestersByTL(string testerTL)
        {
            if (string.IsNullOrWhiteSpace(testerTL))
                return Ok(new List<object>());

            var tlToSubTeam = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "JIJIN E H", 1 }, { "MURUGESAN P", 2 }, { "NIKHIL SEKHAR", 3 },
                { "SMINA BENNY", 4 }, { "VISAGH S", 5 }, { "JOBY JOSE", 6 }
            };

            if (!tlToSubTeam.TryGetValue(testerTL.Trim(), out int subTeam))
                return Ok(new List<object>());

            var testers = new List<object>();
            using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();

            var cmd = new OracleCommand(@"
                SELECT DISTINCT v.emp_name
                FROM mana0809.srm_it_team_members t
                JOIN mana0809.employee_master v ON t.member_id = v.emp_code
                WHERE t.team_id = 6 AND t.sub_team = :subTeam AND v.status_id = 1
                ORDER BY v.emp_name", conn);
            cmd.Parameters.Add("subTeam", OracleDbType.Int32).Value = subTeam;

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var name = reader["emp_name"]?.ToString();
                if (!string.IsNullOrWhiteSpace(name))
                    testers.Add(new { text = name, value = name });
            }

            return Ok(testers);
        }

        [HttpGet("Attachment/{crfId}/{releaseDate}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAttachment(string crfId, string releaseDate)
        {
            if (string.IsNullOrWhiteSpace(crfId) || string.IsNullOrWhiteSpace(releaseDate))
                return BadRequest("Invalid parameters");

            if (!DateTime.TryParseExact(releaseDate, "dd-MMM-yyyy",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out DateTime relDate))
                return BadRequest("Invalid date format");

            using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();

            using var cmd = new OracleCommand(@"
        SELECT attachment, attachment_filename, attachment_mimetype
        FROM tbl_release_verify
        WHERE crf_id = :crfId
          AND TO_CHAR(release_dt, 'DD-MON-YYYY') = :releaseDateStr", conn);

            cmd.Parameters.Add("crfId", OracleDbType.Varchar2).Value = crfId.Trim().ToUpper();
            cmd.Parameters.Add("releaseDateStr", OracleDbType.Varchar2).Value = releaseDate.ToUpper();

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var blob = reader["attachment"] as OracleBlob;
                var fileName = reader["attachment_filename"]?.ToString() ?? "attachment";
                var mimeFromDb = reader["attachment_mimetype"]?.ToString();

                if (blob == null || blob.IsNull)
                    return NotFound("No attachment found");

                // CRITICAL FIX: Force refresh length
                long blobLength = blob.Length; // This line forces Oracle driver to read actual length
                if (blobLength == 0)
                    return Content("<h3>Attachment exists but BLOB appears empty. Please re-upload.</h3>", "text/html");

                var stream = new MemoryStream();
                await blob.CopyToAsync(stream);
                stream.Position = 0;

                string mimeType = mimeFromDb ?? GetMimeType(fileName);

                bool inline = mimeType.StartsWith("image/") ||
                              mimeType == "application/pdf" ||
                              mimeType.StartsWith("text/");

                return File(stream, mimeType, fileName, !inline);
            }

            return NotFound("No record found for this CRF and date");
        }

        private string GetMimeType(string fileName)
        {
            var extension = Path.GetExtension(fileName)?.ToLowerInvariant() ?? "";
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".pdf" => "application/pdf",
                ".txt" => "text/plain",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                _ => "application/octet-stream"
            };
        }

        // QA Verification Save (merged from old controller)
        [HttpPost("qa/save")]
        public async Task<IActionResult> SaveQAVerification([FromBody] VerificationRequest request)
        {
            var empCode = User.FindFirst(ClaimTypes.Name)?.Value;
            var empName = User.FindFirst(ClaimTypes.GivenName)?.Value ?? "Unknown";

            if (string.IsNullOrEmpty(empCode))
                return Unauthorized("User not authenticated");

            if (string.IsNullOrEmpty(request.CrfId) || string.IsNullOrEmpty(request.WorkingStatus))
                return BadRequest("CRF ID and Working Status are required");

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

            using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();

            using var cmd = new OracleCommand("proc_qa_verification", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.Add("p_crf_id", OracleDbType.Varchar2).Value = request.CrfId;
            cmd.Parameters.Add("p_working_status", OracleDbType.Varchar2).Value = request.WorkingStatus;
            cmd.Parameters.Add("p_remarks", OracleDbType.Varchar2).Value =
                string.IsNullOrEmpty(request.Remarks) ? (object)DBNull.Value :
                request.Remarks.Length > 4000 ? request.Remarks.Substring(0, 4000) : request.Remarks;

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
                    return Ok(new { success = true, message = "Verification saved successfully", verification_id = verId });
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
        [HttpGet("myalerts")]
        public async Task<IActionResult> GetMyAlerts()
        {
            var empCode = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst("unique_name")?.Value;

            if (string.IsNullOrEmpty(empCode))
                return Unauthorized();

            var alerts = new List<object>();

            using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();

            using var cmd = new OracleCommand(@"
        SELECT alert_message
        FROM mana0809.tbl_ma_common_alert
        WHERE emp_code = :empCode
          AND module_id = 3444566
          AND TRUNC(entr_dt) = TRUNC(SYSDATE)
          AND status = 1
        ORDER BY entr_dt DESC", conn);

            cmd.Parameters.Add("empCode", OracleDbType.Varchar2).Value = empCode;

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                alerts.Add(new
                {
                    message = reader["alert_message"]?.ToString() ?? ""
                });
            }

            return Ok(alerts);
        }
    }

    public class FilterModel
    {
        public required string fromDate { get; set; }
        public required string toDate { get; set; }
        public string? releaseType { get; set; }
        public string? testerTL { get; set; }
        public string? testerName { get; set; }
    }

    public class SaveModel
    {
        public required string crfId { get; set; }
        public required string requestId { get; set; }
        public string releaseDate { get; set; } = ""; // ← Now required for save
        public required string workingStatus { get; set; }
        public string remarks { get; set; } = "";
        public string? attachmentName { get; set; }
        public string? attachmentBase64 { get; set; }
        public string? attachmentMime { get; set; }
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