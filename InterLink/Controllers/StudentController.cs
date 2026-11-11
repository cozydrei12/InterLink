using System.Security.Claims;
using InterLink.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterLink.Controllers;

[Authorize(Roles = "student")]
public class StudentController : Controller
{
    private readonly Store _s;
    private readonly IWebHostEnvironment _env;
    public StudentController(Store s, IWebHostEnvironment env) { _s = s; _env = env; }

    private AppUser Me => _s.U(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!))!;
    private IActionResult Done(string msg) { TempData["Msg"] = msg; return RedirectToAction(nameof(Index)); }

    public IActionResult Index() => View(Me);

    [HttpPost]
    public IActionResult Apply(int id)
    {
        var me = Me; var job = _s.J(id);
        if (job == null) return Done("Internship not found.");
        if (me.Job != 0) return Done("You are already placed in an internship.");
        if (_s.D.Apps.Any(a => a.Student == me.Id && a.Job == id && a.Status != "Rejected")) return Done("You already applied to this internship.");
        if (_s.Slots(job) <= 0) return Done("No slots left.");

        _s.D.Apps.Add(new App { Id = _s.D.Apps.Count + 1, Student = me.Id, Job = id });
        _s.NotifyRole("coordinator", $"{me.Name} applied for {job.Title} at {job.Company}.");
        return Done("Application submitted.");
    }

    [HttpPost]
    public async Task<IActionResult> Upload(string name, IFormFile? file)
    {
        var me = Me;
        if (file == null || file.Length == 0 || !Store.Req.Contains(name)) return Done("Choose a requirement and a file.");
        var ext = Path.GetExtension(file.FileName).ToLower();
        if (!new[] { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png" }.Contains(ext) || file.Length > 5_000_000)
            return Done("Only PDF, DOC, DOCX, JPG, or PNG files up to 5 MB are allowed.");

        var dir = Path.Combine(_env.WebRootPath, "uploads");
        Directory.CreateDirectory(dir);
        var fileName = $"{me.Id}_{Guid.NewGuid():N}{ext}";
        using (var fs = new FileStream(Path.Combine(dir, fileName), FileMode.Create)) await file.CopyToAsync(fs);

        _s.D.Docs.Add(new Doc { Id = _s.D.Docs.Count + 1, Student = me.Id, Name = name, FilePath = "/uploads/" + fileName });
        _s.NotifyRole("coordinator", $"{me.Name} submitted {name}.");
        return Done("Uploaded and sent for review.");
    }

    [HttpPost]
    public IActionResult TimeIn()
    {
        var me = Me;
        if (me.Job == 0) return Done("You need an accepted internship first.");
        if (_s.D.Logs.Any(l => l.Student == me.Id && l.Out == null)) return Done("You are already timed in.");
        _s.D.Logs.Add(new Log { Id = _s.D.Logs.Count + 1, Student = me.Id, In = DateTime.Now });
        _s.Save();
        return Done("Timed in.");
    }

    [HttpPost]
    public IActionResult TimeOut()
    {
        var l = _s.D.Logs.FirstOrDefault(q => q.Student == Me.Id && q.Out == null);
        if (l == null) return Done("You are not timed in.");
        l.Out = DateTime.Now;
        l.Hours = Math.Round((l.Out.Value - l.In).TotalHours, 2);
        _s.Save();
        return Done($"Timed out. {l.Hours} hour(s) recorded, pending supervisor verification.");
    }

    [HttpPost]
    public IActionResult Report(string title = "", string text = "")
    {
        var me = Me;
        if (title == "" || text == "") return Done("Title and content are required.");
        _s.D.Reports.Add(new Rep { Id = _s.D.Reports.Count + 1, Student = me.Id, Title = title, Text = text });
        _s.NotifyRole("coordinator", $"{me.Name} submitted a report: {title}.");
        return Done("Report submitted.");
    }
}