using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Bayti.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

builder.Services.AddHostedService<Bayti.Services.TaskReminderService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
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