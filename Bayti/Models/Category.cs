using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bayti.Models
{
    public class Category
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Le nom de la catégorie est requis")]
        [MaxLength(80)]
        [Display(Name = "Nom")]
        public string Name { get; set; }

        [MaxLength(100)]
        public string Icon { get; set; } = "🏠";

        [MaxLength(200)]
        public string Description { get; set; }

        public int? ColorCode { get; set; }

        [Required]
        public int ColocationId { get; set; }

        [ForeignKey("ColocationId")]
        public virtual Colocation? Colocation { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<TaskTemplate>? TaskTemplates { get; set; } = new List<TaskTemplate>();
    }
}