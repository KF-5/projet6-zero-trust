using System.ComponentModel.DataAnnotations;

namespace P6Explorer.Web.Models;

/// <summary>Formulaire d'envoi d'une capture d'écran de preuve (IFormFile, comme MovieVM du TP3).</summary>
public class PreuveFormVM
{
    public const long TailleMax = 5 * 1024 * 1024;

    public int EtapeId { get; set; }

    [Required(ErrorMessage = "Choisis une image.")]
    [Display(Name = "Capture d'écran (PNG ou JPEG, 5 Mo au plus)")]
    public IFormFile? Fichier { get; set; }

    [StringLength(200, ErrorMessage = "200 caractères au plus.")]
    [Display(Name = "Légende")]
    public string? Legende { get; set; }
}
