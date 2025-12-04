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
           

            return View();
        }

        [AllowAnonymous]
        public IActionResult ReleaseStatus()
        {
           
    

            return View();
        }

    }

}