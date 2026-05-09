using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bayti.Models
{
    public class ApplicationUser
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Le nom complet est requis")]
        [MaxLength(100)]
        [Display(Name = "Nom complet")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "L'email est requis")]
        [EmailAddress(ErrorMessage = "Format d'email invalide")]
        [MaxLength(200)]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required]
        public string PasswordHash { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int? ColocationId { get; set; }

        [ForeignKey("ColocationId")]
        public virtual Colocation Colocation { get; set; }

        public bool IsAdmin { get; set; } = false;

        public int Points { get; set; } = 0;

        // Avatar par émoji (legacy)
        [MaxLength(10)]
        public string AvatarEmoji { get; set; } = "👤";

        [MaxLength(200)]
        [Display(Name = "Lien de l'avatar")]
        public string AvatarUrl { get; set; } = "/images/avatars/avatar1.png";

        [MaxLength(20)]
        public string AvatarColor { get; set; } = "sage";

        // Navigation
        public virtual ICollection<TaskInstance> AssignedTasks { get; set; } = new List<TaskInstance>();
        public virtual ICollection<Availability> Availabilities { get; set; } = new List<Availability>();
        public virtual ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();
        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public virtual ICollection<PointHistory> PointHistory { get; set; } = new List<PointHistory>();
    }
}