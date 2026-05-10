using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Bayti.Models;
using Bayti.Data;
using System.Security.Claims;

namespace Bayti.Controllers
{
    public class TasksController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TasksController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Tasks
        public async Task<IActionResult> Index()
        {
            var userIdStr = User.FindFirstValue("UserId");
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login", "Account");
            int userId = int.Parse(userIdStr);

            var colocationIdStr = User.FindFirstValue("ColocationId");
            int colocationId = int.Parse(colocationIdStr);

            var colocation = await _context.Colocations.FindAsync(colocationId);
            string mode = colocation?.AssignmentMode ?? "Auto";
            ViewBag.AssignmentMode = mode;

            // Fetch tasks for today
            var today = DateTime.Today;

            IQueryable<TaskInstance> tasksQuery;

            if (mode == "Participatif")
            {
                // Show ALL tasks for the colocation for TODAY only
                tasksQuery = _context.TaskInstances
                    .Include(i => i.TaskTemplate)
                        .ThenInclude(t => t.Category)
                    .Include(i => i.AssignedUser)
                    .Where(i => i.TaskTemplate.ColocationId == colocationId
                             && i.Status != "Completed"
                             && i.DueDate.Date == today);
            }
            else
            {
                // Auto or Manuel: show only tasks assigned to the current user for TODAY
                tasksQuery = _context.TaskInstances
                    .Include(i => i.TaskTemplate)
                        .ThenInclude(t => t.Category)
                    .Where(i => i.TaskTemplate.ColocationId == colocationId
                             && i.Status != "Completed"
                             && i.DueDate.Date == today
                             && i.AssignedUserId == userId);
            }

            var tasks = await tasksQuery.OrderBy(i => i.DueDate).ToListAsync();

            ViewBag.CurrentUserId = userId;

            return View(tasks);
        }

