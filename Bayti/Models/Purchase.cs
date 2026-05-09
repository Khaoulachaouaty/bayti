using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bayti.Models
{
    public class Purchase
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }

        [Required]
        public int RewardId { get; set; }

        [ForeignKey("RewardId")]
        public virtual Reward Reward { get; set; }

        public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;

        public int PointsSpent { get; set; }

        public int? JokerAppliedToTaskId { get; set; }

        [ForeignKey("JokerAppliedToTaskId")]
        public virtual TaskInstance JokerAppliedToTask { get; set; }

        public DateTime? JokerUsedAt { get; set; }

        public string Status { get; set; } = "Completed";
    }
}