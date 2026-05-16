using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Bayti.Data;

var builder = WebApplication.CreateBuilder(args);

// --- CONFIGURATION DES SERVICES (Conteneur de Dépendances) ---

// Ajoute le support des Contrôleurs avec Vues (système MVC)
builder.Services.AddControllersWithViews();

// Configure la connexion à la base de données SQL Server via la chaîne de connexion "DefaultConnection"
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure l'authentification basée sur les cookies
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";       // Page vers laquelle rediriger si non connecté
        options.LogoutPath = "/Account/Logout";     // Route de déconnexion
        options.ExpireTimeSpan = TimeSpan.FromDays(7); // Durée de session (7 jours)
        options.SlidingExpiration = true;           // Prolonge la session à chaque interaction
    });

// Enregistre le service de rappel (Background Service) qui tourne en arrière-plan
builder.Services.AddHostedService<Bayti.Services.TaskReminderService>();

// Construction de l'application
var app = builder.Build();

// --- CONFIGURATION DU PIPELINE DE REQUÊTE (Middlewares) ---

// Gestion des exceptions en mode Production
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Redirection automatique vers HTTPS
app.UseHttpsRedirection();

// Accès aux fichiers statiques (Images, CSS, JavaScript)
app.UseStaticFiles();

// Système de routage (analyse des URLs)
app.UseRouting();

// Middleware d'Authentification (vérifie qui est l'utilisateur via le cookie)
app.UseAuthentication();

// Middleware d'Autorisation (vérifie les permissions de l'utilisateur)
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

// Auto-sync database schema on startup (Manual Migration)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var db = context.Database;
        
        // Notifications
        db.ExecuteSqlRaw("IF COL_LENGTH('Notifications', 'Title') IS NULL ALTER TABLE Notifications ADD Title NVARCHAR(100) NOT NULL DEFAULT 'Alerte';");
        
        // Categories
        if (db.ExecuteSqlRaw("SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Categories') AND name = 'Icon'") == 0)
        {
            db.ExecuteSqlRaw("ALTER TABLE Categories ADD Icon NVARCHAR(100) NOT NULL DEFAULT '🏠';");
            db.ExecuteSqlRaw("IF COL_LENGTH('Categories', 'Emoji') IS NOT NULL UPDATE Categories SET Icon = Emoji;");
            db.ExecuteSqlRaw("IF COL_LENGTH('Categories', 'Emoji') IS NOT NULL ALTER TABLE Categories DROP COLUMN Emoji;");
            db.ExecuteSqlRaw("IF COL_LENGTH('Categories', 'IconClass') IS NOT NULL ALTER TABLE Categories DROP COLUMN IconClass;");
        }

        // Colocations
        db.ExecuteSqlRaw("IF COL_LENGTH('Colocations', 'AssignmentMode') IS NULL ALTER TABLE Colocations ADD AssignmentMode NVARCHAR(20) NOT NULL DEFAULT 'Auto';");
        
        // Users
        db.ExecuteSqlRaw("IF COL_LENGTH('Users', 'AvatarEmoji') IS NULL ALTER TABLE Users ADD AvatarEmoji NVARCHAR(10) NOT NULL DEFAULT '👤';");
        db.ExecuteSqlRaw("IF COL_LENGTH('Users', 'AvatarColor') IS NULL ALTER TABLE Users ADD AvatarColor NVARCHAR(20) NOT NULL DEFAULT 'indigo';");

        // TaskInstances - LastReminderSent
        db.ExecuteSqlRaw("IF COL_LENGTH('TaskInstances', 'LastReminderSent') IS NULL ALTER TABLE TaskInstances ADD LastReminderSent DATETIME NULL;");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Erreur lors de la synchronisation du schéma SQL.");
    }
}

app.Run();