/*Ce fichier définit la structure et les règles de validation des données échangées entre 
les formulaires et les contrôleurs.*/
using System.ComponentModel.DataAnnotations;

namespace Bayti.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "L'email est requis")]
        [EmailAddress(ErrorMessage = "Format d'email invalide")]
        public string Email { get; set; }

        // Attribut [DataType(DataType.Password)] : 
        // - Dans le formulaire : masque les caractères (affiche des ●●●)
        // - Dans la base : ne stocke pas en clair (géré séparément)
        [Required(ErrorMessage = "Le mot de passe est requis")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        // -------------------- SE SOUVENIR DE MOI --------------------
        // booléen : true si l'utilisateur coche la case
        // Permet de garder la session active après fermeture du navigateur 
        // La durée du cookie sera plus longue (ex: 7 jours au lieu de session)
        public bool RememberMe { get; set; }
    }

    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Le nom complet est requis")]
        [MaxLength(100)]
        [Display(Name = "Nom complet")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "L'email est requis")]
        [EmailAddress(ErrorMessage = "Format d'email invalide")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Le mot de passe est requis")]
        [StringLength(100, ErrorMessage = "Le {0} doit compter au moins {2} caractères.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirmer le mot de passe")]
        [Compare("Password", ErrorMessage = "Les mots de passe ne correspondent pas.")]
        public string ConfirmPassword { get; set; }

        public string AvatarUrl { get; set; } = "/images/avatars/avatar1.png";
    }

    public class ProfileViewModel
    {
        [Required(ErrorMessage = "Le nom complet est requis")]
        [MaxLength(100)]
        [Display(Name = "Nom complet")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "L'email est requis")]
        [EmailAddress(ErrorMessage = "Format d'email invalide")]
        public string Email { get; set; }

        [Display(Name = "Lien de l'avatar")]
        public string? AvatarUrl { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Nouveau mot de passe (optionnel)")]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirmer le mot de passe")]
        [Compare("NewPassword", ErrorMessage = "Les mots de passe ne correspondent pas.")]
        public string? ConfirmPassword { get; set; }
    }
}
