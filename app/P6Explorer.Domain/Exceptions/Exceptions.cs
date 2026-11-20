namespace P6Explorer.Domain.Exceptions;

/// <summary>Erreur métier : message en français, affichable tel quel.</summary>
public class ExceptionMetier(string message) : Exception(message);

public sealed class EntiteIntrouvableException(string entite, object cle)
    : ExceptionMetier($"{entite} introuvable : {cle}.");

public sealed class DonneesAmorcageInvalidesException(string fichier, string detail)
    : ExceptionMetier($"Fichier d'amorçage invalide ({fichier}) : {detail}");
