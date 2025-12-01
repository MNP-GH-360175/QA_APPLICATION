using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using Newtonsoft.Json;
using System.Globalization;

namespace WebApplication10.Controllers
{
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
                return BadRequest("From Date and To Date are required.");

            var result = new List<object>();

            using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();

            using var cmd = new OracleCommand("GET_DAILY_RELEASE_VERIFICATION", conn);
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.Add("p_from_date", OracleDbType.Date).Value =
                DateTime.ParseExact(filters.fromDate, "dd-MMM-yyyy", CultureInfo.InvariantCulture);
            cmd.Parameters.Add("p_to_date", OracleDbType.Date).Value =
                DateTime.ParseExact(filters.toDate, "dd-MMM-yyyy", CultureInfo.InvariantCulture);
            cmd.Parameters.Add("p_release_type", OracleDbType.Varchar2).Value = (object)filters.releaseType ?? DBNull.Value;
            cmd.Parameters.Add("p_tester_tl", OracleDbType.Varchar2).Value = (object)filters.testerTL ?? DBNull.Value;
            cmd.Parameters.Add("p_tester_name", OracleDbType.Varchar2).Value = (object)filters.testerName ?? DBNull.Value;
            cmd.Parameters.Add("p_result", OracleDbType.RefCursor).Direction = ParameterDirection.Output;

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new
                {
                    CRF_ID = reader["CRF_ID"]?.ToString() ?? "",
                    CRF_NAME = reader["CRF_NAME"]?.ToString() ?? "Not Available",
                    RELEASE_DATE = reader["RELEASE_DATE"]?.ToString() ?? "",
                    RELEASE_TYPE_TEXT = reader["RELEASE_TYPE_TEXT"]?.ToString() ?? "",
                    DEVELOPER_NAME = reader["DEVELOPER_NAME"]?.ToString() ?? "Not Assigned",
                    TESTER_NAME = reader["TESTER_NAME"]?.ToString() ?? "Not Assigned",
                    TESTER_TL_NAME = reader["TESTER_TL_NAME"]?.ToString() ?? "Not Assigned",
                    CURRENT_STATUS = reader["CURRENT_STATUS"]?.ToString(),
                    CURRENT_REMARKS = reader["CURRENT_REMARKS"]?.ToString(),
                    CURRENT_ATTACHMENT_NAME = reader["CURRENT_ATTACHMENT_NAME"]?.ToString(),
                    CURRENT_VERIFIED_BY = reader["CURRENT_VERIFIED_BY"]?.ToString(),
                    CURRENT_VERIFIED_ON = reader["CURRENT_VERIFIED_ON"]?.ToString(),
                    HISTORY_JSON = reader["HISTORY_JSON"]?.ToString() ?? "[]"
                });
            }

            return Ok(result);
        }

        [HttpPost("Save")]
        public async Task<IActionResult> Save([FromBody] List<VerificationSaveModel> items)
        {
            if (items == null || items.Count == 0)
                return BadRequest("No data to save.");

            using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();

            using var cmd = new OracleCommand("PROC_DAILY_VERIFICATION", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            string currentUser = User?.Identity?.Name ?? "Unknown User";

            var jsonParam = new OracleParameter("p_json_input", OracleDbType.Clob)
            {
                Value = JsonConvert.SerializeObject(items)
            };
            var userParam = new OracleParameter("p_updated_by", OracleDbType.Varchar2, 100)
            {
                Value = currentUser
            };

            cmd.Parameters.Add(jsonParam);
            cmd.Parameters.Add(userParam);

            try
            {
                await cmd.ExecuteNonQueryAsync();
                return Ok("Saved successfully");
            }
            catch (Exception ex)
            {
                return BadRequest("Oracle Error: " + ex.Message);
            }
        }
    }

    // MODELS — FINAL & CORRECT
    public class FilterModel
    {
        public required string fromDate { get; set; }
        public required string toDate { get; set; }
        public string? releaseType { get; set; }
        public string? testerTL { get; set; }
        public string? testerName { get; set; }
    }

    public class VerificationSaveModel
    {
        public required string crfId { get; set; }
        public required string workingStatus { get; set; }
        public required string remarks { get; set; }

        public string? attachmentName { get; set; }
        public string? attachmentBase64 { get; set; }
        public string? attachmentMime { get; set; }
        public string? verifiedBy { get; set; }
    }
}