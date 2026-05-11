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
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);

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
                
                // Only send reminders between 10 AM and 8 PM
                if (now.Hour < 10 || now.Hour > 20) return;

                var today = DateTime.Today;

                // Find pending tasks for today that haven't been reminded today
                var tasksToRemind = await context.TaskInstances
                    .Include(i => i.TaskTemplate)
                    .Where(i => i.Status == "Pending" 
                             && i.AssignedUserId != null 
                             && i.DueDate.Date == today
                             && (i.LastReminderSent == null || i.LastReminderSent.Value.Date < today))
                    .ToListAsync();

                foreach (var task in tasksToRemind)
                {
                    context.Notifications.Add(new Notification
                    {
                        UserId = task.AssignedUserId.Value,
                        Title = "Rappel Automatique ⏰",
                        Message = $"N'oubliez pas votre tâche : {task.TaskTemplate.Title}. Elle est à faire pour aujourd'hui !",
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
