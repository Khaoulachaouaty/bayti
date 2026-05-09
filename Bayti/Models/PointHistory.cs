using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bayti.Models
{
    public class PointHistory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }

        public int PointsChange { get; set; }

        public int PointsBalanceAfter { get; set; }

        [Required]
        [MaxLength(200)]
        public string Reason { get; set; }

        [MaxLength(30)]
        public string Type { get; set; }

        public int? TaskInstanceId { get; set; }

        [ForeignKey("TaskInstanceId")]
        public virtual TaskInstance TaskInstance { get; set; }

        public int? PurchaseId { get; set; }

        [ForeignKey("PurchaseId")]
        public virtual Purchase Purchase { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}