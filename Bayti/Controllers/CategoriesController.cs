using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Bayti.Data;
using Bayti.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Bayti.Controllers
{
    [Authorize]
    public class CategoriesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CategoriesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var colocationIdClaim = User.FindFirstValue("ColocationId");
            if (string.IsNullOrEmpty(colocationIdClaim)) return RedirectToAction("Setup", "Colocation");
            
            var colocationId = int.Parse(colocationIdClaim);

            var categories = await _context.Categories
                .Where(c => c.ColocationId == colocationId)
                .ToListAsync();

            return View(categories);
        }

        [HttpGet]
        public IActionResult Create()
        {
            if (User.FindFirstValue("IsAdmin") != "True") return Forbid();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Category category)
        {
            if (User.FindFirstValue("IsAdmin") != "True") return Forbid();

            var colocationIdClaim = User.FindFirstValue("ColocationId");
            if (string.IsNullOrEmpty(colocationIdClaim)) return RedirectToAction("Setup", "Colocation");
            
            category.ColocationId = int.Parse(colocationIdClaim);

            if (ModelState.IsValid)
            {
                _context.Categories.Add(category);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (User.FindFirstValue("IsAdmin") != "True") return Forbid();

            var colocationIdClaim = User.FindFirstValue("ColocationId");
            if (string.IsNullOrEmpty(colocationIdClaim)) return RedirectToAction("Setup", "Colocation");
            var colocationId = int.Parse(colocationIdClaim);

            var category = await _context.Categories.FindAsync(id);
            if (category == null || category.ColocationId != colocationId)
            {
                return NotFound();
            }
            return View(category);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Category category)
        {
            if (User.FindFirstValue("IsAdmin") != "True") return Forbid();

            var colocationIdClaim = User.FindFirstValue("ColocationId");
            if (string.IsNullOrEmpty(colocationIdClaim)) return RedirectToAction("Setup", "Colocation");
            var colocationId = int.Parse(colocationIdClaim);

            if (ModelState.IsValid)
            {

                var existing = await _context.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == category.Id);
                // Vérifie si la catégorie existe dans la base de données
                if (existing == null || existing.ColocationId != colocationId)
                {
                    return NotFound();
                }

                category.ColocationId = existing.ColocationId;
                _context.Update(category);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (User.FindFirstValue("IsAdmin") != "True") return Forbid();

            var colocationIdClaim = User.FindFirstValue("ColocationId");
            if (string.IsNullOrEmpty(colocationIdClaim)) return RedirectToAction("Setup", "Colocation");
            var colocationId = int.Parse(colocationIdClaim);

            var category = await _context.Categories.FindAsync(id);
            if (category != null && category.ColocationId == colocationId)
            {
                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
