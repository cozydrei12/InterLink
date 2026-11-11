using System.Security.Claims;
using InterLink.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterLink.Controllers;

[Authorize(Roles = "supervisor")]
public class SupervisorController : Controller
{
    private readonly Store _s;
    public SupervisorController(Store s) => _s = s;

    private AppUser Me => _s.U(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!))!;
    private IActionResult Done(string msg) { TempData["Msg"] = msg; return RedirectToAction(nameof(Index)); }

    public IActionResult Index() => View(Me);

    private List<Log> Pending()
    {
        var ids = _s.Interns(Me).Select(i => i.Id).ToList();
        return _s.D.Logs.Where(l => ids.Contains(l.Student) && l.Out != null && !l.Verified).ToList();
    }

    [HttpPost]
    public IActionResult Verify(int id)
    {
        var rec = Pending().FirstOrDefault(l => l.Id == id);
        if (rec == null) return Done("Record not found.");
        rec.Verified = true;
        _s.Save();
        return Done("Attendance verified.");
    }

    [HttpPost]
    public IActionResult VerifyAll()
    {
        var list = Pending();
        foreach (var l in list) l.Verified = true;
        _s.Save();
        return Done($"{list.Count} record(s) verified.");
    }

    [HttpPost]
    public IActionResult Evaluate(int student, int score, string comments = "")
    {
        if (!_s.Interns(Me).Any(i => i.Id == student)) return Done("That student is not your intern.");
        if (score < 0 || score > 100) return Done("Score must be between 0 and 100.");
        _s.D.Evals.Add(new Eval { Id = _s.D.Evals.Count + 1, Student = student, Score = score, Comments = comments });
        _s.Notify(student, $"You received an evaluation: {score}/100.");
        return Done("Evaluation saved.");
    }
}