using Microsoft.EntityFrameworkCore;
using Bayti.Models;

namespace Bayti.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<ApplicationUser> Users { get; set; }
        public DbSet<Colocation> Colocations { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<TaskTemplate> TaskTemplates { get; set; }
        public DbSet<TaskInstance> TaskInstances { get; set; }
        public DbSet<Availability> Availabilities { get; set; }
        public DbSet<Reward> Rewards { get; set; }
        public DbSet<Purchase> Purchases { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<PointHistory> PointHistory { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ApplicationUser>(entity =>
            {
                entity.HasIndex(u => u.Email).IsUnique();

                entity.HasOne(u => u.Colocation)
                    .WithMany(c => c.Members)
                    .HasForeignKey(u => u.ColocationId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Colocation>(entity =>
            {
                entity.HasIndex(c => c.JoinCode).IsUnique();
            });

            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasOne(c => c.Colocation)
                    .WithMany(col => col.Categories)
                    .HasForeignKey(c => c.ColocationId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<TaskTemplate>(entity =>
            {
                entity.HasOne(t => t.Category)
                    .WithMany(c => c.TaskTemplates)
                    .HasForeignKey(t => t.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(t => t.Colocation)
                    .WithMany(col => col.TaskTemplates)
                    .HasForeignKey(t => t.ColocationId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ===== CONFIGURATION CORRIGÉE DE TASKINSTANCE =====
            modelBuilder.Entity<TaskInstance>(entity =>
            {
                // Relation avec TaskTemplate
                entity.HasOne(ti => ti.TaskTemplate)
                    .WithMany(tt => tt.Instances)
                    .HasForeignKey(ti => ti.TaskTemplateId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Relation avec AssignedUser (celui qui doit faire la tâche)
                entity.HasOne(ti => ti.AssignedUser)
                    .WithMany(u => u.AssignedTasks)
                    .HasForeignKey(ti => ti.AssignedUserId)
                    .OnDelete(DeleteBehavior.SetNull);


                // Relation avec ClaimedBy (qui a pris la tâche) - NO ACTION pour éviter cascade multiple
                entity.HasOne(ti => ti.ClaimedBy)
                    .WithMany()
                    .HasForeignKey(ti => ti.ClaimedByUserId)
                    .OnDelete(DeleteBehavior.NoAction);

                // Index
                entity.HasIndex(ti => new { ti.TaskTemplateId, ti.DueDate });
                entity.HasIndex(ti => ti.AssignedUserId);
                entity.HasIndex(ti => ti.Status);

                // Default values
                entity.Property(ti => ti.Status)
                    .IsRequired()
                    .HasMaxLength(20)
                    .HasDefaultValue("Pending");
            });
            // ===== FIN CONFIGURATION =====

            modelBuilder.Entity<Availability>(entity =>
            {
                entity.HasOne(a => a.User)
                    .WithMany(u => u.Availabilities)
                    .HasForeignKey(a => a.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(a => new { a.UserId, a.DayKey }).IsUnique();
            });

            modelBuilder.Entity<Reward>(entity =>
            {
                entity.HasOne(r => r.Colocation)
                    .WithMany(c => c.Rewards)
                    .HasForeignKey(r => r.ColocationId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Purchase>(entity =>
            {
                entity.HasOne(p => p.User)
                    .WithMany(u => u.Purchases)
                    .HasForeignKey(p => p.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(p => p.Reward)
                    .WithMany(r => r.Purchases)
                    .HasForeignKey(p => p.RewardId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasOne(n => n.User)
                    .WithMany(u => u.Notifications)
                    .HasForeignKey(n => n.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                modelBuilder.Entity<PointHistory>(entity =>
                {
                    entity.HasOne(ph => ph.User)
                        .WithMany(u => u.PointHistory)
                        .HasForeignKey(ph => ph.UserId)
                        .OnDelete(DeleteBehavior.Cascade);

                    entity.HasOne(ph => ph.TaskInstance)
                        .WithMany()
                        .HasForeignKey(ph => ph.TaskInstanceId)
                        .OnDelete(DeleteBehavior.SetNull);
                });
            });
        }
    }
}