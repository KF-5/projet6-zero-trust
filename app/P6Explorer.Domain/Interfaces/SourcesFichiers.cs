using P6Explorer.Domain.Politique;
using P6Explorer.Domain.Video;

namespace P6Explorer.Domain.Interfaces;

/// <summary>
/// Fichiers du dépôt lus en direct (pas en base) : ils sont modifiés par l'équipe ou produits par ses scripts,
/// et l'application les affiche tels quels. Implémentations : P6Explorer.Infrastructure/Fichiers.
/// </summary>
public interface ISourceScenes
{
    /// <summary>Lit et vérifie donnees/video/scenes.json à chaque appel (modifiable sans redémarrer).</summary>
    ResultatValidation<FichierScenes> Charger();
}

public interface ISourcePolitique
{
    /// <summary>donnees/politique/*.csv, lus une fois au démarrage.</summary>
    PolitiqueLaboratoire Charger();
}

public interface ISourceSynthese
{
    /// <summary>resultats/synthese/synthese.csv s'il existe et est valide ; sinon l'exemple, marqué « illustration ».</summary>
    Synthese Charger();
}
