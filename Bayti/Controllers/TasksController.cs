using Bayti.Data;
using Bayti.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Net;
using System.Security.Claims;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Bayti.Controllers
{
    public class TasksController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TasksController(ApplicationDbContext context)
        {
            _context = context;
        }


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

            var today = DateTime.Today;

            // CONSTRUCTION DE LA REQUÊTE SELON LE MODE (sans l'exécuter)
            IQueryable<TaskInstance> tasksQuery;

            if (mode == "Participatif")
            {
                tasksQuery = _context.TaskInstances
                    .Include(i => i.TaskTemplate)
                        .ThenInclude(t => t.Category)
                    .Include(i => i.AssignedUser)
                    .Where(i => i.TaskTemplate.ColocationId == colocationId
                             && i.DueDate.Date == today);
            }
            else
            {
                //manuel/auto
                tasksQuery = _context.TaskInstances
                    .Include(i => i.TaskTemplate)
                        .ThenInclude(t => t.Category)
                    .Where(i => i.TaskTemplate.ColocationId == colocationId
                             && i.DueDate.Date == today
                             && i.AssignedUserId == userId);
            }

            var tasks = await tasksQuery.OrderBy(i => i.DueDate).ToListAsync();

            if (User.FindFirstValue("IsAdmin") == "True")
            {
                ViewBag.AllTasks = await _context.TaskInstances
                    .Where(i => i.TaskTemplate.ColocationId == colocationId && i.DueDate.Date == today)
                    .ToListAsync();
            }

            ViewBag.CurrentUserId = userId;

            return View(tasks);
        }

        [HttpPost]
        public async Task<IActionResult> Complete(int id)
        {
            var userIdStr = User.FindFirstValue("UserId");
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();
            int userId = int.Parse(userIdStr);

            // cherche une tâche que l'utilisateur peut prendre en charge
            var instance = await _context.TaskInstances
                .Include(i => i.TaskTemplate)
                .FirstOrDefaultAsync(i => i.Id == id && (i.AssignedUserId == userId || i.AssignedUserId == null));

            if (instance != null && instance.Status != "Completed")
            {
                instance.Status = "Completed";
                instance.CompletedAt = DateTime.UtcNow;
                instance.ClaimedByUserId = userId;
                instance.PointsAwarded = instance.TaskTemplate.Points;

               
                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    user.Points += instance.PointsAwarded ?? 0;
                    
                    
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

                await NotifyUser(userId, "Mission accomplie ! 🏆", 
                    $"Félicitations ! Vous avez terminé '{instance.TaskTemplate.Title}' et gagné {instance.PointsAwarded} points.", 
                    "Success", instance.Id);

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }


        // choisir une tache
        [HttpPost]
        public async Task<IActionResult> Claim(int id)
        {
            var userIdStr = User.FindFirstValue("UserId");
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();
            int userId = int.Parse(userIdStr);

            // cherche une tache non assigné
            var instance = await _context.TaskInstances
                .FirstOrDefaultAsync(i => i.Id == id && i.AssignedUserId == null);

            if (instance != null && instance.Status != "Completed")
            {
                instance.AssignedUserId = userId;
                instance.ClaimedByUserId = userId;
                instance.ClaimedAt = DateTime.UtcNow;

                await NotifyUser(userId, "C'est noté ! 🧹", 
                    $"Vous avez choisi la tâche '{instance.TaskTemplate.Title}'. Elle est maintenant dans votre liste.", 
                    "Info", instance.Id);

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        // mode auto
        public async Task<IActionResult> GenerateTestTasks()
        {
            var colocationIdStr = User.FindFirstValue("ColocationId");
            int colocationId = int.Parse(colocationIdStr);
            var colocation = await _context.Colocations.FirstOrDefaultAsync(c => c.Id == colocationId);
            string mode = colocation?.AssignmentMode ?? "Auto";

            // Tous les membres de la colocation
            var members = await _context.Users
                .Where(u => u.ColocationId == colocationId)
                .ToListAsync();
            
            var memberIds = members.Select(m => m.Id).ToList();

            // Tous les templates de tâches actifs
            var templates = await _context.TaskTemplates
                .Where(t => t.ColocationId == colocationId && t.IsActive && !t.IsPaused)
                .ToListAsync();

            var random = new Random();
            string todayKey = DateTime.Today.DayOfWeek.ToString();  //"Monday", "Tuesday", etc.
            int currentDayOfWeekNum = (int)DateTime.Today.DayOfWeek == 0 ? 7 : (int)DateTime.Today.DayOfWeek; // 1=Mon...7=Sun
            int currentDayOfMonth = DateTime.Today.Day; // 1=Lundi...7=Dimanche

            // membres dispo today
            var availableUserIds = await _context.Availabilities
                .Where(a => a.DayKey == todayKey && a.IsActive && memberIds.Contains(a.UserId))
                .Select(a => a.UserId)
                .ToListAsync();

            int createdCount = 0;
            int skippedCount = 0;
            int alreadyExistsCount = 0;
            int assignedCount = 0;

            //Suivi local du nombre de tâches par utilisateur 
            var currentAssignments = memberIds.ToDictionary(id => (int?)id, id => 0);

            foreach (var t in templates)
            {
                // Vérification si la tache doit être généré aujourd'hui
                bool shouldGenerateToday = false;

                //Si la tâche est quotidienne, elle doit être générée AUJOURD'HUI
                if (t.RecurrenceType == "Daily")
                {
                    shouldGenerateToday = true;
                }
                else if (t.RecurrenceType == "Weekly" && !string.IsNullOrEmpty(t.WeeklyDays))
                {
                    //Si la tâche est hebdomadaire, vérifie si le jour actuel fait partie des jours sélectionnés
                    var days = t.WeeklyDays.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    shouldGenerateToday = days.Contains(currentDayOfWeekNum.ToString());
                }
                //Mensuel 
                else if (t.RecurrenceType == "Monthly" && t.MonthlyDay.HasValue)
                {
                    shouldGenerateToday = (t.MonthlyDay.Value == currentDayOfMonth);
                }
                //Unique 
                else if (t.RecurrenceType == "Once" && t.SpecificDate.HasValue)
                {
                    shouldGenerateToday = (t.SpecificDate.Value.Date == DateTime.Today);
                }
                //Personnalisé 
                else if (t.RecurrenceType == "Custom" && t.CustomIntervalDays.HasValue && t.CustomIntervalDays.Value > 0 && t.StartDate.HasValue)
                {
                    int daysPassed = (DateTime.Today - t.StartDate.Value.Date).Days;
                    shouldGenerateToday = (daysPassed >= 0 && daysPassed % t.CustomIntervalDays.Value == 0);
                }

                //Si la tâche ne doit PAS être générée aujourd'hui, on l'ignore et on passe à la tâche suivante
                if (!shouldGenerateToday) 
                {
                    skippedCount++;
                    continue; 
                }

                //Vérification si une instance existe déjà pour aujourd'hui
                var exists = await _context.TaskInstances
                    .AnyAsync(i => i.TaskTemplateId == t.Id && i.DueDate.Date == DateTime.Today);
                
                if (!exists)
                {
                    int? assignedUserId = null;
                    //assignation automatique à l'utilisateur le plus disponible
                    if (mode == "Auto")
                    {
                        /// Si des utilisateurs sont disponibles aujourd'hui 
                        var candidateIds = availableUserIds.Any() ? availableUserIds : memberIds;
                        
                        if (candidateIds.Any())
                        {
                            //  COMPTER LES TÂCHES DE CHAQUE CANDIDAT AUJOURD'HUI
                            var dbTaskCounts = await _context.TaskInstances
                                .Where(i => candidateIds.Contains(i.AssignedUserId ?? 0) && i.DueDate.Date == DateTime.Today)
                                .GroupBy(i => i.AssignedUserId)
                                .Select(g => new { UserId = g.Key, Count = g.Count() })
                                .ToListAsync();

                            //COMBINER AVEC LES TÂCHES DÉJÀ ASSIGNÉES DANS CETTE BOUCLE
                            var totalCounts = candidateIds.ToDictionary(id => (int?)id, id => currentAssignments[id]);
                            // Ajoute les compteurs de la base aux compteurs locaux
                            foreach (var tc in dbTaskCounts) { if (tc.UserId.HasValue) totalCounts[tc.UserId] += tc.Count; }

                            int minTasks = totalCounts.Values.Min();
                            var bestCandidates = totalCounts.Where(d => d.Value == minTasks).Select(d => d.Key).ToList();

                            assignedUserId = bestCandidates[random.Next(bestCandidates.Count)];
                            currentAssignments[assignedUserId]++;
                            assignedCount++;

                            await NotifyUser(assignedUserId.Value, "Nouvelle mission ! ✨", 
                                $"Une nouvelle tâche vous a été attribuée : {t.Title}.", "Info");
                        }
                    }

                    //Définition de la date d'échéance (heure préférée ou 20h par défaut)
                    var dueDate = DateTime.Today;
                    if (t.PreferredTime.HasValue) {
                        dueDate = dueDate.Add(t.PreferredTime.Value);
                    } else {
                        dueDate = dueDate.AddHours(20); // Default fallback
                    }

                    var instance = new TaskInstance
                    {
                        TaskTemplateId = t.Id,
                        AssignedUserId = assignedUserId,
                        DueDate = dueDate,
                        Status = "Pending",
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.TaskInstances.Add(instance);
                    createdCount++;
                }
                else
                {
                    //Tâche existe mais non assignée(mode Auto)
                    alreadyExistsCount++;
                    
                    if (mode == "Auto")
                    {
                        var existingTask = await _context.TaskInstances
                            .FirstOrDefaultAsync(i => i.TaskTemplateId == t.Id && i.DueDate.Date == DateTime.Today && i.AssignedUserId == null);
                            
                        if (existingTask != null)
                        {
                            var candidateIds = availableUserIds.Any() ? availableUserIds : memberIds;
                            if (candidateIds.Any())
                            {
                                var totalCounts = candidateIds.ToDictionary(id => (int?)id, id => currentAssignments[id]);
                                
                                var dbTaskCounts = await _context.TaskInstances
                                    .Where(i => candidateIds.Contains(i.AssignedUserId ?? 0) && i.DueDate.Date == DateTime.Today)
                                    .GroupBy(i => i.AssignedUserId)
                                    .Select(g => new { UserId = g.Key, Count = g.Count() })
                                    .ToListAsync();

                                foreach (var tc in dbTaskCounts) { if (tc.UserId.HasValue) totalCounts[tc.UserId] += tc.Count; }

                                int minTasks = totalCounts.Values.Min();
                                var bestCandidates = totalCounts.Where(d => d.Value == minTasks).Select(d => d.Key).ToList();

                                existingTask.AssignedUserId = bestCandidates[random.Next(bestCandidates.Count)];
                                
                                if (t.PreferredTime.HasValue) {
                                    existingTask.DueDate = DateTime.Today.Add(t.PreferredTime.Value);
                                }
                                
                                currentAssignments[existingTask.AssignedUserId]++;
                                assignedCount++;
                                createdCount++;

                                await NotifyUser(existingTask.AssignedUserId.Value, "Mission assignée ! 🧹", 
                                    $"La tâche '{t.Title}' vous a été assignée.", "Info");
                            }
                        }
                    }
                }
            }

            //MESSAGES DE CONFIRMATION
            if (createdCount > 0)
            {
                await _context.SaveChangesAsync();
                
                if (mode == "Auto" && assignedCount < createdCount)
                {
                    TempData["WarningMessage"] = $"{createdCount} tâches générées, mais {createdCount - assignedCount} n'ont pas pu être assignées automatiquement (aucun membre disponible aujourd'hui).";
                }
                else
                {
                    TempData["SuccessMessage"] = $"{createdCount} tâche(s) générée(s) avec succès !";
                }
            }
            else if (templates.Count == 0)
            {
                TempData["InfoMessage"] = "Aucune règle de tâche active trouvée pour cette colocation.";
            }
            else if (alreadyExistsCount > 0)
            {
                TempData["InfoMessage"] = "Toutes les tâches prévues pour aujourd'hui ont déjà été générées.";
            }
            else
            {
                TempData["InfoMessage"] = "Aucune tâche n'est planifiée pour aujourd'hui selon vos règles de récurrence.";
            }

            return RedirectToAction(nameof(Index));
        }

        
        // pour le mode manuel
        public async Task<IActionResult> Manage()
        {
            var userIdStr = User.FindFirstValue("UserId");
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login", "Account");
            
            var isAdmin = User.FindFirstValue("IsAdmin") == "True";
            if (!isAdmin) return Forbid();

            var colocationIdStr = User.FindFirstValue("ColocationId");
            int colocationId = int.Parse(colocationIdStr);

            var colocation = await _context.Colocations.Include(c => c.Members).FirstOrDefaultAsync(c => c.Id == colocationId);

           // CHARGEMENT DES TÂCHES DU JOUR et non terminé de tout les users 
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

                if (assignedUserId.HasValue)
                {
                    await NotifyUser(assignedUserId.Value, "Nouvelle tâche assignée", 
                        $"On vous a assigné la tâche : {instance.TaskTemplate.Title}. À faire pour le {instance.DueDate:dd/MM}.", 
                        "Info", instance.Id);
                }
            }

            return RedirectToAction(nameof(Manage));
        }

        // traiter les tâches en retard et à appliquer des pénalités
        [HttpPost]
        public async Task<IActionResult> ProcessOverdueTasks()
        {
            var colocationIdStr = User.FindFirstValue("ColocationId");
            int colocationId = int.Parse(colocationIdStr);
            
            // Find tasks that are past their due date and not completed

            
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

                // Penalty is equal to the points the user WOULD HAVE gained
                int taskPenalty = task.TaskTemplate.Points;

                if (task.AssignedUserId.HasValue && taskPenalty > 0)
                {
                    var user = await _context.Users.FindAsync(task.AssignedUserId.Value);
                    if (user != null)
                    {
                        user.Points -= taskPenalty;

                        var history = new PointHistory
                        {
                            UserId = user.Id,
                            PointsChange = -taskPenalty,
                            PointsBalanceAfter = user.Points,
                            Reason = $"Tâche non réalisée (Pénalité) : {task.TaskTemplate.Title}",
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

        private async Task NotifyUser(int userId, string title, string message, string type = "Info", int? relatedId = null)
        {
            _context.Notifications.Add(new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                ActionUrl = "/Tasks",
                RelatedEntityType = relatedId.HasValue ? "TaskInstance" : "General",
                RelatedEntityId = relatedId,
                CreatedAt = DateTime.Now
            });
        }
    }
}
