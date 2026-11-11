using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace InterLink.Models;

public class AppUser { public int Id, Job, Required = 300; public string Name = "", Username = "", Pass = "", Role = "", Company = "", Program = "", Question = "", Answer = ""; }
public class Job { public int Id, Slots; public string Title = "", Company = "", Place = ""; }
public class App { public int Id, Student, Job; public string Status = "Pending"; }
public class Doc { public int Id, Student; public string Name = "", FilePath = "", Status = "Submitted", Remarks = ""; }
public class Log { public int Id, Student; public DateTime In; public DateTime? Out; public double Hours; public bool Verified; }
public class Rep { public int Id, Student; public string Title = "", Text = ""; public DateTime Date = DateTime.Now; }
public class Eval { public int Id, Student, Score; public string Comments = ""; }
public class Note { public int Id, User; public string Text = ""; public bool Read; public DateTime Date = DateTime.Now; }

public class Db
{
    public List<AppUser> Users = new(); public List<Job> Jobs = new(); public List<App> Apps = new(); public List<Doc> Docs = new();
    public List<Log> Logs = new(); public List<Rep> Reports = new(); public List<Eval> Evals = new(); public List<Note> Notes = new();
}

/// <summary>Centralized data storage (one JSON file) + shared helper methods.</summary>
public class Store
{
    public static readonly string[] Req = { "Resume", "Application Letter", "Parent Consent", "Medical Certificate", "Endorsement Letter" };
    public static readonly string[] Questions =
    {
        "What is your mother's maiden name?", "What was the name of your first pet?", "What city were you born in?",
        "What is your favorite food?", "What was the name of your elementary school?"
    };
    public const string DemoQuestion = "What is the name of this system?";
    public const string DemoAnswer = "internlink";

    private static readonly JsonSerializerOptions Opt = new() { IncludeFields = true, WriteIndented = true };
    private readonly string _path;
    public Db D { get; private set; }

    public Store(IWebHostEnvironment env)
    {
        _path = Path.Combine(env.ContentRootPath, "internlink_data.json");
        D = File.Exists(_path) ? JsonSerializer.Deserialize<Db>(File.ReadAllText(_path), Opt)! : Seed();

        // demo accounts get a recovery question so "Forgot password" works for them
        foreach (var u in D.Users.Where(x => x.Question == "" && (x.Username == "student" || x.Username == "coordinator" || x.Username == "supervisor")))
        {
            u.Question = DemoQuestion;
            u.Answer = HashAnswer(DemoAnswer);
        }
        Save();
    }

    public static string Hash(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
    public static string HashAnswer(string a) => Hash(a.Trim().ToLower());
    public void Save() { lock (this) File.WriteAllText(_path, JsonSerializer.Serialize(D, Opt)); }

    private static Db Seed()
    {
        var d = new Db();
        d.Users.Add(new AppUser { Id = 1, Name = "Maria Santos", Username = "coordinator", Pass = Hash("coord123"), Role = "coordinator" });
        d.Users.Add(new AppUser { Id = 2, Name = "John Reyes", Username = "supervisor", Pass = Hash("super123"), Role = "supervisor", Company = "TechNova Inc." });
        d.Users.Add(new AppUser { Id = 3, Name = "Juan Dela Cruz", Username = "student", Pass = Hash("student123"), Role = "student", Program = "BSIT" });
        d.Jobs.Add(new Job { Id = 1, Title = "Software Developer Intern", Company = "TechNova Inc.", Place = "Cebu City", Slots = 3 });
        d.Jobs.Add(new Job { Id = 2, Title = "IT Support Intern", Company = "BlueWave Solutions", Place = "Mandaue City", Slots = 2 });
        return d;
    }

    public void Notify(int uid, string text) { D.Notes.Add(new Note { Id = D.Notes.Count + 1, User = uid, Text = text }); Save(); }
    public void NotifyRole(string role, string text) { foreach (var w in D.Users.Where(q => q.Role == role).ToList()) Notify(w.Id, text); }

    public AppUser? U(int id) => D.Users.FirstOrDefault(u => u.Id == id);
    public Job? J(int id) => D.Jobs.FirstOrDefault(j => j.Id == id);
    public double Hrs(int sid) => Math.Round(D.Logs.Where(l => l.Student == sid && l.Verified).Sum(l => l.Hours), 2);
    public int Slots(Job j) => j.Slots - D.Apps.Count(a => a.Job == j.Id && a.Status == "Accepted");
    public IEnumerable<AppUser> Interns(AppUser sup) =>
        D.Users.Where(u => u.Role == "student" && u.Job != 0 && J(u.Job)?.Company == sup.Company);
}