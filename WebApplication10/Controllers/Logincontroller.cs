//using Microsoft.AspNetCore.Mvc;
//using Oracle.ManagedDataAccess.Client;
//using WebApplication10.Models;
//using Microsoft.IdentityModel.Tokens;
//using System.IdentityModel.Tokens.Jwt;
//using System.Text;
//using System.Security.Claims;

//public class LoginController : Controller
//{
//    private readonly IConfiguration _configuration;
//    private readonly string _oracleConnectionString;

//    public LoginController(IConfiguration configuration)
//    {
//        _configuration = configuration;
//        _oracleConnectionString = _configuration.GetConnectionString("OracleConnection");
//    }

//    [HttpGet]
//    public IActionResult Index() => View();

//    [HttpPost]
//    public IActionResult Index(LoginModel model)
//    {
//        if (!ModelState.IsValid)
//            return View(model);

//        using (var connection = new OracleConnection(_oracleConnectionString))
//        {
//            connection.Open();
//            string query = "SELECT emp_code, password FROM employee_master WHERE emp_code = :EmpCode";

//            using (var cmd = new OracleCommand(query, connection))
//            {
//                cmd.Parameters.Add(new OracleParameter("Emp_code", model.EmpCode));
//                var reader = cmd.ExecuteReader();

//                if (reader.Read())
//                {
//                    string dbPassword = reader["PASSWORD"].ToString();

//                    // ✅ If passwords are plain text in DB (soft1234), compare directly
//                    if (dbPassword == model.Password)
//                    {
//                        var token = GenerateJwtToken(model.EmpCode);
//                        return Json(new { Token = token });
//                    }
//                    else
//                    {
//                        ViewBag.Error = "Invalid Password";
//                    }
//                }
//                else
//                {
//                    ViewBag.Error = "Employee not found";
//                }
//            }
//        }
//        return View(model);
//    }

//    private string GenerateJwtToken(string empCode)
//    {
//        var jwtSettings = _configuration.GetSection("Jwt");
//        var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]);

//        var tokenHandler = new JwtSecurityTokenHandler();
//        var tokenDescriptor = new SecurityTokenDescriptor
//        {
//            Subject = new ClaimsIdentity(new[]
//            {
//                new Claim(ClaimTypes.Name, empCode)
//            }),
//            Expires = DateTime.UtcNow.AddHours(1),
//            Issuer = jwtSettings["Issuer"],
//            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
//        };

//        var token = tokenHandler.CreateToken(tokenDescriptor);
//        return tokenHandler.WriteToken(token);
//    }
//}