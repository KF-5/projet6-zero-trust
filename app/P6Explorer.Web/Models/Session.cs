using System.Globalization;

namespace P6Explorer.Web.Models;

/// <summary>Préférences de visite gardées en session : état A ou B affiché, vitesse du lecteur.</summary>
public static class PreferencesSession
{
    private const string CleEtat = "p6.etat";
    private const string CleVitesse = "p6.vitesse";

    public static string Etat(this ISession s) => s.GetString(CleEtat) is "B" ? "B" : "A";
    public static void ChoisirEtat(this ISession s, string etat) => s.SetString(CleEtat, etat == "B" ? "B" : "A");

    public static double Vitesse(this ISession s)
        => double.TryParse(s.GetString(CleVitesse), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 1;
    public static void ChoisirVitesse(this ISession s, double v)
        => s.SetString(CleVitesse, Math.Clamp(v, 0.5, 4).ToString(CultureInfo.InvariantCulture));
}
