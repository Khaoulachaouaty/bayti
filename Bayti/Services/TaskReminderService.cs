using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Bayti.Data;
using Bayti.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Bayti.Services
{
    public class TaskReminderService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(15); // Check more frequently

        public TaskReminderService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndSendReminders();
                }
                catch (Exception ex)
                {
                    // Log error if needed (using a logger would be better)
                    Console.WriteLine($"Error in TaskReminderService: {ex.Message}");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private async Task CheckAndSendReminders()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var now = DateTime.Now;
                
                // We want to remind 1 hour before the task is due
                var reminderThreshold = now.AddHours(1);

                var tasksToRemind = await context.TaskInstances
                    .Include(i => i.TaskTemplate)
                    .Where(i => i.Status == "Pending" 
                             && i.AssignedUserId != null 
                             && i.DueDate > now 
                             && i.DueDate <= reminderThreshold
                             && i.LastReminderSent == null) // Only one reminder per task
                    .ToListAsync();

                foreach (var task in tasksToRemind)
                {
                    var timeUntilDue = task.DueDate - now;
                    string timeStr = timeUntilDue.Minutes > 0 
                        ? $"{timeUntilDue.Hours}h {timeUntilDue.Minutes}m" 
                        : $"{timeUntilDue.Hours}h";

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
