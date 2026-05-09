using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Bayti.Models;
using Bayti.Data;
using System.Security.Claims;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Bayti.Controllers
{
    public class PlanningController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PlanningController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(DateTime? date = null)
        {
            var colocationIdStr = User.FindFirstValue("ColocationId");
            if (string.IsNullOrEmpty(colocationIdStr)) return RedirectToAction("Login", "Account");
            int colocationId = int.Parse(colocationIdStr);

            var templates = await _context.TaskTemplates
                .Include(t => t.Category)
                .Where(t => t.ColocationId == colocationId && t.IsActive && !t.IsPaused)
                .ToListAsync();

            // Calculate current week dates (Monday to Sunday)
            var targetDate = date ?? DateTime.Today;
            int diff = (7 + (targetDate.DayOfWeek - DayOfWeek.Monday)) % 7;
            var startOfWeek = targetDate.AddDays(-1 * diff).Date;

            var weekPlanning = new Dictionary<DateTime, List<TaskTemplate>>();

            for (int i = 0; i < 7; i++)
            {
                var currentDate = startOfWeek.AddDays(i);
                var tasksForDay = new List<TaskTemplate>();
                int currentDayOfWeekNum = (int)currentDate.DayOfWeek == 0 ? 7 : (int)currentDate.DayOfWeek;
                int currentDayOfMonth = currentDate.Day;

                foreach (var t in templates)
                {
                    bool shouldGenerate = false;
                    
                    if (t.RecurrenceType == "Daily")
                    {
                        shouldGenerate = true;
                    }
                    else if (t.RecurrenceType == "Weekly" && !string.IsNullOrEmpty(t.WeeklyDays))
                    {
                        shouldGenerate = t.WeeklyDays.Contains(currentDayOfWeekNum.ToString());
                    }
                    else if (t.RecurrenceType == "Monthly" && t.MonthlyDay.HasValue)
                    {
                        shouldGenerate = (t.MonthlyDay.Value == currentDayOfMonth);
                    }
                    else if (t.RecurrenceType == "Once" && t.SpecificDate.HasValue)
                    {
                        shouldGenerate = (t.SpecificDate.Value.Date == currentDate);
                    }
                    else if (t.RecurrenceType == "Custom" && t.CustomIntervalDays.HasValue && t.CustomIntervalDays.Value > 0 && t.StartDate.HasValue)
                    {
                        int daysPassed = (currentDate - t.StartDate.Value.Date).Days;
                        shouldGenerate = (daysPassed >= 0 && daysPassed % t.CustomIntervalDays.Value == 0);
                    }

                    if (shouldGenerate)
                    {
                        tasksForDay.Add(t);
                    }
                }
                weekPlanning.Add(currentDate, tasksForDay);
            }

            ViewBag.StartOfWeek = startOfWeek;
            return View(weekPlanning);
        }
    }
}
