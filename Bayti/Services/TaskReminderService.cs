using Microsoft.EntityFrameworkCore;
using Bayti.Data;
using Bayti.Models;

namespace Bayti.Services
{
    public class TaskReminderService : BackgroundService
    {
        // ServiceProvider : permet de créer un scope pour accéder au DbContext
        // On ne peut pas injecter directement le DbContext car il est "Scoped"
        private readonly IServiceProvider _serviceProvider;
        
        // Intervalle de vérification : toutes les 15 minutes
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(15);
        // Constructeur : injection du service provider
        public TaskReminderService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }
        // Exécutée automatiquement au démarrage de l'application
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Boucle infinie tant que l'application tourne
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Vérifie et envoie les rappels
                    await CheckAndSendReminders();
                }
                catch (Exception ex)
                {
                    // En cas d'erreur, on logue (sans faire planter le service)
                    Console.WriteLine($"Error in TaskReminderService: {ex.Message}");
                }
                // Attend 15 minutes avant la prochaine vérification
                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        // ==================== VÉRIFICATION ET ENVOI DES RAPPELS ====================
        private async Task CheckAndSendReminders()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                // Crée un scope : permet d'avoir un DbContext frais et isolé
                // Chaque exécution a son propre DbContext
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var now = DateTime.Now;
                
                // Seuil de rappel : 1 heure avant l'échéance
                var reminderThreshold = now.AddHours(1);
                
                // ==================== RECHERCHE DES TÂCHES À RAPPELER ====================
                var tasksToRemind = await context.TaskInstances
                    .Include(i => i.TaskTemplate)
                    .Where(i => i.Status == "Pending" 
                             && i.AssignedUserId != null 
                             && i.DueDate > now 
                             && i.DueDate <= reminderThreshold
                             && i.LastReminderSent == null) // Only one reminder per task
                    .ToListAsync();
                
                // ==================== CRÉATION DES NOTIFICATIONS ====================
                foreach (var task in tasksToRemind)
                {
                    // Calcule le temps restant avant l'échéance
                    var timeUntilDue = task.DueDate - now;
                    // Formatte le temps restant (ex: "2h 30m" ou "1h")
                    string timeStr = timeUntilDue.Minutes > 0 
                        ? $"{timeUntilDue.Hours}h {timeUntilDue.Minutes}m" 
                        : $"{timeUntilDue.Hours}h";

                    // Crée une notification pour l'utilisateur assigné
                    context.Notifications.Add(new Notification
                    {
                        UserId = task.AssignedUserId.Value,
                        Title = "Rappel de mission ! ⏰",
                        Message = $"Votre tâche '{task.TaskTemplate.Title}' est prévue dans environ {timeStr}. N'oubliez pas de la faire !",
                        Type = "Warning",
                        ActionUrl = "/Tasks",
                        RelatedEntityType = "TaskInstance",
                        RelatedEntityId = task.Id,
                        CreatedAt = DateTime.UtcNow
                    });

                    task.LastReminderSent = DateTime.UtcNow;
                }

                if (tasksToRemind.Any())
                {
                    await context.SaveChangesAsync();
                    Console.WriteLine($"[TaskReminderService] Sent {tasksToRemind.Count} reminders at {now}");
                }
            }
        }
    }
}
