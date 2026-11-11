using System.Security.Claims;
using InterLink.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterLink.Controllers;

public class AccountController : Controller
{
    private readonly Store _s;
    public AccountController(Store s) => _s = s;

    private AppUser? Current => User.Identity?.IsAuthenticated == true
        ? _s.U(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)) : null;

    // ---------- login / logout ----------
    [HttpGet] public IActionResult Login() => View();

    [HttpPost]
    public async Task<IActionResult> Login(string username = "", string password = "")
    {
        var u = _s.D.Users.FirstOrDefault(x => x.Username.Equals(username, StringComparison.OrdinalIgnoreCase) && x.Pass == Store.Hash(password));
        if (u == null) { ViewBag.Error = "Invalid username or password."; return View(); }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, u.Id.ToString()),
            new(ClaimTypes.Name, u.Name),
            new(ClaimTypes.Role, u.Role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    // ---------- register ----------
    [HttpGet] public IActionResult Register() => View();

    [HttpPost]
    public IActionResult Register(string username = "", string password = "", string name = "", string program = "", string question = "", string answer = "")
    {
        if (username == "" || password == "" || name == "" || answer.Trim() == "") { ViewBag.Error = "All fields are required."; return View(); }
        if (password.Length < 6) { ViewBag.Error = "Password must be at least 6 characters."; return View(); }
        if (!Store.Questions.Contains(question)) { ViewBag.Error = "Choose a security question."; return View(); }
        if (_s.D.Users.Any(x => x.Username.Equals(username, StringComparison.OrdinalIgnoreCase))) { ViewBag.Error = "Username already taken."; return View(); }

        _s.D.Users.Add(new AppUser
        {
            Id = _s.D.Users.Count + 1,
            Name = name,
            Username = username,
            Pass = Store.Hash(password),
            Role = "student",
            Program = program,
            Question = question,
            Answer = Store.HashAnswer(answer)
        });
        _s.Save();
        TempData["Msg"] = "Account created. You can log in now.";
        return RedirectToAction("Login");
    }

    // ---------- forgot password ----------
    [HttpGet] public IActionResult ForgotPassword() => View();

    [HttpPost]
    public IActionResult ForgotPassword(string username = "")
    {
        var u = _s.D.Users.FirstOrDefault(x => x.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
        if (u == null || u.Question == "")
        {
            ViewBag.Error = "Account not found, or no recovery question is set. Please contact your coordinator.";
            return View();
        }
        ViewBag.Username = u.Username;
        ViewBag.Question = u.Question;
        return View();
    }

    [HttpPost]
    public IActionResult ResetPassword(string username = "", string answer = "", string newPassword = "", string confirmPassword = "")
    {
        var u = _s.D.Users.FirstOrDefault(x => x.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
        if (u == null || u.Question == "") { ViewBag.Error = "Account not found."; return View("ForgotPassword"); }

        ViewBag.Username = u.Username;
        ViewBag.Question = u.Question;
        if (u.Answer != Store.HashAnswer(answer)) { ViewBag.Error = "Incorrect answer."; return View("ForgotPassword"); }
        if (newPassword.Length < 6) { ViewBag.Error = "New password must be at least 6 characters."; return View("ForgotPassword"); }
        if (newPassword != confirmPassword) { ViewBag.Error = "Passwords do not match."; return View("ForgotPassword"); }

        u.Pass = Store.Hash(newPassword);
        _s.Save();
        TempData["Msg"] = "Password reset. You can log in with your new password.";
        return RedirectToAction("Login");
    }

    // ---------- change password (logged in) ----------
    [Authorize, HttpGet] public IActionResult ChangePassword() => View();

    [Authorize, HttpPost]
    public IActionResult ChangePassword(string currentPassword = "", string newPassword = "", string confirmPassword = "")
    {
        var u = Current!;
        if (u.Pass != Store.Hash(currentPassword)) { ViewBag.Error = "Current password is incorrect."; return View(); }
        if (newPassword.Length < 6) { ViewBag.Error = "New password must be at least 6 characters."; return View(); }
        if (newPassword != confirmPassword) { ViewBag.Error = "New passwords do not match."; return View(); }

        u.Pass = Store.Hash(newPassword);
        _s.Save();
        TempData["Msg"] = "Password changed successfully.";
        return RedirectToAction("ChangePassword");
    }

    [Authorize, HttpPost]
    public IActionResult UpdateSecurity(string currentPassword = "", string question = "", string answer = "")
    {
        var u = Current!;
        if (u.Pass != Store.Hash(currentPassword)) { ViewBag.Error = "Current password is incorrect."; return View("ChangePassword"); }
        if (!Store.Questions.Contains(question) || answer.Trim() == "") { ViewBag.Error = "Choose a question and enter an answer."; return View("ChangePassword"); }

        u.Question = question;
        u.Answer = Store.HashAnswer(answer);
        _s.Save();
        TempData["Msg"] = "Recovery question updated.";
        return RedirectToAction("ChangePassword");
    }

    [Authorize, HttpPost]
    public IActionResult MarkRead()
    {
        int id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        foreach (var n in _s.D.Notes.Where(q => q.User == id)) n.Read = true;
        _s.Save();
        return RedirectToAction("Index", "Home");
    }
}