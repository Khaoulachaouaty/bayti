using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bayti.Models
{
    public class Colocation
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Le nom de la colocation est requis")]
        [MaxLength(100)]
        [Display(Name = "Nom de la colocation")]
        public string Name { get; set; }

        [Required]
        [MaxLength(10)]
        public string JoinCode { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Mode d'affectation : "Auto", "Manuel", "Participatif"
        [MaxLength(20)]
        public string AssignmentMode { get; set; } = "Auto";

        // Options
        public bool EnableNotifications { get; set; } = true;
        public bool EnableEmailReminders { get; set; } = true;
        public int LatePenaltyPoints { get; set; } = 0;

        // Navigation
        public virtual ICollection<ApplicationUser> Members { get; set; } = new List<ApplicationUser>();
        public virtual ICollection<Category> Categories { get; set; } = new List<Category>();
        public virtual ICollection<TaskTemplate> TaskTemplates { get; set; } = new List<TaskTemplate>();
        public virtual ICollection<Reward> Rewards { get; set; } = new List<Reward>();
    }
}