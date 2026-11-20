using System.Linq.Expressions;
using P6Explorer.Domain.Commandes;
using P6Explorer.Domain.Moteur;
using P6Explorer.Domain.Suivi;
using P6Explorer.Domain.Topologie;

namespace P6Explorer.Domain.Interfaces;

/// <summary>Accès générique aux données : enregistré une fois pour toutes les entités (AddScoped(typeof(…))).</summary>
public interface IGenericRepository<T> where T : class
{
    Task<T?> TrouverAsync(params object[] cle);
    Task<IReadOnlyList<T>> ListerAsync(Expression<Func<T, bool>>? filtre = null);
    Task<int> CompterAsync(Expression<Func<T, bool>>? filtre = null);
    Task AjouterAsync(T entite);
    void Modifier(T entite);
    void Supprimer(T entite);
    Task<int> EnregistrerAsync();
}

/// <summary>Une page de résultats (pagination Skip/Take).</summary>
public sealed record Page<T>(IReadOnlyList<T> Elements, int Numero, int Taille, int Total)
{
    public int NombrePages => Math.Max(1, (int)Math.Ceiling(Total / (double)Taille));
}

/// <summary>Rôles et étapes, avec leurs commandes quand il le faut.</summary>
public interface IRoleRepository : IGenericRepository<Role>
{
    /// <summary>Les 6 rôles, avec leurs étapes (sans les commandes), triés par numéro.</summary>
    Task<IReadOnlyList<Role>> ListerAvecEtapesAsync();

    /// <summary>Nombre de commandes de premier niveau par rôle.</summary>
    Task<IReadOnlyDictionary<int, int>> CompterCommandesParRoleAsync();
}

/// <summary>Étapes, commandes et effets (lecture du lecteur, édition).</summary>
public interface IEtapeRepository : IGenericRepository<Etape>
{
    /// <summary>Une étape pour sa page : pannes, critères, preuves et commandes de premier niveau (sans effets).</summary>
    Task<Etape?> ChargerPageAsync(int role, int numero);

    /// <summary>Commandes, sous-commandes et effets d'une étape (Include / ThenInclude), dans l'ordre.</summary>
    Task<IReadOnlyList<Commande>> ListerCommandesAsync(int etapeId);

    /// <summary>Étape suivie par le contexte, pour la modifier.</summary>
    Task<Etape?> ChargerPourEditionAsync(int id);

    /// <summary>Commande suivie, avec ses effets, ses sous-commandes et son étape.</summary>
    Task<Commande?> ChargerCommandeAsync(int id);

    /// <summary>Supprime une commande suivie, avec ses effets et ses sous-commandes (chacun écrit dans l'historique).</summary>
    void SupprimerCommande(Commande commande);

    /// <summary>Recopie les valeurs d'un objet du même type dans une entité suivie (seuls les champs changés sont écrits).</summary>
    void CopierValeurs(object cible, object source);

    /// <summary>Date de dernière modification (propriété shadow) de chaque étape d'un rôle.</summary>
    Task<IReadOnlyDictionary<int, DateTime>> DatesModificationAsync(int role);
}

/// <summary>Historique des modifications, écrit par l'intercepteur.</summary>
public interface IRevisionRepository
{
    Task<Page<Revision>> ListerAsync(int etapeId, int page, int taille);
    /// <summary>Remet les valeurs « avant » d'un lot de révisions ; renvoie un message pour l'utilisateur.</summary>
    Task<string> AnnulerLotAsync(Guid lot);
}

/// <summary>Topologie d'un état (A ou B), pour le schéma et le moteur réseau.</summary>
public interface ITopologieRepository
{
    Task<EtatReseau?> ChargerEtatAsync(string code);
    Task<IReadOnlyList<Noeud>> ListerNoeudsAsync(string etat);
    Task<IReadOnlyList<Reseau>> ListerReseauxAsync(string etat);
    Task<IReadOnlyList<Lien>> ListerLiensAsync(string etat);
    /// <summary>Toutes les machines (cartes des deux états), réseaux, règles et redirections : le moteur filtre lui-même.</summary>
    Task<TopologieSimulee> ChargerPourMoteurAsync(string etat);
}

/// <summary>Quiz d'auto-évaluation et tentatives.</summary>
public interface IQuizRepository
{
    Task<Quiz?> ChargerParEtapeAsync(int etapeId);
    Task<Quiz?> ChargerAsync(int quizId);
    Task AjouterTentativeAsync(Tentative tentative);
    Task<IReadOnlyList<Tentative>> TentativesAsync(string utilisateurId, int quizId);
    /// <summary>Meilleur score de chaque utilisateur pour chaque étape (group by + Max).</summary>
    Task<IReadOnlyList<MeilleurScore>> MeilleursScoresAsync();
    /// <summary>Étapes dont un quiz a été réussi par cet utilisateur.</summary>
    Task<IReadOnlySet<int>> EtapesMaitriseesAsync(string utilisateurId);
    Task<Question?> ChargerQuestionAsync(int id);
    Task<int> EnregistrerAsync();
}

public sealed record MeilleurScore(string UtilisateurId, int EtapeId, double Score, DateTime Date, int Tentatives, double Seuil);

/// <summary>Commandes cochées « j'ai compris ».</summary>
public interface IProgressionRepository
{
    Task<IReadOnlySet<int>> CommandesComprisesAsync(string utilisateurId, int etapeId);
    Task BasculerAsync(string utilisateurId, int commandeId, DateTime date);
    /// <summary>Commandes comprises par rôle pour un utilisateur.</summary>
    Task<IReadOnlyDictionary<int, int>> CompterParRoleAsync(string utilisateurId);
}

/// <summary>Questions et réponses sur les commandes.</summary>
public interface IDiscussionRepository
{
    Task<Page<Fil>> ListerAsync(int? commandeId, StatutFiltreFil statut, int page, int taille);
    Task<Fil?> ChargerAsync(int id);
    Task AjouterAsync(Fil fil);
    Task AjouterMessageAsync(Message message);
    Task<IReadOnlyDictionary<int, int>> OuvertsParEtapeAsync(int role);
    Task<IReadOnlyDictionary<int, int>> OuvertsParCommandeAsync(int etapeId);
    Task<int> EnregistrerAsync();
}

public enum StatutFiltreFil { Tous, Ouverts, Resolus }

/// <summary>Historique des simulations « Et si ? » et sélection pour la démonstration.</summary>
public interface ISimulationRepository : IGenericRepository<Simulation>
{
    Task<Page<Simulation>> ListerAsync(int page, int taille);
    Task<IReadOnlyList<Simulation>> ListerDemoAsync();
}

/// <summary>Termes du glossaire, avec les étapes qui les emploient.</summary>
public interface IGlossaireRepository
{
    Task<IReadOnlyList<TermeGlossaire>> ListerAsync();
    Task<IReadOnlyList<TermeGlossaire>> ListerPourEtapeAsync(int etapeId);
}

/// <summary>Noms affichés des comptes (les comptes Identity vivent dans l'infrastructure).</summary>
public interface IAnnuaire
{
    Task<IReadOnlyDictionary<string, string>> NomsAsync(IEnumerable<string> identifiants);
    Task<IReadOnlyList<(string Id, string Nom, int? NumeroRole)>> MembresAsync();
}
