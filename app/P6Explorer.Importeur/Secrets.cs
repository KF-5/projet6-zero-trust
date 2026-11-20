using System.Text.RegularExpressions;

namespace P6Explorer.Importeur;

/// <summary>
/// Aucun secret dans les données (prompt, règle 5) : mots de passe, clés et jetons remplacés par « •••••• ».
/// Les références à une variable (${PG_APP_PWD}, $APP_PWD) ne sont pas des secrets et restent visibles.
/// </summary>
public static partial class Secrets
{
    public const string Masque = "••••••";

    public static string? Masquer(string? texte)
    {
        if (string.IsNullOrEmpty(texte)) return texte;
        var t = CleePrivee().Replace(texte, "-----BEGIN PRIVATE KEY-----\n" + Masque + "\n-----END PRIVATE KEY-----");
        t = CleSsh().Replace(t, m => m.Groups[1].Value + " " + Masque);
        t = Affectation().Replace(t, m => EstReference(m.Groups[3].Value) ? m.Value : m.Groups[1].Value + m.Groups[2].Value + Masque);
        t = PasswordSql().Replace(t, m => EstReference(m.Groups[2].Value.Trim('\'')) ? m.Value : m.Groups[1].Value + "'" + Masque + "'");
        t = CurlUtilisateur().Replace(t, m => m.Groups[1].Value + Masque);
        t = DsnPassword().Replace(t, m => EstReference(m.Groups[2].Value) ? m.Value : m.Groups[1].Value + Masque);
        t = IdentifiantsParDefaut().Replace(t, m => m.Groups[1].Value + Masque + m.Groups[3].Value);
        return t;
    }

    /// <summary>Vrai si le texte contient encore quelque chose qui ressemble à un secret (utilisé par les tests).</summary>
    public static bool ContientSecret(string texte)
        => CleePriveeNonMasquee().IsMatch(texte)
           || CleSshNonMasquee().IsMatch(texte)
           || Affectation().Matches(texte).Any(m => !EstReference(m.Groups[3].Value) && m.Groups[3].Value != Masque)
           || CurlUtilisateur().Matches(texte).Any(m => !m.Value.Contains(Masque));

    private static bool EstReference(string valeur)
        => valeur.StartsWith('$') || valeur.StartsWith("\"$") || valeur.StartsWith("'$") || valeur.Length == 0
           || valeur.StartsWith(Masque) || valeur.StartsWith("<") || valeur.StartsWith("${") || valeur.StartsWith(":");

    [GeneratedRegex(@"-----BEGIN [A-Z ]*PRIVATE KEY-----.*?-----END [A-Z ]*PRIVATE KEY-----", RegexOptions.Singleline)]
    private static partial Regex CleePrivee();
    [GeneratedRegex(@"-----BEGIN [A-Z ]*PRIVATE KEY-----\s*[A-Za-z0-9+/=]{20}")]
    private static partial Regex CleePriveeNonMasquee();
    [GeneratedRegex(@"(ssh-(?:ed25519|rsa|dss)|ecdsa-sha2-nistp\d+)\s+AAAA\S*")]
    private static partial Regex CleSsh();
    [GeneratedRegex(@"ssh-(?:ed25519|rsa)\s+AAAA[A-Za-z0-9+/]{8,}")]
    private static partial Regex CleSshNonMasquee();
    // NOM_PASSWORD=valeur, NOM_PWD: valeur, PGPASSWORD=valeur, SECRET=valeur, TOKEN=valeur
    [GeneratedRegex(@"(\b[A-Z_]*(?:PASSWORD|_PWD|PGPASSWORD|SECRET|TOKEN|AUTHKEY)\b)(\s*[=:]\s*)([^\s,}""']+)")]
    private static partial Regex Affectation();
    // Mot-clé SQL en capitales (« LOGIN PASSWORD '…' ») : sensible à la casse, pour ne pas toucher
    // au texte ordinaire (« password authentication failed »).
    [GeneratedRegex(@"(PASSWORD\s+)('[^']*'|\S+)")]
    private static partial Regex PasswordSql();
    [GeneratedRegex(@"(-u\s+)(?!••)[A-Za-z0-9_.-]+:[A-Za-z0-9_.-]+")]
    private static partial Regex CurlUtilisateur();
    [GeneratedRegex(@"(password=)(\S+)")]
    private static partial Regex DsnPassword();
    // « connexion `installer` / `opnsense` » : identifiants par défaut de l'installateur OPNsense
    [GeneratedRegex(@"(`installer`\s*/\s*`)([^`]+)(`)")]
    private static partial Regex IdentifiantsParDefaut();
}
