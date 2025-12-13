using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.IdentityModel.Tokens;
using Oracle.ManagedDataAccess.Client;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace WebApplication10.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration _config;
        private readonly string _connStr;

        public HomeController(ILogger<HomeController> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
            _connStr = _config.GetConnectionString("OracleConnection");
        }

        [AllowAnonymous]
        public IActionResult Index() => View();

        [AllowAnonymous]
        public IActionResult Login() => View();

        // PROTECTED PAGES - NOW WITH SERVER-SIDE AUTH CHECK
        private bool IsAuthenticated()
        {
            var token = Request.Cookies["jwtToken"];  // Now from cookie!

            if (string.IsNullOrEmpty(token))
                return false;

            try
            {
                var handler = new JwtSecurityTokenHandler();
                var validations = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"])),
                    ValidateIssuer = true,
                    ValidIssuer = _config["Jwt:Issuer"],
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                handler.ValidateToken(token, validations, out _);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private IActionResult RedirectToLogin()
        {
            return RedirectToAction("Login", "Home");
        }

        public IActionResult Dashboard()
        {
            if (!IsAuthenticated())
                return RedirectToLogin();

            return View();
        }

        public IActionResult DailyVerification()
        {
            if (!IsAuthenticated())
                return RedirectToLogin();

            return View();
        }

        public IActionResult ReleaseStatus()
        {
            if (!IsAuthenticated())
                return RedirectToLogin();

            ViewBag.TesterTLs = GetTesterTLs();
            ViewBag.TesterNames = GetTesterNames();
            return View();
        }

        [AllowAnonymous]
        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() => View(new { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

        // Get real Tester Team Leads from srm_testing.test_lead
        private List<SelectListItem> GetTesterTLs()
        {
            var list = new List<SelectListItem>
            {
                new SelectListItem { Text = "All TLs", Value = "" }
            };
            using (var conn = new OracleConnection(_connStr))
            {
                conn.Open();
                var cmd = new OracleCommand(@"
                    SELECT DISTINCT e.emp_name
                    FROM mana0809.srm_testing st
                    JOIN mana0809.employee_master e ON st.test_lead = e.emp_code
                    WHERE st.test_lead IS NOT NULL
                    ORDER BY e.emp_name", conn);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new SelectListItem
                        {
                            Text = reader["emp_name"].ToString(),
                            Value = reader["emp_name"].ToString()
                        });
                    }
                }
            }
            var knownTLs = new[] { "NIKHIL SEKHAR", "VISAGH S", "JIJIN E H", "MURUGESAN P", "JOBY JOSE" };
            foreach (var tl in knownTLs)
            {
                if (!list.Any(x => x.Value == tl))
                    list.Add(new SelectListItem { Text = tl, Value = tl });
            }
            return list.OrderBy(x => x.Text).ToList();
        }

        private List<SelectListItem> GetTesterNames()
        {
            var list = new List<SelectListItem>
            {
                new SelectListItem { Text = "All Testers", Value = "" }
            };
            using (var conn = new OracleConnection(_connStr))
            {
                conn.Open();
                var cmd = new OracleCommand(@"
                    SELECT DISTINCT e.emp_name
                    FROM mana0809.srm_test_assign ta
                    JOIN mana0809.employee_master e ON ta.assign_to = e.emp_code
                    WHERE ta.assign_to IS NOT NULL
                    ORDER BY e.emp_name", conn);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new SelectListItem
                        {
                            Text = reader["emp_name"].ToString(),
                            Value = reader["emp_name"].ToString()
                        });
                    }
                }
            }
            return list.OrderBy(x => x.Text).ToList();
        }
    }
}