using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bayti.Models
{
    public class Reward
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [MaxLength(300)]
        public string? Description { get; set; }

        [MaxLength(10)]
        public string? Emoji { get; set; } = "🎁";

        public int Price { get; set; }

        public bool IsJoker { get; set; } = false;

        public bool IsActive { get; set; } = true;

        [Required]
        public int ColocationId { get; set; }

        [ForeignKey("ColocationId")]
        public virtual Colocation? Colocation { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();
    }
}