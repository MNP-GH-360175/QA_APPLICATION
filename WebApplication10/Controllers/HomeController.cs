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

            // Dictionary to map sub_team -> TL Name (only once, maintainable)
            var subTeamToTL = new Dictionary<int, string>
    {
        { 1, "JIJIN E H" },
        { 2, "MURUGESAN P" },
        { 3, "NIKHIL SEKHAR" },
        { 4, "SMINA BENNY" },
        { 5, "VISAGH S" },
        { 6, "JOBY JOSE" } // if needed
    };

            using (var conn = new OracleConnection(_connStr))
            {
                conn.Open();
                var cmd = new OracleCommand(@"
            SELECT DISTINCT 
                CASE
                    WHEN t.sub_team = 1 THEN 'JIJIN E H'
                    WHEN t.sub_team = 2 THEN 'MURUGESAN P'
                    WHEN t.sub_team = 3 THEN 'NIKHIL SEKHAR'
                    WHEN t.sub_team = 4 THEN 'SMINA BENNY'
                    WHEN t.sub_team = 5 THEN 'VISAGH S'
                    WHEN t.sub_team = 6 THEN 'JOBY JOSE'
                    ELSE NULL
                END AS tester_tl_name
            FROM mana0809.srm_it_team_members t
            JOIN mana0809.employee_master v ON t.member_id = v.emp_code
            JOIN mana0809.srm_it_team c ON c.team_id = t.team_id
            WHERE v.status_id = 1
              AND t.team_id = 6  -- QA Team
              AND t.sub_team IS NOT NULL
              AND t.sub_team IN (1,2,3,4,5,6)
            ORDER BY tester_tl_name", conn);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var tlName = reader["tester_tl_name"].ToString();
                        if (!string.IsNullOrWhiteSpace(tlName) && !list.Any(x => x.Value == tlName))
                        {
                            list.Add(new SelectListItem
                            {
                                Text = tlName,
                                Value = tlName
                            });
                        }
                    }
                }
            }

            return list.OrderBy(x => x.Text).ToList();
        }

        
        
    }
}