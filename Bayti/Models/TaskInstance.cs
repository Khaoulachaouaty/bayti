using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bayti.Models
{
    public class TaskInstance
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int TaskTemplateId { get; set; }

        [ForeignKey("TaskTemplateId")]
        public virtual TaskTemplate TaskTemplate { get; set; }

        public int? AssignedUserId { get; set; }

        [ForeignKey("AssignedUserId")]
        public virtual ApplicationUser AssignedUser { get; set; }

        public DateTime DueDate { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } = "Pending";

        public DateTime? CompletedAt { get; set; }

        public bool JokerUsed { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;


        public int? PointsAwarded { get; set; }

        // Pour mode participatif
        public int? ClaimedByUserId { get; set; }

        [ForeignKey("ClaimedByUserId")]
        public virtual ApplicationUser ClaimedBy { get; set; }

        public DateTime? ClaimedAt { get; set; }

        public DateTime? LastReminderSent { get; set; }

        [MaxLength(200)]
        public string Comments { get; set; } = "";
    }
}