using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace QA.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly string _oracleConnectionString;

        public AuthController(IConfiguration configuration)
        {
            _configuration = configuration;
            _oracleConnectionString = configuration.GetConnectionString("OracleConnection")
                ?? throw new InvalidOperationException("OracleConnection connection string is missing.");
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.EmpCode) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(new { message = "EmpCode and Password are required" });
            }

            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                connection.Open();

                const string query = "SELECT emp_code, emp_name, PASSWORD FROM EMPLOYEE_MASTER WHERE emp_code = :EmpCode";
                using var cmd = new OracleCommand(query, connection);
                cmd.Parameters.Add("EmpCode", OracleDbType.Varchar2).Value = request.EmpCode;

                using var reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    string dbPassword = reader["PASSWORD"]?.ToString() ?? "";
                    string empName = reader["emp_name"]?.ToString() ?? "Unknown User";
                    string empCode = reader["emp_code"]?.ToString() ?? "";

                    // Plain text comparison (as per central system design)
                    if (dbPassword == request.Password)
                    {
                        var token = GenerateJwtToken(empCode, empName);
                        return Ok(new
                        {
                            token,
                            empName,
                            empCode
                        });
                    }
                }

                return Unauthorized(new { message = "Invalid Employee Code or Password" });
            }
            catch (Exception ex)
            {
                // Log in real app (ILogger if injected), but safe fallback
                System.Diagnostics.Debug.WriteLine($"Login error: {ex.Message}");

                // Do NOT expose real error in production
                return StatusCode(500, new { message = "An internal error occurred. Please try again later." });
            }
        }

        private string GenerateJwtToken(string empCode, string empName)
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            var keyString = jwtSettings["Key"]
                ?? throw new InvalidOperationException("JWT Key is missing in configuration.");

            if (keyString.Length < 32)
                throw new InvalidOperationException("JWT Key is too weak (minimum 256 bits recommended).");

            var key = Encoding.UTF8.GetBytes(keyString); // Better than ASCII

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, empCode),
                new Claim(ClaimTypes.Name, empName),
                new Claim(ClaimTypes.GivenName, empName),
                new Claim(JwtRegisteredClaimNames.Sub, empCode),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(8),
                Issuer = jwtSettings["Issuer"],
                Audience = jwtSettings["Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }

    public class LoginRequest
    {
        public string EmpCode { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}