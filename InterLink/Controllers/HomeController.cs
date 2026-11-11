using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace InterLink.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated != true) return RedirectToAction("Login", "Account");
        var role = User.FindFirstValue(ClaimTypes.Role);
        return RedirectToAction("Index", role == "student" ? "Student" : role == "coordinator" ? "Coordinator" : "Supervisor");
    }
}
