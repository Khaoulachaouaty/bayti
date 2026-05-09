using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Bayti.Models;
using System.Security.Claims;
using Bayti.Data;

namespace Bayti.Controllers
{
    public class AvailabilitiesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AvailabilitiesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Availabilities
        public async Task<IActionResult> Index()
        {
            var userIdStr = User.FindFirstValue("UserId");
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login", "Account");
            
            int userId = int.Parse(userIdStr);
            var availabilities = await _context.Availabilities
                .Where(a => a.UserId == userId)
                .OrderBy(a => a.DayKey) // Or custom order
                .ToListAsync();

            var colocationIdStr = User.FindFirstValue("ColocationId");
            int colocationId = int.Parse(colocationIdStr);
            var colocation = await _context.Colocations.FindAsync(colocationId);
            ViewBag.AssignmentMode = colocation?.AssignmentMode ?? "Auto";

            // Ensure we have entries for all 7 days
            var dayKeys = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
            var existingKeys = availabilities.Select(a => a.DayKey).ToList();

            foreach (var key in dayKeys)
            {
                if (!existingKeys.Contains(key))
                {
                    var newAvail = new Availability
                    {
                        UserId = userId,
                        DayKey = key,
                        StartTime = "08:00",
                        EndTime = "20:00",
                        IsActive = true
                    };
                    _context.Availabilities.Add(newAvail);
                }
            }

            if (_context.ChangeTracker.HasChanges())
            {
                await _context.SaveChangesAsync();
                availabilities = await _context.Availabilities
                    .Where(a => a.UserId == userId)
                    .ToListAsync();
            }

            return View(availabilities);
        }

        [HttpPost]
        public async Task<IActionResult> Update(List<Availability> availabilities)
        {
            var userIdStr = User.FindFirstValue("UserId");
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();
            int userId = int.Parse(userIdStr);

            foreach (var item in availabilities)
            {
                var existing = await _context.Availabilities.FirstOrDefaultAsync(a => a.Id == item.Id && a.UserId == userId);
                if (existing != null)
                {
                    existing.StartTime = item.StartTime;
                    existing.EndTime = item.EndTime;
                    existing.IsActive = item.IsActive;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
