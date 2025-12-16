// File: Controllers/DailyVerificationController.cs
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
    public class DailyVerificationController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly string _connStr;

        public DailyVerificationController(IConfiguration config)
        {
            _config = config;
            _connStr = _config.GetConnectionString("OracleConnection");
        }

        // FETCH: Get all CRFs with verification status
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] FilterModel filters)
        {
            if (filters == null || string.IsNullOrWhiteSpace(filters.fromDate) || string.IsNullOrWhiteSpace(filters.toDate))
                return BadRequest("Dates required");

            var list = new List<object>();

            using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();

            using var cmd = new OracleCommand("PROC_DAILY_VERIFICATION_MASTER", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.Add("p_flag", OracleDbType.Varchar2).Value = "FETCH";
            cmd.Parameters.Add("p_from_date", OracleDbType.Varchar2).Value = filters.fromDate;
            cmd.Parameters.Add("p_to_date", OracleDbType.Varchar2).Value = filters.toDate;
            cmd.Parameters.Add("p_release_type", OracleDbType.Varchar2).Value = filters.releaseType ?? (object)DBNull.Value;
            cmd.Parameters.Add("p_tester_tl", OracleDbType.Varchar2).Value = filters.testerTL ?? (object)DBNull.Value;
            cmd.Parameters.Add("p_tester_name", OracleDbType.Varchar2).Value = filters.testerName ?? (object)DBNull.Value;
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

        // SAVE: Save verification + insert into history table
        [HttpPost("Save")]
        public async Task<IActionResult> Save([FromBody] List<SaveModel> items)
        {
            if (items == null || items.Count == 0)
                return BadRequest("No data to save");

            var userName = User.FindFirst(ClaimTypes.GivenName)?.Value ??
                           User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown User";

            using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();

            // Only call the master procedure — it handles BOTH insert/update AND history archiving
            using var cmd = new OracleCommand("PROC_DAILY_VERIFICATION_MASTER", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.Add("p_flag", OracleDbType.Varchar2).Value = "SAVE";
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

            return Ok(new
            {
                testerTLs = tlList,
                testerNames = testerList
            });
        }
        [HttpGet("Attachment/{crfId}/{releaseDate}")]
        public async Task<IActionResult> GetAttachment(string crfId, string releaseDate)
        {
            if (string.IsNullOrWhiteSpace(crfId) || string.IsNullOrWhiteSpace(releaseDate))
                return BadRequest("Invalid parameters");

            // Parse releaseDate from "16-DEC-2025" format
            if (!DateTime.TryParseExact(releaseDate, "dd-MMM-yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime relDate))
                return BadRequest("Invalid date format");

            using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();

            using var cmd = new OracleCommand(@"
        SELECT attachment, attachment_filename, attachment_mimetype
        FROM tbl_release_verify
        WHERE crf_id = :crfId
          AND TRUNC(release_dt) = :releaseDt", conn);

            cmd.Parameters.Add("crfId", OracleDbType.Varchar2).Value = crfId;
            cmd.Parameters.Add("releaseDt", OracleDbType.Date).Value = relDate.Date;

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var blob = reader["attachment"] as OracleBlob;
                var fileName = reader["attachment_filename"]?.ToString() ?? "attachment";
                var mime = reader["attachment_mimetype"]?.ToString() ?? "application/octet-stream";

                if (blob == null || blob.IsNull || blob.Length == 0)
                    return NotFound("No attachment found");

                var stream = new MemoryStream();
                await blob.CopyToAsync(stream);
                stream.Position = 0;

                // This enables download + allows inline view for browser-supported types (PDF, images, etc.)
                return File(stream, mime, fileName);
            }

            return NotFound("Attachment not found");
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
        public required string requestId { get; set; }  // <-- THIS WAS MISSING!
        public required string workingStatus { get; set; }
        public string remarks { get; set; } = "";
        public string? attachmentName { get; set; }
        public string? attachmentBase64 { get; set; }
        public string? attachmentMime { get; set; }
    }
}