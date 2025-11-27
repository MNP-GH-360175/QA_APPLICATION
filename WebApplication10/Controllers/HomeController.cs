using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Diagnostics;
using WebApplication10.Models;

namespace WebApplication10.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        public HomeController(ILogger<HomeController> logger) => _logger = logger;

        [AllowAnonymous]
        public IActionResult Index() => View();

        [AllowAnonymous]
        public IActionResult Login() => View();  // ← Now finds Views/Home/Login.cshtml

        [AllowAnonymous] // ← Important: View itself is public, JS protects content
        public IActionResult Dashboard() => View();

        [AllowAnonymous]
        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }


        [AllowAnonymous]
        public IActionResult DailyVerification()
        {
            ViewBag.TesterTLs = new List<SelectListItem>
    {
        new SelectListItem { Text = "NIKHIL SEKHAR", Value = "NIKHIL SEKHAR" },
        new SelectListItem { Text = "VISAGH S", Value = "VISAGH S" },
        new SelectListItem { Text = "JIJIN E H", Value = "JIJIN E H" },
        new SelectListItem { Text = "MURUGESAN P", Value = "MURUGESAN P" }
    };

            ViewBag.TesterNames = new List<SelectListItem>
    {
        new SelectListItem { Text = "Rajendran N", Value = "Rajendran N" },
        new SelectListItem { Text = "Aditya T S", Value = "Aditya T S" },
        new SelectListItem { Text = "Anjitha K A", Value = "Anjitha K A" },
        new SelectListItem { Text = "Aswathy Chandran", Value = "Aswathy Chandran" },
        new SelectListItem { Text = "Kala Chandran", Value = "Kala Chandran" },
        new SelectListItem { Text = "Seethal V S", Value = "Seethal V S" }
    };

            return View();
        }

        [AllowAnonymous]
        public IActionResult ReleaseStatus()
        {
            ViewBag.TesterTLs = new List<SelectListItem>
    {
        new SelectListItem { Text = "NIKHIL SEKHAR", Value = "NIKHIL SEKHAR" },
        new SelectListItem { Text = "VISAGH S", Value = "VISAGH S" },
        new SelectListItem { Text = "JIJIN E H", Value = "JIJIN E H" },
        new SelectListItem { Text = "MURUGESAN P", Value = "MURUGESAN P" }
    };

            ViewBag.TesterNames = new List<SelectListItem>
    {
        new SelectListItem { Text = "Rajendran N", Value = "Rajendran N" },
        new SelectListItem { Text = "Aditya T S", Value = "Aditya T S" },
        new SelectListItem { Text = "Anjitha K A", Value = "Anjitha K A" },
        new SelectListItem { Text = "Aswathy Chandran", Value = "Aswathy Chandran" },
        new SelectListItem { Text = "Kala Chandran", Value = "Kala Chandran" },
        new SelectListItem { Text = "Seethal V S", Value = "Seethal V S" }
    };

            return View();
        }

    }

}