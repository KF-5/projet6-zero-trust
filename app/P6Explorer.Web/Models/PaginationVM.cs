namespace P6Explorer.Web.Models;

/// <summary>Liens de pagination (partielle _Pagination) : action cible et paramètres à garder.</summary>
public sealed record PaginationVM(int Page, int NombrePages, string Action, IDictionary<string, string?>? Parametres = null);
