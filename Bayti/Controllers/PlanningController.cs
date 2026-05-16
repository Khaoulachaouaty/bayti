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

        // Action qui affiche le planning hebdomadaire des tâches d'une colocation
        public async Task<IActionResult> Index(DateTime? date = null)
        {
            // 1. RÉCUPÉRATION DE L'ID COLOCATION DEPUIS LE COOKIE
            var colocationIdStr = User.FindFirstValue("ColocationId");
            if (string.IsNullOrEmpty(colocationIdStr)) return RedirectToAction("Login", "Account");
            int colocationId = int.Parse(colocationIdStr);

            // 2. CHARGEMENT DES TEMPLATES DE TÂCHES ACTIFS DE LA COLOCATION
            var templates = await _context.TaskTemplates
                .Include(t => t.Category)  
                .Where(t => t.ColocationId == colocationId && t.IsActive && !t.IsPaused)
                .ToListAsync();

            // 3. CALCUL DE LA SEMAINE À AFFICHER (Lundi ? Dimanche)
            var targetDate = date ?? DateTime.Today;  // Date cible = aujourd'hui ou paramètre
            int diff = (7 + (targetDate.DayOfWeek - DayOfWeek.Monday)) % 7;  // Décalage jusqu'à Lundi
            var startOfWeek = targetDate.AddDays(-1 * diff).Date;  // Date du Lundi de la semaine

            // 4. STRUCTURE POUR STOCKER LE PLANNING : Date ? Liste des tâches
            var weekPlanning = new Dictionary<DateTime, List<TaskTemplate>>();

            // 5. GÉNÉRATION DES TÂCHES POUR CHAQUE JOUR DE LA SEMAINE
            for (int i = 0; i < 7; i++)
            {
                var currentDate = startOfWeek.AddDays(i);  // Date du jour traité
                var tasksForDay = new List<TaskTemplate>();

                // Convertit DayOfWeek en nombre (Lundi=1, Dimanche=7)
                int currentDayOfWeekNum = (int)currentDate.DayOfWeek == 0 ? 7 : (int)currentDate.DayOfWeek;
                int currentDayOfMonth = currentDate.Day;  // Jour du mois (1-31)

                // 6. VÉRIFICATION POUR CHAQUE TEMPLATE SI ELLE DOIT ÊTRE GÉNÉRÉE CE JOUR
                foreach (var t in templates)
                {
                    bool shouldGenerate = false;

                    // 6.1 Quotidien : tous les jours
                    if (t.RecurrenceType == "Daily")
                    {
                        shouldGenerate = true;
                    }
                    // 6.2 Hebdomadaire : vérifie si le jour est dans la liste "WeeklyDays"
                    else if (t.RecurrenceType == "Weekly" && !string.IsNullOrEmpty(t.WeeklyDays))
                    {
                        // Ex: WeeklyDays = "1,3,5" ? Lundi, Mercredi, Vendredi
                        shouldGenerate = t.WeeklyDays.Contains(currentDayOfWeekNum.ToString());
                    }
                    // 6.3 Mensuel : vérifie si le jour du mois correspond
                    else if (t.RecurrenceType == "Monthly" && t.MonthlyDay.HasValue)
                    {
                        // Ex: MonthlyDay = 15 ? tous les 15 du mois
                        shouldGenerate = (t.MonthlyDay.Value == currentDayOfMonth);
                    }
                    // 6.4 Unique : une seule date précise
                    else if (t.RecurrenceType == "Once" && t.SpecificDate.HasValue)
                    {
                        shouldGenerate = (t.SpecificDate.Value.Date == currentDate);
                    }
                    // 6.5 Personnalisé : intervalle en jours
                    else if (t.RecurrenceType == "Custom" && t.CustomIntervalDays.HasValue && t.CustomIntervalDays.Value > 0 && t.StartDate.HasValue)
                    {
                        // Ex: Intervalle de 3 jours ? J0, J+3, J+6, J+9...
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

            // 7. PASSAGE DE LA DATE DE DÉBUT À LA VUE (pour navigation)
            ViewBag.StartOfWeek = startOfWeek;

            // 8. AFFICHAGE DU PLANNING
            return View(weekPlanning);
        }

    }
}