using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Bayti.Data;
using Bayti.Models;
using Bayti.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace Bayti.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PasswordHasher<ApplicationUser> _passwordHasher;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
            _passwordHasher = new PasswordHasher<ApplicationUser>();
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity.IsAuthenticated)
                return RedirectToAction("Index", "Home");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _context.Users
                    .Include(u => u.Colocation)
                    .FirstOrDefaultAsync(u => u.Email == model.Email);

                if (user != null)
                {
                    var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);
                    if (result == PasswordVerificationResult.Success)
                    {
                        var claims = new List<Claim>
                        {
                            new Claim(ClaimTypes.Name, user.FullName),
                            new Claim(ClaimTypes.Email, user.Email),
                            new Claim("UserId", user.Id.ToString()),
                            new Claim("IsAdmin", user.IsAdmin.ToString()),
                            new Claim("ColocationId", user.ColocationId?.ToString() ?? ""),
                            new Claim("JoinCode", user.Colocation?.JoinCode ?? ""),
                            new Claim("AvatarUrl", user.AvatarUrl ?? "/images/avatars/avatar1.png")
                        };

                        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                        var authProperties = new AuthenticationProperties
                        {
                            IsPersistent = model.RememberMe
                        };

                        await HttpContext.SignInAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme,
                            new ClaimsPrincipal(claimsIdentity),
                            authProperties);

                        return RedirectToAction("Index", "Home");
                    }
                }
                ModelState.AddModelError(string.Empty, "Email ou mot de passe incorrect.");
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity.IsAuthenticated)
                return RedirectToAction("Index", "Home");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                if (await _context.Users.AnyAsync(u => u.Email == model.Email))
                {
                    ModelState.AddModelError("Email", "Cet email est déjà utilisé.");
                    return View(model);
                }

                var user = new ApplicationUser
                {
                    FullName = model.FullName,
                    Email = model.Email,
                    Points = 0,
                    CreatedAt = DateTime.UtcNow,
                    AvatarEmoji = "👤",
                    AvatarUrl = model.AvatarUrl ?? "/images/avatars/avatar1.png",
                    AvatarColor = "indigo"
                };

                user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // Connecter l'utilisateur après inscription
                return await Login(new LoginViewModel { Email = model.Email, Password = model.Password });
            }
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userIdStr = User.FindFirstValue("UserId");
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login");

            var user = await _context.Users
                .Include(u => u.Colocation)
                .FirstOrDefaultAsync(u => u.Id == int.Parse(userIdStr));

            if (user == null) return RedirectToAction("Login");

            var model = new ProfileViewModel
            {
                FullName = user.FullName,
                Email = user.Email,
                AvatarUrl = user.AvatarUrl ?? "/images/avatars/avatar1.png"
            };

            ViewBag.Points = user.Points;
            ViewBag.IsAdmin = user.IsAdmin;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileViewModel model)
        {
            if (ModelState.IsValid)
            {
                var userIdStr = User.FindFirstValue("UserId");
                if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login");

                var user = await _context.Users
                    .Include(u => u.Colocation)
                    .FirstOrDefaultAsync(u => u.Id == int.Parse(userIdStr));
                
                if (user == null) return RedirectToAction("Login");

                if (await _context.Users.AnyAsync(u => u.Email == model.Email && u.Id != user.Id))
                {
                    ModelState.AddModelError("Email", "Cet email est déjà utilisé.");
                    return View(model);
                }

                user.FullName = model.FullName;
                user.Email = model.Email;
                user.AvatarUrl = model.AvatarUrl;

                if (!string.IsNullOrEmpty(model.NewPassword))
                {
                    user.PasswordHash = _passwordHasher.HashPassword(user, model.NewPassword);
                }

                try 
                {
                    _context.Entry(user).State = EntityState.Modified;
                    await _context.SaveChangesAsync();

                    // Rafraîchir les claims
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, user.FullName),
                        new Claim(ClaimTypes.Email, user.Email),
                        new Claim("UserId", user.Id.ToString()),
                        new Claim("IsAdmin", user.IsAdmin.ToString()),
                        new Claim("ColocationId", user.ColocationId?.ToString() ?? ""),
                        new Claim("JoinCode", user.Colocation?.JoinCode ?? ""),
                        new Claim("AvatarUrl", user.AvatarUrl ?? "/images/avatars/avatar1.png")
                    };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme, 
                        new ClaimsPrincipal(claimsIdentity),
                        new AuthenticationProperties { IsPersistent = true });

                    TempData["Success"] = "Profil mis à jour avec succès !";
                    return RedirectToAction("Profile");
                }
                catch (Exception ex)
                {
                    TempData["Error"] = "Erreur lors de la sauvegarde : " + ex.Message;
                }
            }
            return View(model);
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }
    }
}
