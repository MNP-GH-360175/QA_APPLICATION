// File: Controllers/DailyVerificationController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using Newtonsoft.Json;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

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

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] FilterModel filters)
        {
            if (filters == null || string.IsNullOrWhiteSpace(filters.fromDate) || string.IsNullOrWhiteSpace(filters.toDate))
                return BadRequest("Dates required");

            var list = new List<object>();

            using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();

            using var cmd = new OracleCommand("proc_daily_release_verification", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.Add("p_from_date", OracleDbType.Date).Value = DateTime.ParseExact(filters.fromDate, "dd-MMM-yyyy", CultureInfo.InvariantCulture);
            cmd.Parameters.Add("p_to_date", OracleDbType.Date).Value = DateTime.ParseExact(filters.toDate, "dd-MMM-yyyy", CultureInfo.InvariantCulture);
            cmd.Parameters.Add("p_release_type", OracleDbType.Varchar2).Value = filters.releaseType ?? (object)DBNull.Value;
            cmd.Parameters.Add("p_tester_tl", OracleDbType.Varchar2).Value = filters.testerTL ?? (object)DBNull.Value;
            cmd.Parameters.Add("p_tester_name", OracleDbType.Varchar2).Value = filters.testerName ?? (object)DBNull.Value;
            cmd.Parameters.Add("p_result", OracleDbType.RefCursor).Direction = ParameterDirection.Output;

            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                list.Add(new
                {
                    CRF_ID = rdr["CRF_ID"]?.ToString() ?? "",
                    REQUEST_ID = rdr["REQUEST_ID"]?.ToString() ?? "",
                    CRF_NAME = rdr["CRF_NAME"]?.ToString() ?? "No Title",
                    RELEASE_DATE = rdr["RELEASE_DATE"]?.ToString() ?? "",
                    TECHLEAD_NAME = rdr["TECHLEAD_NAME"]?.ToString() ?? "Not Assigned",
                    DEVELOPER_NAME = rdr["DEVELOPER_NAME"]?.ToString() ?? "Not Assigned",
                    TESTER_NAME = rdr["TESTER_NAME"]?.ToString() ?? "Not Assigned",
                    TESTER_TL_NAME = rdr["TESTER_TL_NAME"]?.ToString() ?? "Not Assigned",
                    RELEASE_TYPE_TEXT = rdr["RELEASE_TYPE_TEXT"]?.ToString() ?? "",
                    CURRENT_STATUS = rdr["CURRENT_STATUS"]?.ToString(),
                    CURRENT_REMARKS = rdr["CURRENT_REMARKS"]?.ToString(),
                    CURRENT_ATTACHMENT_NAME = rdr["CURRENT_ATTACHMENT_NAME"]?.ToString(),
                    CURRENT_VERIFIED_ON = rdr["CURRENT_VERIFIED_ON"]?.ToString(),
                    HISTORY_JSON = rdr["HISTORY_JSON"]?.ToString() ?? "[]"
                });
            }
            return Ok(list);
        }

        [HttpPost("Save")]
        public async Task<IActionResult> Save([FromBody] List<SaveModel> items)
        {
            if (items == null || items.Count == 0) return BadRequest("No data");

            var userName = User.FindFirst(ClaimTypes.GivenName)?.Value ??
                          User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";

            using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();

            using var cmd = new OracleCommand("PROC_DAILY_VERIFICATION", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.Add("p_json_input", OracleDbType.Clob).Value = JsonConvert.SerializeObject(items);
            cmd.Parameters.Add("p_updated_by", OracleDbType.Varchar2).Value = userName;

            await cmd.ExecuteNonQueryAsync();
            return Ok("Saved successfully");
        }
        [HttpGet("filters")]
        public async Task<IActionResult> GetFilters()
        {
            var tlList = new List<object>();
            var testerList = new List<object>();

            using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();

            // Tester TLs
            using (var cmd = new OracleCommand(@"
        SELECT DISTINCT est.emp_name AS name
        FROM mana0809.srm_dailyrelease_updn n
        JOIN mana0809.srm_testing st ON st.request_id = n.request_id
        JOIN mana0809.employee_master est ON est.emp_code = st.test_lead
        ORDER BY est.emp_name", conn))
            {
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    tlList.Add(new { text = r["name"]?.ToString(), value = r["name"]?.ToString() });
            }

            // Tester Names
            using (var cmd = new OracleCommand(@"
        SELECT DISTINCT ets.emp_name AS name
        FROM mana0809.srm_dailyrelease_updn n
        JOIN mana0809.srm_test_assign ts ON ts.request_id = n.request_id
        JOIN mana0809.employee_master ets ON ets.emp_code = ts.assign_to
        ORDER BY ets.emp_name", conn))
            {
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    testerList.Add(new { text = r["name"]?.ToString(), value = r["name"]?.ToString() });
            }

            return Ok(new { testerTLs = tlList, testerNames = testerList });
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
        public required string workingStatus { get; set; }
        public string remarks { get; set; } = "";
        public string? attachmentName { get; set; }
        public string? attachmentBase64 { get; set; }
        public string? attachmentMime { get; set; }
    }
}