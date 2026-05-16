using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bayti.Models
{
    public class TaskTemplate
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Le titre est requis")]
        [MaxLength(150)]
        [Display(Name = "Titre")]
        public string Title { get; set; }

        [MaxLength(400)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        // Type de récurrence
        [Required]
        [MaxLength(20)]
        public string RecurrenceType { get; set; } = "Once"; // Once, Daily, Weekly, Monthly, Custom

        // Pour "Custom" : tous les X jours
        public int? CustomIntervalDays { get; set; }

        // Pour "Weekly" : jours de la semaine (ex: "1,3,5")
        [MaxLength(20)]
        public string? WeeklyDays { get; set; }

        // Pour "Monthly" : jour du mois (1-31)
        public int? MonthlyDay { get; set; }

        // Date spécifique pour tâches ponctuelles
        public DateTime? SpecificDate { get; set; }

        // Date de début
        public DateTime? StartDate { get; set; }

        // Heure préférée pour la tâche
        public TimeSpan? PreferredTime { get; set; }

        // Mise en pause
        public bool IsPaused { get; set; } = false;


        public int Points { get; set; } = 10;
        
        //Au lieux de suppression
        public bool IsActive { get; set; } = true;

        [Required]
        public int CategoryId { get; set; }

        [ForeignKey("CategoryId")]
        public virtual Category Category { get; set; }

        [Required]
        public int ColocationId { get; set; }

        [ForeignKey("ColocationId")]
        public virtual Colocation Colocation { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<TaskInstance> Instances { get; set; } = new List<TaskInstance>();
    }
}