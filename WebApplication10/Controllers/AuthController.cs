using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace WebApplication10.Controllers
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
            _oracleConnectionString = _configuration.GetConnectionString("OracleConnection");
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrEmpty(request.EmpCode) || string.IsNullOrEmpty(request.Password))
                return BadRequest(new { message = "EmpCode and Password are required" });

            using var connection = new OracleConnection(_oracleConnectionString);
            connection.Open();

            const string query = "SELECT emp_code, emp_name, PASSWORD FROM EMPLOYEE_MASTER WHERE emp_code = :EmpCode";
            using var cmd = new OracleCommand(query, connection);
            cmd.Parameters.Add("EmpCode", OracleDbType.Varchar2).Value = request.EmpCode;

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                string dbPassword = reader["PASSWORD"]?.ToString() ?? "";
                string empCode = reader["emp_code"]?.ToString() ?? "";
                string empName = reader["emp_name"]?.ToString() ?? "Unknown User";

                // Simple password check (upgrade to hashing later!)
                if (dbPassword == request.Password)
                {
                    var token = GenerateJwtToken(empCode, empName);

                    return Ok(new
                    {
                        token,
                        empName,     // THIS WAS THE FIX!
                        empCode
                    });
                }
            }

            return Unauthorized(new { message = "Invalid EmpCode or Password" });
        }

        private string GenerateJwtToken(string empCode, string empName)
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]!);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, empCode),
                new Claim(ClaimTypes.Name, empCode),
                new Claim(ClaimTypes.GivenName, empName),
                new Claim(JwtRegisteredClaimNames.Sub, empCode),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(8),
                Issuer = jwtSettings["Issuer"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        [Authorize]
        [HttpGet("dashboard")]
        public IActionResult Dashboard()
        {
            var empName = User.FindFirst(ClaimTypes.GivenName)?.Value ?? "User";
            return Ok(new { Message = $"Welcome {empName}!", User = User.Identity?.Name });
        }
    }

    public class LoginRequest
    {
        public string EmpCode { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}