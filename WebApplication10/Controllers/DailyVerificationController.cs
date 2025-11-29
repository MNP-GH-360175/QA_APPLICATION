using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;

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
            _connStr = _config.GetConnectionString("OracleConnection"); // Same as Auth
        }

        [HttpPost]
        //[Authorize]
        [AllowAnonymous]
        public async Task<IActionResult> GetReport([FromBody] FilterModel filters)
        {
            if (filters == null || string.IsNullOrEmpty(filters.fromDate) || string.IsNullOrEmpty(filters.toDate))
                return BadRequest("FromDate and ToDate are required.");

            var result = new List<object>();

            using (var conn = new OracleConnection(_connStr))
            {
                await conn.OpenAsync();
                using (var cmd = new OracleCommand("GET_DAILY_RELEASE_VERIFICATION", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add("p_from_date", OracleDbType.Date).Value = DateTime.ParseExact(filters.fromDate, "dd-MMM-yyyy", null);
                    cmd.Parameters.Add("p_to_date", OracleDbType.Date).Value = DateTime.ParseExact(filters.toDate, "dd-MMM-yyyy", null);
                    cmd.Parameters.Add("p_release_type", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(filters.releaseType) ? (object)DBNull.Value : filters.releaseType;
                    cmd.Parameters.Add("p_tester_tl", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(filters.testerTL) ? (object)DBNull.Value : filters.testerTL.Trim().ToUpper();
                    cmd.Parameters.Add("p_tester_name", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(filters.testerName) ? (object)DBNull.Value : filters.testerName.Trim().ToUpper();

                    var pResult = cmd.Parameters.Add("p_result", OracleDbType.RefCursor);
                    pResult.Direction = ParameterDirection.Output;

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Add(new
                            {
                                crfId = reader["CRF_ID"].ToString(),
                                subject = reader["CRF_NAME"]?.ToString() ?? "",
                                releaseDate = reader["RELEASE_DATE"]?.ToString(), // Already "31-OCT-2025 14:39"
                                releaseType = reader["RELEASE_TYPE"]?.ToString(),
                                developerTL = reader["DEVELOPER_TL_NAME"]?.ToString() ?? "Not Assigned",
                                developer = reader["DEVELOPER_NAME"]?.ToString() ?? "Not Assigned",
                                testerTL = reader["TESTER_TL_NAME"]?.ToString() ?? "Not Assigned",
                                tester = reader["TESTER_NAME"]?.ToString() ?? "Not Assigned",
                                workingStatus = (string)null,
                                remarks = "",
                                attachmentName = "",
                                attachmentBase64 = "",
                                isConfirmed = false,
                                History = new List<object>()
                            });
                        }
                    }
                }
            }

            return Ok(result);
        }
    }

    public class FilterModel
    {
        public string releaseType { get; set; }
        public string fromDate { get; set; }
        public string toDate { get; set; }
        public string testerTL { get; set; }
        public string testerName { get; set; }
    }
}