using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Bayti.Data;
using Bayti.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace Bayti.Controllers
{
    [Authorize]
    public class ColocationController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ColocationController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));
            var user = await _context.Users
                .Include(u => u.Colocation)
                .ThenInclude(c => c.Members)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user.ColocationId == null)
            {
                return RedirectToAction("Setup");
            }

            return View(user.Colocation);
        }

        [HttpGet]
        public IActionResult Setup()
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));
            var user = _context.Users.Find(userId);
            if (user.ColocationId != null)
            {
                return RedirectToAction("Index");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                ModelState.AddModelError("", "Le nom de la colocation est requis.");
                return View("Setup");
            }

            var userId = int.Parse(User.FindFirstValue("UserId"));
            var user = await _context.Users.FindAsync(userId);

            var colocation = new Colocation
            {
                Name = name,
                JoinCode = GenerateJoinCode(),
                CreatedAt = DateTime.UtcNow,
                AssignmentMode = "Auto"
            };

            _context.Colocations.Add(colocation);
            await _context.SaveChangesAsync();

            user.ColocationId = colocation.Id;
            user.IsAdmin = true; // Le créateur est admin
            await _context.SaveChangesAsync();

            // Refresh cookie with ColocationId
            await RefreshSignIn(user);

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Join(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                ModelState.AddModelError("", "Le code est requis.");
                return View("Setup");
            }

            var colocation = await _context.Colocations
                .FirstOrDefaultAsync(c => c.JoinCode == code.ToUpper());

            if (colocation == null)
            {
                ModelState.AddModelError("", "Code invalide.");
                return View("Setup");
            }

            var userId = int.Parse(User.FindFirstValue("UserId"));
            var user = await _context.Users.FindAsync(userId);

            user.ColocationId = colocation.Id;
            user.IsAdmin = false;
            await _context.SaveChangesAsync();

            await RefreshSignIn(user);

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Leave()
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));
            var user = await _context.Users.FindAsync(userId);

            if (user.IsAdmin)
            {
                // Si l'admin part, on pourrait soit supprimer la coloc, soit nommer un nouvel admin.
                // Ici, on suit la règle : suppression en cascade si admin part (ou s'il le décide explicitement).
                // Pour simplifier l'action "Quitter", on ne le permet pas à l'admin s'il est seul ou on lui demande de supprimer.
                TempData["Error"] = "En tant qu'administrateur, vous devez supprimer la colocation pour la quitter.";
                return RedirectToAction("Index");
            }

            user.ColocationId = null;
            await _context.SaveChangesAsync();

            await RefreshSignIn(user);

            return RedirectToAction("Setup");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete()
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));
            var user = await _context.Users.FindAsync(userId);

            if (!user.IsAdmin || user.ColocationId == null)
            {
                return Forbid();
            }

            var colocation = await _context.Colocations.FindAsync(user.ColocationId);
            
            // La suppression en cascade est gérée au niveau de la DB (Fluent API)
            _context.Colocations.Remove(colocation);
            
            // On remet l'admin en visiteur
            user.ColocationId = null;
            user.IsAdmin = false;
            
            await _context.SaveChangesAsync();
            await RefreshSignIn(user);

            return RedirectToAction("Setup");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAssignmentMode(string assignmentMode)
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));
            var user = await _context.Users.FindAsync(userId);

            if (!user.IsAdmin || user.ColocationId == null)
            {
                return Forbid();
            }

            var colocation = await _context.Colocations.FindAsync(user.ColocationId);
            if (colocation != null && new[] { "Auto", "Manuel", "Participatif" }.Contains(assignmentMode))
            {
                if (colocation.AssignmentMode != assignmentMode && (assignmentMode == "Manuel" || assignmentMode == "Participatif"))
                {
                    var pendingTasks = await _context.TaskInstances
                        .Include(t => t.TaskTemplate)
                        .Where(t => t.TaskTemplate.ColocationId == colocation.Id && t.Status == "Pending")
                        .ToListAsync();

                    foreach (var task in pendingTasks)
                    {
                        task.AssignedUserId = null;
                    }
                }

                colocation.AssignmentMode = assignmentMode;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }

        private string GenerateJoinCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 6)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private async Task RefreshSignIn(ApplicationUser user)
        {
            // Simple logic to re-sign in the user to update claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("UserId", user.Id.ToString()),
                new Claim("IsAdmin", user.IsAdmin.ToString()),
                new Claim("ColocationId", user.ColocationId?.ToString() ?? "")
            };
            var claimsIdentity = new System.Security.Claims.ClaimsIdentity(claims, Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));
        }
    }
}
