using System.Security.Claims;
using InterLink.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterLink.Controllers;

[Authorize(Roles = "coordinator")]
public class CoordinatorController : Controller
{
    private readonly Store _s;
    public CoordinatorController(Store s) => _s = s;

    private AppUser Me => _s.U(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!))!;
    private IActionResult Done(string msg) { TempData["Msg"] = msg; return RedirectToAction(nameof(Index)); }

    public IActionResult Index() => View(Me);

    [HttpPost]
    public IActionResult Decide(int id, bool accept)
    {
        var app = _s.D.Apps.FirstOrDefault(a => a.Id == id && a.Status == "Pending");
        if (app == null) return Done("Application not found.");
        var st = _s.U(app.Student)!; var job = _s.J(app.Job)!;

        if (accept)
        {
            if (_s.Slots(job) <= 0) return Done("No slots left.");
            app.Status = "Accepted"; st.Job = job.Id;
            foreach (var o in _s.D.Apps.Where(q => q.Student == st.Id && q.Status == "Pending")) o.Status = "Rejected";
            _s.Notify(st.Id, $"Congratulations! You were accepted for {job.Title} at {job.Company}.");
        }
        else
        {
            app.Status = "Rejected";
            _s.Notify(st.Id, $"Your application for {job.Title} at {job.Company} was not accepted.");
        }
        _s.Save();
        return Done(accept ? "Application accepted." : "Application rejected.");
    }

    [HttpPost]
    public IActionResult ReviewDoc(int id, bool approve, string remarks = "")
    {
        var doc = _s.D.Docs.FirstOrDefault(d => d.Id == id && d.Status == "Submitted");
        if (doc == null) return Done("Document not found.");
        doc.Status = approve ? "Approved" : "Rejected";
        doc.Remarks = remarks;
        _s.Notify(doc.Student, $"Your {doc.Name} was {doc.Status.ToLower()}. {remarks}");
        return Done("Document reviewed.");
    }

    [HttpPost]
    public IActionResult AddJob(string title = "", string company = "", string place = "", int slots = 1)
    {
        if (title == "" || company == "") return Done("Title and company are required.");
        _s.D.Jobs.Add(new Job { Id = _s.D.Jobs.Count + 1, Title = title, Company = company, Place = place, Slots = slots });
        _s.Save();
        return Done("Internship added.");
    }

    [HttpPost]
    public IActionResult Announce(string message = "")
    {
        if (message == "") return Done("Write a message first.");
        foreach (var s in _s.D.Users.Where(u => u.Role == "student").ToList()) _s.Notify(s.Id, "[Announcement] " + message);
        return Done("Announcement sent to all students.");
    }

    [HttpPost]
    public IActionResult Remind()
    {
        int count = 0;
        foreach (var s in _s.D.Users.Where(u => u.Role == "student").ToList())
        {
            var missing = Store.Req.Where(r => !_s.D.Docs.Any(d => d.Student == s.Id && d.Name == r && d.Status != "Rejected")).ToList();
            if (missing.Count == 0) continue;
            _s.Notify(s.Id, "Reminder - missing requirements: " + string.Join(", ", missing));
            count++;
        }
        return Done($"Reminders sent to {count} student(s).");
    }
}