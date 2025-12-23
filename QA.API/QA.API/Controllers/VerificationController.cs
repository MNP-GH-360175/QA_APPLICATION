    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Oracle.ManagedDataAccess.Client;
    using System.Data;
    using System.Security.Claims;
    using Oracle.ManagedDataAccess.Types;
    using Newtonsoft.Json;
using QA.API.Models;
namespace QA.API.Controllers
    {
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class VerificationController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly string _connStr;
        private readonly ILogger<VerificationController> _logger;

        public VerificationController(IConfiguration config, ILogger<VerificationController> logger)
        {
            _config = config;
            _connStr = _config.GetConnectionString("OracleConnection")!;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] FilterModel filters)
        {
            if (filters == null)
                return BadRequest("Invalid request body");

            // Get current user from JWT token
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
            cmd.Parameters.Add("p_release_type", OracleDbType.Varchar2).Value = filters.releaseType;
            cmd.Parameters.Add("p_tester_tl", OracleDbType.Varchar2).Value = DBNull.Value;
            cmd.Parameters.Add("p_tester_name", OracleDbType.Varchar2).Value = currentTester; // From JWT
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

        [HttpPost("Save")]
        public async Task<IActionResult> Save([FromBody] List<SaveModel> items)
        {
            if (items == null || items.Count == 0)
                return BadRequest("No data to save");

            var userName = User.FindFirst(ClaimTypes.GivenName)?.Value ??
                           User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown User";

            using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();

            using var cmd = new OracleCommand("PROC_DAILY_REPORTS_MASTER", conn)
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

        [HttpGet("filters")]
        public async Task<IActionResult> GetFilters()
        {
            var tlList = new List<object>();
            var testerList = new List<object>();

            using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();

            // TL List
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

            // Tester List
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
                System.Globalization.DateTimeStyles.None, out _))
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

                if (blob == null || blob.IsNull || blob.Length == 0)
                    return NotFound("No attachment found or empty");

                long blobLength = blob.Length; // forces fetch
                var stream = new MemoryStream();
                await blob.CopyToAsync(stream);
                stream.Position = 0;

                string mimeType = mimeFromDb ?? GetMimeType(fileName);
                bool inline = mimeType.StartsWith("image/") || mimeType == "application/pdf" || mimeType.StartsWith("text/");

                return File(stream, mimeType, fileName, !inline);
            }

            return NotFound("No record found");
        }

        private string GetMimeType(string fileName)
        {
            var ext = Path.GetExtension(fileName)?.ToLowerInvariant() ?? "";
            return ext switch
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
                alerts.Add(new { message = reader["alert_message"]?.ToString() ?? "" });
            }

            return Ok(alerts);
        }
        // Add this method to your existing VerificationController
        [HttpGet("formaccess")]
        public async Task<IActionResult> CheckFormAccess()
        {
            // Get emp_code from JWT (NameIdentifier is usually emp_code)
            var empCode = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst("unique_name")?.Value
                          ?? "UNKNOWN";

            if (string.IsNullOrEmpty(empCode))
                return Ok(new { hasAccess = false });

            using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();

            using var cmd = new OracleCommand("PROC_DAILY_VERIFICATION_MASTER", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.Add("p_flag", OracleDbType.Varchar2).Value = "FORM_ACCESS";
            cmd.Parameters.Add("p_from_date", OracleDbType.Varchar2).Value = DBNull.Value;
            cmd.Parameters.Add("p_to_date", OracleDbType.Varchar2).Value = DBNull.Value;
            cmd.Parameters.Add("p_release_type", OracleDbType.Varchar2).Value = DBNull.Value;
            cmd.Parameters.Add("p_tester_tl", OracleDbType.Varchar2).Value = DBNull.Value;
            cmd.Parameters.Add("p_tester_name", OracleDbType.Varchar2).Value = DBNull.Value;
            cmd.Parameters.Add("p_json_input", OracleDbType.Clob).Value = DBNull.Value;
            cmd.Parameters.Add("p_updated_by", OracleDbType.Varchar2).Value = empCode;
            cmd.Parameters.Add("p_result", OracleDbType.RefCursor).Direction = ParameterDirection.Output;

            using var reader = await cmd.ExecuteReaderAsync();

            bool hasAccess = false;
            if (await reader.ReadAsync())
            {
                hasAccess = reader.GetInt32(0) == 1; // ACCESS_GRANTED = 1
            }

            return Ok(new { hasAccess });
        }
        // GET api/Verification/tlview - Tech Lead View (replaces old TL_VERIFY)
        [HttpPost("tlview")]
        public async Task<IActionResult> TLView([FromBody] FilterModel filters)
        {
            if (filters == null)
                return BadRequest("Invalid filters");

            var currentTL = User.FindFirst(ClaimTypes.GivenName)?.Value ??
                            User.FindFirst(ClaimTypes.Name)?.Value ??
                            User.FindFirst("unique_name")?.Value ?? "";

            if (string.IsNullOrWhiteSpace(currentTL))
                return Unauthorized();

            var list = new List<object>();

            using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();

            using var cmd = new OracleCommand("PROC_DAILY_VERIFICATION_MASTER", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.Add("p_flag", OracleDbType.Varchar2).Value = "TL_VERIFY";
            cmd.Parameters.Add("p_from_date", OracleDbType.Varchar2).Value = filters.fromDate;
            cmd.Parameters.Add("p_to_date", OracleDbType.Varchar2).Value = filters.toDate;
            cmd.Parameters.Add("p_release_type", OracleDbType.Varchar2).Value = filters.releaseType;
            cmd.Parameters.Add("p_tester_tl", OracleDbType.Varchar2).Value = currentTL; // Important: filter by TL name
            cmd.Parameters.Add("p_tester_name", OracleDbType.Varchar2).Value = filters.testerName;
            cmd.Parameters.Add("p_json_input", OracleDbType.Clob).Value = DBNull.Value;
            cmd.Parameters.Add("p_updated_by", OracleDbType.Varchar2).Value = currentTL;
            cmd.Parameters.Add("p_result", OracleDbType.RefCursor).Direction = ParameterDirection.Output;

            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                list.Add(new
                {
                    CRF_ID = rdr["CRF_ID"]?.ToString() ?? "",
                    REQUEST_ID = rdr["REQUEST_ID"]?.ToString() ?? "",
                    CRF_NAME = rdr["CRF_NAME"]?.ToString() ?? "",
                    RELEASE_DATE = rdr["RELEASE_DATE"]?.ToString() ?? "",
                    TECHLEAD = rdr["TECHLEAD"]?.ToString() ?? "",
                    DEVELOPER = rdr["DEVELOPER"]?.ToString() ?? "",
                    TESTER = rdr["TESTER"]?.ToString() ?? "",
                    TESTER_TL = rdr["TESTER_TL"]?.ToString() ?? "",
                    WORKING_STATUS = rdr["WORKING_STATUS"]?.ToString() ?? "",
                    WORKING_STATUS_TEXT = rdr["WORKING_STATUS_TEXT"]?.ToString() ?? "",
                    REMARKS = rdr["REMARKS"]?.ToString() ?? "",
                    TL_REMARKS = rdr["TL_REMARKS"]?.ToString() ?? "",
                    ATTACHMENT_NAME = rdr["ATTACHMENT_NAME"]?.ToString() ?? "",
                    ATTACHMENT_MIME = rdr["ATTACHMENT_MIME"]?.ToString() ?? "application/octet-stream",
                    TESTER_VERIFIED_BY = rdr["TESTER_VERIFIED_BY"]?.ToString() ?? "",
                    TESTER_VERIFIED_ON = rdr["TESTER_VERIFIED_ON"]?.ToString() ?? "",
                    TL_VERIFIED_BY = rdr["TL_VERIFIED_BY"]?.ToString() ?? "",
                    TL_VERIFIED_ON = rdr["TL_VERIFIED_ON"]?.ToString() ?? "",
                    VERIFY_STATUS = rdr["VERIFY_STATUS"] != DBNull.Value ? Convert.ToInt32(rdr["VERIFY_STATUS"]) : 0
                });
            }

            return Ok(list);
        }


        // POST api/Verification/tlsave - Tech Lead Save (Confirm/Return/Close)
        [HttpPost("tlsave")]
        public async Task<IActionResult> TLSave([FromBody] List<TLSaveModel> items)
        {
            if (items == null || items.Count == 0)
                return BadRequest("No items to save");

            var currentTL = User.FindFirst(ClaimTypes.GivenName)?.Value ??
                            User.FindFirst(ClaimTypes.Name)?.Value ??
                            User.FindFirst("unique_name")?.Value ?? "UNKNOWN";

            using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();

            using var cmd = new OracleCommand("PROC_DAILY_VERIFICATION_MASTER", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.Add("p_flag", OracleDbType.Varchar2).Value = "TL_SAVE";
            cmd.Parameters.Add("p_from_date", OracleDbType.Varchar2).Value = DBNull.Value;
            cmd.Parameters.Add("p_to_date", OracleDbType.Varchar2).Value = DBNull.Value;
            cmd.Parameters.Add("p_release_type", OracleDbType.Varchar2).Value = DBNull.Value;
            cmd.Parameters.Add("p_tester_tl", OracleDbType.Varchar2).Value = DBNull.Value;
            cmd.Parameters.Add("p_tester_name", OracleDbType.Varchar2).Value = DBNull.Value;
            cmd.Parameters.Add("p_json_input", OracleDbType.Clob).Value = JsonConvert.SerializeObject(items);
            cmd.Parameters.Add("p_updated_by", OracleDbType.Varchar2).Value = currentTL;
            cmd.Parameters.Add("p_result", OracleDbType.RefCursor).Direction = ParameterDirection.Output;

            await cmd.ExecuteNonQueryAsync();

            return Ok(new { message = "TL action completed successfully" });
        }


        [HttpGet("team/{subTeamId:int}")]
        public async Task<IActionResult> GetTeamMembers(int subTeamId)
        {
            if (subTeamId < 1 || subTeamId > 6)
                return BadRequest("Invalid sub_team ID. Must be between 1 and 6.");

            var testers = new List<object>();

            try
            {
                using var conn = new OracleConnection(_connStr);
                await conn.OpenAsync();

                var cmd = new OracleCommand(@"
                    SELECT DISTINCT v.emp_name AS tester_name,
                                    v.emp_code AS tester_code
                    FROM mana0809.srm_it_team_members t
                    JOIN mana0809.employee_master v ON t.member_id = v.emp_code
                    WHERE t.team_id = 6
                      AND t.sub_team = :subTeam
                      AND v.status_id = 1
                    ORDER BY v.emp_name", conn);

                cmd.Parameters.Add("subTeam", OracleDbType.Int32).Value = subTeamId;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var name = reader["tester_name"]?.ToString()?.Trim();
                    var code = reader["tester_code"]?.ToString()?.Trim();

                    if (!string.IsNullOrEmpty(name))
                    {
                        testers.Add(new
                        {
                            text = name,
                            value = code ?? name  // fallback to name if code is null
                        });
                    }
                }

                _logger.LogInformation("Loaded {Count} testers for sub_team {SubTeamId}", testers.Count, subTeamId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading team members for sub_team {SubTeamId}", subTeamId);
                return StatusCode(500, "Error loading team members");
            }

            return Ok(testers);
        }

    }

 }  