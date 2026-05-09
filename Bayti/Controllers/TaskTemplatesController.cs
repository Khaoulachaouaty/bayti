using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Bayti.Data;
using Bayti.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Bayti.Controllers
{
    [Authorize]
    public class TaskTemplatesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TaskTemplatesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var colocationIdStr = User.FindFirstValue("ColocationId");
            if (string.IsNullOrEmpty(colocationIdStr)) return RedirectToAction("Index", "Home");

            int colocationId = int.Parse(colocationIdStr);

            var tasks = await _context.TaskTemplates
                .Include(t => t.Category)
                .Where(t => t.ColocationId == colocationId && t.IsActive)
                .ToListAsync();

            return View(tasks);
        }

        public async Task<IActionResult> Create()
        {
            var colocationIdStr = User.FindFirstValue("ColocationId");
            if (string.IsNullOrEmpty(colocationIdStr)) return RedirectToAction("Index", "Home");

            int colocationId = int.Parse(colocationIdStr);

            ViewBag.Categories = new SelectList(await _context.Categories
                .Where(c => c.ColocationId == colocationId)
                .ToListAsync(), "Id", "Name");

            return View(new TaskTemplate { StartDate = DateTime.Today });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TaskTemplate model)
        {
            var colocationIdStr = User.FindFirstValue("ColocationId");
            var userIdStr = User.FindFirstValue("UserId");
            
            if (string.IsNullOrEmpty(colocationIdStr)) return RedirectToAction("Index", "Home");

            int colocationId = int.Parse(colocationIdStr);
            model.ColocationId = colocationId;
            model.CreatedByUserId = !string.IsNullOrEmpty(userIdStr) ? int.Parse(userIdStr) : null;
            model.CreatedAt = DateTime.UtcNow;

            // Database doesn't allow NULLs for these columns even if model says string?
            model.WeeklyDays ??= "";
            model.Description ??= "";

            // Remove server-side and navigation properties from validation
            ModelState.Remove("ColocationId");
            ModelState.Remove("CreatedByUserId");
            ModelState.Remove("Colocation");
            ModelState.Remove("Category");
            ModelState.Remove("CreatedBy");

            if (model.RecurrenceType != "Weekly") ModelState.Remove("WeeklyDays");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Add(model);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    var msg = ex.Message;
                    if (ex.InnerException != null) msg += " | " + ex.InnerException.Message;
                    ModelState.AddModelError(string.Empty, "Erreur lors de la sauvegarde : " + msg);
                }
            }

            ViewBag.Categories = new SelectList(await _context.Categories
                .Where(c => c.ColocationId == colocationId)
                .ToListAsync(), "Id", "Name", model.CategoryId);

            return View(model);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var colocationIdStr = User.FindFirstValue("ColocationId");
            int colocationId = int.Parse(colocationIdStr);

            var task = await _context.TaskTemplates.FindAsync(id);
            if (task == null || task.ColocationId != colocationId) return NotFound();

            ViewBag.Categories = new SelectList(await _context.Categories
                .Where(c => c.ColocationId == colocationId)
                .ToListAsync(), "Id", "Name", task.CategoryId);

            return View(task);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TaskTemplate model)
        {
            if (id != model.Id) return NotFound();

            var colocationIdStr = User.FindFirstValue("ColocationId");
            int colocationId = int.Parse(colocationIdStr);

            // Remove server-side fields from validation
            ModelState.Remove("ColocationId");
            ModelState.Remove("CreatedByUserId");
            ModelState.Remove("Colocation");
            ModelState.Remove("Category");
            ModelState.Remove("CreatedBy");

            model.WeeklyDays ??= "";
            model.Description ??= "";

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(model);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TaskExists(model.Id)) return NotFound();
                    else throw;
                }
                catch (Exception ex)
                {
                    var msg = ex.Message;
                    if (ex.InnerException != null) msg += " | " + ex.InnerException.Message;
                    ModelState.AddModelError(string.Empty, "Erreur lors de la sauvegarde : " + msg);
                }
            }

            ViewBag.Categories = new SelectList(await _context.Categories
                .Where(c => c.ColocationId == colocationId)
                .ToListAsync(), "Id", "Name", model.CategoryId);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var colocationIdStr = User.FindFirstValue("ColocationId");
            int colocationId = int.Parse(colocationIdStr);

            var task = await _context.TaskTemplates.FirstOrDefaultAsync(t => t.Id == id && t.ColocationId == colocationId);
            if (task != null)
            {
                task.IsActive = false; // Soft delete
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> TogglePause(int id)
        {
            var colocationIdStr = User.FindFirstValue("ColocationId");
            int colocationId = int.Parse(colocationIdStr);

            var task = await _context.TaskTemplates.FirstOrDefaultAsync(t => t.Id == id && t.ColocationId == colocationId);
            if (task != null)
            {
                task.IsPaused = !task.IsPaused;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool TaskExists(int id)
        {
            return _context.TaskTemplates.Any(e => e.Id == id);
        }
    }
}
