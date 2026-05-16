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
            // Activité récente (5 dernières tâches complétées dans la colocation)
            ColocationTasks = await _context.TaskInstances
                .Include(t => t.TaskTemplate)
                .Include(t => t.AssignedUser)
                .Where(t => t.TaskTemplate.ColocationId == colocationId && t.Status == "Completed")
                .OrderByDescending(t => t.CompletedAt)
                .Take(5)
                .ToListAsync()
        };
        
        var today = DateTime.Today;

        // Toutes les tâches de l'utilisateur pour aujourd'hui
        var allMyTasksToday = await _context.TaskInstances
            .Where(t => t.AssignedUserId == userId && t.DueDate.Date == today)
            .ToListAsync();
            
        // Nombre total de tâches pour aujourd'hui
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

    // Action qui affiche la page d'erreur générique
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

// Regroupe toutes les données nécessaires à l'affichage du tableau de bord
public class DashboardViewModel
{
    public Colocation Colocation { get; set; }
    public List<TaskInstance> MyPendingTasks { get; set; }
    public int UserPoints { get; set; }
    public int TotalTasksToday { get; set; }
    public int CompletedTasksToday { get; set; }
    public List<TaskInstance> ColocationTasks { get; set; }
}
