using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Bayti.Models;
using Microsoft.AspNetCore.Authorization;
using Bayti.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Bayti.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _context;

    public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var colocationIdStr = User.FindFirstValue("ColocationId");
        if (string.IsNullOrEmpty(colocationIdStr))
        {
            return RedirectToAction("Setup", "Colocation");
        }

        var colocationId = int.Parse(colocationIdStr);
        var userId = int.Parse(User.FindFirstValue("UserId"));

        // Données pour le Dashboard
        var dashboard = new DashboardViewModel
        {
            Colocation = await _context.Colocations
                .Include(c => c.Members)
                .Include(c => c.TaskTemplates)
                .FirstOrDefaultAsync(c => c.Id == colocationId),
            
            MyPendingTasks = await _context.TaskInstances
                .Include(t => t.TaskTemplate)
                .Where(t => t.AssignedUserId == userId && t.Status == "Pending")
                .OrderBy(t => t.DueDate)
                .ToListAsync(),
            
            RecentNotifications = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(5)
                .ToListAsync(),

            ColocationTasks = await _context.TaskInstances
                .Include(t => t.TaskTemplate)
                .Include(t => t.AssignedUser)
                .Where(t => t.TaskTemplate.ColocationId == colocationId && t.Status == "Pending" && t.AssignedUserId != userId)
                .OrderBy(t => t.DueDate)
                .Take(5)
                .ToListAsync(),

            RecentPurchases = await _context.Purchases
                .Include(p => p.User)
                .Include(p => p.Reward)
                .Where(p => p.Reward.ColocationId == colocationId)
                .OrderByDescending(p => p.PurchasedAt)
                .Take(4)
                .ToListAsync()
        };
        
        var today = DateTime.Today;
        var allMyTasksToday = await _context.TaskInstances
            .Where(t => t.AssignedUserId == userId && t.DueDate.Date == today)
            .ToListAsync();
            
        dashboard.TotalTasksToday = allMyTasksToday.Count;
        dashboard.CompletedTasksToday = allMyTasksToday.Count(t => t.Status == "Completed");
        
        var currentUser = await _context.Users.FindAsync(userId);
        dashboard.UserPoints = currentUser?.Points ?? 0;

        return View(dashboard);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

public class DashboardViewModel
{
    public Colocation Colocation { get; set; }
    public List<TaskInstance> MyPendingTasks { get; set; }
    public List<Notification> RecentNotifications { get; set; }
    public int UserPoints { get; set; }
    public int TotalTasksToday { get; set; }
    public int CompletedTasksToday { get; set; }
    public List<TaskInstance> ColocationTasks { get; set; }
    public List<Purchase> RecentPurchases { get; set; }
}