        [HttpPost]
        public async Task<IActionResult> Complete(int id)
        {
            var userIdStr = User.FindFirstValue("UserId");
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();
            int userId = int.Parse(userIdStr);

            var instance = await _context.TaskInstances
                .Include(i => i.TaskTemplate)
                .FirstOrDefaultAsync(i => i.Id == id && (i.AssignedUserId == userId || i.AssignedUserId == null));

            if (instance != null && instance.Status != "Completed")
            {
                instance.Status = "Completed";
                instance.CompletedAt = DateTime.UtcNow;
                instance.ClaimedByUserId = userId; // In case it was unassigned
                instance.PointsAwarded = instance.TaskTemplate.Points;

                // Award points to user
                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    user.Points += instance.PointsAwarded ?? 0;
                    
                    // Add to history
                    var history = new PointHistory
                    {
                        UserId = userId,
                        PointsChange = instance.PointsAwarded ?? 0,
                        PointsBalanceAfter = user.Points,
                        Reason = $"Complétion tâche : {instance.TaskTemplate.Title}",
                        Type = "TaskCompletion",
                        TaskInstanceId = instance.Id,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.PointHistory.Add(history);
                }

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Claim(int id)
        {
            var userIdStr = User.FindFirstValue("UserId");
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();
            int userId = int.Parse(userIdStr);

            var instance = await _context.TaskInstances
                .FirstOrDefaultAsync(i => i.Id == id && i.AssignedUserId == null);

            if (instance != null && instance.Status != "Completed")
            {
                instance.AssignedUserId = userId;
                instance.ClaimedByUserId = userId;
                instance.ClaimedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        // Action to generate test tasks if none exist
        public async Task<IActionResult> GenerateTestTasks()
        {
            var colocationIdStr = User.FindFirstValue("ColocationId");
            int colocationId = int.Parse(colocationIdStr);
            var colocation = await _context.Colocations.Include(c => c.Members).FirstOrDefaultAsync(c => c.Id == colocationId);
            string mode = colocation?.AssignmentMode ?? "Auto";

            var templates = await _context.TaskTemplates
                .Where(t => t.ColocationId == colocationId && t.IsActive && !t.IsPaused)
                .ToListAsync();

            var members = colocation?.Members.ToList() ?? new List<ApplicationUser>();
            var random = new Random();
            string todayKey = DateTime.Today.DayOfWeek.ToString(); // e.g. "Monday"
            int currentDayOfWeekNum = (int)DateTime.Today.DayOfWeek == 0 ? 7 : (int)DateTime.Today.DayOfWeek; // 1=Mon...7=Sun
            int currentDayOfMonth = DateTime.Today.Day;

            // Fetch availabilities for today
            var availableUserIds = await _context.Availabilities
                .Where(a => a.DayKey == todayKey && a.IsActive && members.Select(m => m.Id).Contains(a.UserId))
                .Select(a => a.UserId)
                .ToListAsync();

            foreach (var t in templates)
            {
                // Check recurrence rules
                bool shouldGenerateToday = false;
                
                if (t.RecurrenceType == "Daily")
                {
                    shouldGenerateToday = true;
                }
                else if (t.RecurrenceType == "Weekly" && !string.IsNullOrEmpty(t.WeeklyDays))
                {
                    // WeeklyDays is typically saved as "1,3,5" where 1=Mon, 2=Tue...
                    shouldGenerateToday = t.WeeklyDays.Contains(currentDayOfWeekNum.ToString());
                }
                else if (t.RecurrenceType == "Monthly" && t.MonthlyDay.HasValue)
                {
                    shouldGenerateToday = (t.MonthlyDay.Value == currentDayOfMonth);
                }
                else if (t.RecurrenceType == "Once" && t.SpecificDate.HasValue)
                {
                    shouldGenerateToday = (t.SpecificDate.Value.Date == DateTime.Today);
                }
                else if (t.RecurrenceType == "Custom" && t.CustomIntervalDays.HasValue && t.CustomIntervalDays.Value > 0 && t.StartDate.HasValue)
                {
                    int daysPassed = (DateTime.Today - t.StartDate.Value.Date).Days;
                    shouldGenerateToday = (daysPassed >= 0 && daysPassed % t.CustomIntervalDays.Value == 0);
                }

                if (!shouldGenerateToday) continue; // Skip if not scheduled for today

                // Check if already has an instance for today
                var exists = await _context.TaskInstances
                    .AnyAsync(i => i.TaskTemplateId == t.Id && i.DueDate.Date == DateTime.Today);
                
                if (!exists)
                {
                    int? assignedUserId = null;
                    if (mode == "Auto")
                    {
                        if (availableUserIds.Any())
                        {
                            assignedUserId = availableUserIds[random.Next(availableUserIds.Count)];
                        }
                        else if (members.Any())
                        {
                            // Fallback if no one explicitly available
                            assignedUserId = members[random.Next(members.Count)].Id;
                        }
                    }

                    var instance = new TaskInstance
                    {
                        TaskTemplateId = t.Id,
                        AssignedUserId = assignedUserId,
                        DueDate = DateTime.Today.AddHours(20), // Due at 8pm
                        Status = "Pending",
                        Comments = "", // Required by DB
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.TaskInstances.Add(instance);
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: Tasks/Manage (For Admin Manuel Mode)
        public async Task<IActionResult> Manage()
        {
            var userIdStr = User.FindFirstValue("UserId");
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login", "Account");
            
            var isAdmin = User.FindFirstValue("IsAdmin") == "True";
            if (!isAdmin) return Forbid();

            var colocationIdStr = User.FindFirstValue("ColocationId");
            int colocationId = int.Parse(colocationIdStr);

            var colocation = await _context.Colocations.Include(c => c.Members).FirstOrDefaultAsync(c => c.Id == colocationId);

            var tasks = await _context.TaskInstances
                .Include(i => i.TaskTemplate)
                    .ThenInclude(t => t.Category)
                .Include(i => i.AssignedUser)
                .Where(i => i.TaskTemplate.ColocationId == colocationId && i.Status != "Completed" && i.DueDate.Date == DateTime.Today)
                .OrderBy(i => i.DueDate)
                .ToListAsync();

            ViewBag.Members = colocation.Members;
            return View(tasks);
        }

        [HttpPost]
        public async Task<IActionResult> AssignTask(int taskId, int? assignedUserId)
        {
            var isAdmin = User.FindFirstValue("IsAdmin") == "True";
            if (!isAdmin) return Forbid();

            var instance = await _context.TaskInstances.Include(t => t.TaskTemplate).FirstOrDefaultAsync(t => t.Id == taskId);
            if (instance != null)
            {
                instance.AssignedUserId = assignedUserId;
                await _context.SaveChangesAsync();

                // Send notification to the user if newly assigned
                if (assignedUserId.HasValue)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = assignedUserId.Value,
                        Title = "Nouvelle tâche assignée",
                        Message = $"On vous a assigné la tâche : {instance.TaskTemplate.Title}. À faire pour le {instance.DueDate:dd/MM}.",
                        Type = "Info",
                        ActionUrl = "/Tasks",
                        RelatedEntityType = "TaskInstance",
                        RelatedEntityId = instance.Id,
                        CreatedAt = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync();
                }
            }

            return RedirectToAction(nameof(Manage));
        }

        // Action to send reminders for pending tasks
        [HttpPost]
        public async Task<IActionResult> SendReminders()
        {
            var colocationIdStr = User.FindFirstValue("ColocationId");
            int colocationId = int.Parse(colocationIdStr);
            var isAdmin = User.FindFirstValue("IsAdmin") == "True";

            if (!isAdmin) return Forbid();

            var pendingTasks = await _context.TaskInstances
                .Include(t => t.TaskTemplate)
                .Where(t => t.TaskTemplate.ColocationId == colocationId && 
                            t.Status == "Pending" && 
                            t.AssignedUserId != null &&
                            t.DueDate.Date >= DateTime.Today)
                .ToListAsync();

            int notifsSent = 0;
            foreach (var task in pendingTasks)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = task.AssignedUserId.Value,
                    Title = "Rappel de tâche 🔔",
                    Message = $"N'oubliez pas de terminer : {task.TaskTemplate.Title}. L'échéance approche !",
                    Type = "Warning",
                    ActionUrl = "/Tasks",
                    RelatedEntityType = "TaskInstance",
                    RelatedEntityId = task.Id,
                    CreatedAt = DateTime.UtcNow
                });
                notifsSent++;
            }

            if (notifsSent > 0)
            {
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"{notifsSent} rappel(s) envoyé(s) avec succès !";
            }

            return RedirectToAction(nameof(Index));
        }

        // Action to process overdue tasks and apply penalties (Simulates a midnight cron job)
        [HttpPost]
        public async Task<IActionResult> ProcessOverdueTasks()
        {
            var colocationIdStr = User.FindFirstValue("ColocationId");
            int colocationId = int.Parse(colocationIdStr);
            var colocation = await _context.Colocations.FindAsync(colocationId);
            
            int penalty = colocation?.LatePenaltyPoints ?? 0;

            // Find tasks that are past their due date and not completed
            var overdueTasks = await _context.TaskInstances
                .Include(t => t.TaskTemplate)
                .Where(t => t.TaskTemplate.ColocationId == colocationId && 
                            t.Status == "Pending" && 
                            t.DueDate < DateTime.UtcNow)
                .ToListAsync();

            foreach (var task in overdueTasks)
            {
                task.Status = "Failed";

                // Penalize if the task was assigned to someone (or claimed but not finished)
                if (task.AssignedUserId.HasValue && penalty > 0)
                {
                    var user = await _context.Users.FindAsync(task.AssignedUserId.Value);
                    if (user != null)
                    {
                        user.Points -= penalty;

                        var history = new PointHistory
                        {
                            UserId = user.Id,
                            PointsChange = -penalty,
                            PointsBalanceAfter = user.Points,
                            Reason = $"Tâche non réalisée : {task.TaskTemplate.Title}",
                            Type = "Penalty",
                            TaskInstanceId = task.Id,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.PointHistory.Add(history);
                    }
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
