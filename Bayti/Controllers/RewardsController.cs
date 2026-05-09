using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Bayti.Models;
using Bayti.Data;
using System.Security.Claims;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Bayti.Controllers
{
    public class RewardsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RewardsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Rewards (Boutique)
        public async Task<IActionResult> Index()
        {
            var colocationIdStr = User.FindFirstValue("ColocationId");
            if (string.IsNullOrEmpty(colocationIdStr)) return RedirectToAction("Login", "Account");
            int colocationId = int.Parse(colocationIdStr);

            var rewards = await _context.Rewards
                .Where(r => r.ColocationId == colocationId && r.IsActive)
                .OrderBy(r => r.Price)
                .ToListAsync();

            var isAdmin = User.FindFirstValue("IsAdmin") == "True";

            // Auto-generate defaults if empty and user is admin
            if (!rewards.Any() && isAdmin)
            {
                var defaults = new List<Reward>
                {
                    new Reward { Name = "Carte Joker", Description = "Évitez une corvée de votre choix sans perdre de points !", Emoji = "🃏", IconClass = "", Price = 150, IsJoker = true, ColocationId = colocationId, CreatedAt = DateTime.UtcNow },
                    new Reward { Name = "Choix du Film", Description = "Vous avez le pouvoir absolu sur la TV ce soir.", Emoji = "🍿", IconClass = "", Price = 50, IsJoker = false, ColocationId = colocationId, CreatedAt = DateTime.UtcNow },
                    new Reward { Name = "Grâce Matinée", Description = "Quelqu'un d'autre prépare le petit-déjeuner demain matin.", Emoji = "🥐", IconClass = "", Price = 100, IsJoker = false, ColocationId = colocationId, CreatedAt = DateTime.UtcNow },
                    new Reward { Name = "Pizza payée !", Description = "L'admin (ou la cagnotte) vous offre une pizza.", Emoji = "🍕", IconClass = "", Price = 300, IsJoker = false, ColocationId = colocationId, CreatedAt = DateTime.UtcNow }
                };
                
                _context.Rewards.AddRange(defaults);
                await _context.SaveChangesAsync();
                
                rewards = defaults.OrderBy(r => r.Price).ToList();
            }

            var userId = int.Parse(User.FindFirstValue("UserId"));
            var user = await _context.Users.FindAsync(userId);
            ViewBag.UserPoints = user?.Points ?? 0;

            return View(rewards);
        }

        [HttpPost]
        public async Task<IActionResult> Buy(int id)
        {
            var userIdStr = User.FindFirstValue("UserId");
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();
            int userId = int.Parse(userIdStr);

            var reward = await _context.Rewards.FindAsync(id);
            var user = await _context.Users.FindAsync(userId);

            if (reward != null && user != null && user.Points >= reward.Price)
            {
                // Deduct points
                user.Points -= reward.Price;

                // Record purchase
                var purchase = new Purchase
                {
                    UserId = userId,
                    RewardId = reward.Id,
                    PointsSpent = reward.Price,
                    PurchasedAt = DateTime.UtcNow
                };
                _context.Purchases.Add(purchase);

                // Record in history
                var history = new PointHistory
                {
                    UserId = userId,
                    PointsChange = -reward.Price,
                    PointsBalanceAfter = user.Points,
                    Reason = $"Achat boutique : {reward.Name}",
                    Type = "Purchase",
                    CreatedAt = DateTime.UtcNow
                };
                _context.PointHistory.Add(history);

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Vous avez acheté : {reward.Name} !";
            }
            else
            {
                TempData["ErrorMessage"] = "Fonds insuffisants ou récompense introuvable.";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Rewards/Create
        public IActionResult Create()
        {
            var isAdmin = User.FindFirstValue("IsAdmin") == "True";
            if (!isAdmin) return Forbid();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Reward model)
        {
            var isAdmin = User.FindFirstValue("IsAdmin") == "True";
            if (!isAdmin) return Forbid();

            var colocationIdStr = User.FindFirstValue("ColocationId");
            int colocationId = int.Parse(colocationIdStr);
            var userIdStr = User.FindFirstValue("UserId");

            model.ColocationId = colocationId;
            model.CreatedByUserId = int.Parse(userIdStr);
            model.CreatedAt = DateTime.UtcNow;
            
            // Handle nulls for form submission
            model.Description ??= "";
            model.Emoji ??= "🎁";
            model.IconClass ??= "";

            ModelState.Remove("ColocationId");
            ModelState.Remove("CreatedByUserId");
            ModelState.Remove("Colocation");
            ModelState.Remove("CreatedBy");
            ModelState.Remove("Purchases");

            if (ModelState.IsValid)
            {
                _context.Rewards.Add(model);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        // GET: Rewards/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var isAdmin = User.FindFirstValue("IsAdmin") == "True";
            if (!isAdmin) return Forbid();

            var colocationIdStr = User.FindFirstValue("ColocationId");
            int colocationId = int.Parse(colocationIdStr);

            var reward = await _context.Rewards
                .FirstOrDefaultAsync(r => r.Id == id && r.ColocationId == colocationId);

            if (reward == null) return NotFound();

            return View(reward);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Reward model)
        {
            var isAdmin = User.FindFirstValue("IsAdmin") == "True";
            if (!isAdmin) return Forbid();

            var colocationIdStr = User.FindFirstValue("ColocationId");
            int colocationId = int.Parse(colocationIdStr);

            var reward = await _context.Rewards
                .FirstOrDefaultAsync(r => r.Id == id && r.ColocationId == colocationId);

            if (reward == null) return NotFound();

            if (ModelState.IsValid)
            {
                reward.Name = model.Name;
                reward.Description = model.Description ?? "";
                reward.Price = model.Price;
                reward.Emoji = model.Emoji ?? "🎁";
                reward.IsJoker = model.IsJoker;
                reward.IsActive = model.IsActive;

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var isAdmin = User.FindFirstValue("IsAdmin") == "True";
            if (!isAdmin) return Forbid();

            var colocationIdStr = User.FindFirstValue("ColocationId");
            int colocationId = int.Parse(colocationIdStr);

            var reward = await _context.Rewards
                .FirstOrDefaultAsync(r => r.Id == id && r.ColocationId == colocationId);

            if (reward != null)
            {
                // Soft delete by setting IsActive to false
                reward.IsActive = false;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Récompense supprimée avec succès.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
