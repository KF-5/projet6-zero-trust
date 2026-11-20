using P6Explorer.Application.ViewModels;
using P6Explorer.Domain.Enumerations;
using P6Explorer.Domain.Exceptions;
using P6Explorer.Domain.Interfaces;
using P6Explorer.Domain.Suivi;

namespace P6Explorer.Application.Services;

public interface IQuizService
{
    Task<QuizVM?> ChargerAsync(int role, int numero, string? utilisateurId);
    Task<ResultatQuizVM> CorrigerAsync(int quizId, IReadOnlyDictionary<int, List<int>> reponses, string utilisateurId);
    Task<PreparationVM> PreparationAsync();
    Task<QuestionFormVM?> QuestionAsync(int id);
    Task<int?> RoleDeQuestionAsync(int id);
    Task EnregistrerQuestionAsync(QuestionFormVM formulaire);
}

public interface IProgressionService
{
    Task BasculerAsync(string utilisateurId, int commandeId);
    Task<IReadOnlyDictionary<int, int>> ComprisesParRoleAsync(string utilisateurId);
    Task<IReadOnlySet<int>> EtapesMaitriseesAsync(string utilisateurId);
    Task<AvancementVM> AvancementEquipeAsync();
}

public interface IDiscussionService
{
    Task<DiscussionsVM> ListerAsync(string filtre, int page);
    Task<NouveauFilVM?> PreparerAsync(int commandeId);
    Task<int> CreerAsync(NouveauFilVM formulaire, string auteurId);
    Task<FilVM?> ChargerAsync(int id);
    Task<int?> RoleDuFilAsync(int id);
    Task RepondreAsync(NouveauMessageVM formulaire, string auteurId);
    Task ChangerStatutAsync(int id, bool resolu);
}

public interface IGlossaireService
{
    Task<List<TermeVM>> ListerAsync();
}

public class QuizService(IQuizRepository quiz, IRoleRepository roles, IEtapeRepository etapes, IAnnuaire annuaire, TimeProvider horloge) : IQuizService
{
    public async Task<QuizVM?> ChargerAsync(int role, int numero, string? utilisateurId)
    {
        var etape = (await etapes.ListerAsync(e => e.RoleNumero == role && e.Numero == numero)).FirstOrDefault();
        if (etape is null) return null;
        var q = await quiz.ChargerParEtapeAsync(etape.Id);
        if (q is null) return null;
        return new QuizVM
        {
            QuizId = q.Id, EtapeId = etape.Id, RoleNumero = role, EtapeNumero = numero, EtapeTitre = etape.Titre,
            Seuil = (int)Math.Round(q.Seuil * 100),
            Questions = q.Questions.OrderBy(x => x.Ordre).Select(x => new QuestionVM
            {
                Id = x.Id, Texte = x.Texte, Statut = x.Statut.ToString(), Source = x.Source,
                PlusieursReponses = x.Choix.Count(c => c.Correct) > 1,
                // Ordre des choix mélangé de façon stable (sinon la bonne réponse serait toujours au même endroit).
                Choix = x.Choix.OrderBy(c => (c.Id * 7919) % 101).Select(c => new ChoixVM { Id = c.Id, Texte = c.Texte }).ToList()
            }).ToList(),
            Tentatives = utilisateurId is null ? []
                : (await quiz.TentativesAsync(utilisateurId, q.Id)).Select(t => new TentativeVM { Date = t.Date, Score = (int)Math.Round(t.Score * 100) }).ToList()
        };
    }

    public async Task<ResultatQuizVM> CorrigerAsync(int quizId, IReadOnlyDictionary<int, List<int>> reponses, string utilisateurId)
    {
        var q = await quiz.ChargerAsync(quizId);
        if (q?.Etape is null) throw new EntiteIntrouvableException("Quiz", quizId);

        var tentative = new Tentative { QuizId = q.Id, UtilisateurId = utilisateurId, Date = horloge.GetUtcNow().UtcDateTime };
        var resultat = new ResultatQuizVM { RoleNumero = q.Etape.RoleNumero, EtapeNumero = q.Etape.Numero, EtapeTitre = q.Etape.Titre, Seuil = (int)Math.Round(q.Seuil * 100) };
        var justes = 0;
        foreach (var question in q.Questions.OrderBy(x => x.Ordre))
        {
            var cochees = (reponses.TryGetValue(question.Id, out var l) ? l : []).Where(id => question.Choix.Any(c => c.Id == id)).Distinct().ToHashSet();
            var bonnes = question.Choix.Where(c => c.Correct).Select(c => c.Id).ToHashSet();
            var juste = cochees.SetEquals(bonnes);
            if (juste) justes++;
            foreach (var c in cochees) tentative.Reponses.Add(new Reponse { ChoixId = c });   // table de jointure n-n
            resultat.Corrections.Add(new CorrectionQuestionVM
            {
                Question = question.Texte, Juste = juste,
                BonnesReponses = question.Choix.Where(c => c.Correct).Select(c => c.Texte).ToList(),
                TesReponses = question.Choix.Where(c => cochees.Contains(c.Id)).Select(c => c.Texte).ToList(),
                Explication = question.Choix.FirstOrDefault(c => c.Correct)?.Explication
            });
        }
        tentative.Score = q.Questions.Count == 0 ? 0 : (double)justes / q.Questions.Count;
        resultat.Score = (int)Math.Round(tentative.Score * 100);
        await quiz.AjouterTentativeAsync(tentative);
        return resultat;
    }

    public async Task<PreparationVM> PreparationAsync()
    {
        var membres = (await annuaire.MembresAsync()).OrderBy(m => m.NumeroRole ?? 9).ThenBy(m => m.Nom).ToList();
        var scores = await quiz.MeilleursScoresAsync();
        var parCle = scores.ToDictionary(s => (s.UtilisateurId, s.EtapeId));
        var vm = new PreparationVM { Membres = membres.Select(m => m.NumeroRole is null ? m.Nom : $"{m.Nom} (rôle {m.NumeroRole})").ToList() };
        foreach (var r in await roles.ListerAvecEtapesAsync())
            foreach (var e in r.Etapes)
            {
                var ligne = new LignePreparationVM { RoleNumero = r.Numero, Etape = $"{r.Numero}.{e.Numero} {e.Titre}" };
                foreach (var m in membres)
                    ligne.Scores.Add(parCle.TryGetValue((m.Id, e.Id), out var s)
                        ? new ScoreMembreVM { Score = (int)Math.Round(s.Score * 100), Date = s.Date, Tentatives = s.Tentatives, Reussi = s.Score >= s.Seuil }
                        : null);
                vm.Lignes.Add(ligne);
                if (!ligne.Scores.Any(x => x?.Reussi == true)) vm.PersonneNeMaitrise.Add(ligne.Etape);
            }
        vm.EtapesMaitrisees = vm.Lignes.Count - vm.PersonneNeMaitrise.Count;
        return vm;
    }

    public async Task<QuestionFormVM?> QuestionAsync(int id)
    {
        var q = await quiz.ChargerQuestionAsync(id);
        if (q?.Quiz is null) return null;
        var e = await etapes.TrouverAsync(q.Quiz.EtapeId);
        return new QuestionFormVM
        {
            Id = q.Id, Texte = q.Texte, Statut = q.Statut, RoleNumero = e?.RoleNumero ?? 0, EtapeNumero = e?.Numero ?? 0,
            Choix = q.Choix.OrderBy(c => c.Ordre).Select(c => new ChoixFormVM { Id = c.Id, Texte = c.Texte, Correct = c.Correct }).ToList()
        };
    }

    public async Task<int?> RoleDeQuestionAsync(int id)
    {
        var q = await quiz.ChargerQuestionAsync(id);
        return q?.Quiz is null ? null : (await etapes.TrouverAsync(q.Quiz.EtapeId))?.RoleNumero;
    }

    public async Task EnregistrerQuestionAsync(QuestionFormVM f)
    {
        var q = await quiz.ChargerQuestionAsync(f.Id) ?? throw new EntiteIntrouvableException("Question", f.Id);
        if (!f.Choix.Any(c => c.Correct)) throw new ExceptionMetier("Coche au moins une bonne réponse.");
        q.Texte = f.Texte.Trim();
        q.Statut = f.Statut;
        foreach (var c in f.Choix)
            if (q.Choix.FirstOrDefault(x => x.Id == c.Id) is { } choix) { choix.Texte = c.Texte.Trim(); choix.Correct = c.Correct; }
        await quiz.EnregistrerAsync();
    }
}

public class ProgressionService(IProgressionRepository progression, IQuizRepository quiz, IAnnuaire annuaire, TimeProvider horloge) : IProgressionService
{
    public Task BasculerAsync(string utilisateurId, int commandeId) => progression.BasculerAsync(utilisateurId, commandeId, horloge.GetUtcNow().UtcDateTime);

    public Task<IReadOnlyDictionary<int, int>> ComprisesParRoleAsync(string utilisateurId) => progression.CompterParRoleAsync(utilisateurId);

    public Task<IReadOnlySet<int>> EtapesMaitriseesAsync(string utilisateurId) => quiz.EtapesMaitriseesAsync(utilisateurId);

    public async Task<AvancementVM> AvancementEquipeAsync()
    {
        var vm = new AvancementVM();
        foreach (var m in (await annuaire.MembresAsync()).OrderBy(m => m.NumeroRole ?? 9))
            vm.Membres.Add(new AvancementMembreVM
            {
                Nom = m.Nom, NumeroRole = m.NumeroRole,
                CommandesComprises = (await progression.CompterParRoleAsync(m.Id)).Values.Sum(),
                EtapesMaitrisees = (await quiz.EtapesMaitriseesAsync(m.Id)).Count
            });
        return vm;
    }
}

public class DiscussionService(IDiscussionRepository fils, IEtapeRepository etapes, IAnnuaire annuaire, TimeProvider horloge) : IDiscussionService
{
    public const int TaillePage = 15;

    public async Task<DiscussionsVM> ListerAsync(string filtre, int page)
    {
        var statut = Enum.TryParse<StatutFiltreFil>(filtre, true, out var s) ? s : StatutFiltreFil.Ouverts;
        var p = await fils.ListerAsync(null, statut, page, TaillePage);
        var noms = await annuaire.NomsAsync(p.Elements.Select(f => f.AuteurId));
        return new DiscussionsVM
        {
            Filtre = statut.ToString(),
            Fils = new PageVM<FilLigneVM>
            {
                Page = p.Numero, NombrePages = p.NombrePages, Total = p.Total,
                Elements = p.Elements.Select(f => new FilLigneVM
                {
                    Id = f.Id, Titre = f.Titre, Auteur = noms.GetValueOrDefault(f.AuteurId, "?"), Statut = f.Statut.ToString(),
                    Date = f.Date, NbMessages = f.Messages.Count, CommandeCode = f.Commande?.Code ?? "",
                    RoleNumero = f.Commande?.Etape?.RoleNumero ?? 0, EtapeNumero = f.Commande?.Etape?.Numero ?? 0
                }).ToList()
            }
        };
    }

    public async Task<NouveauFilVM?> PreparerAsync(int commandeId)
    {
        var c = await etapes.ChargerCommandeAsync(commandeId);
        return c is null ? null : new NouveauFilVM { CommandeId = c.Id, CommandeCode = c.Code, CommandeTexte = c.Texte.Split('\n')[0] };
    }

    public async Task<int> CreerAsync(NouveauFilVM f, string auteurId)
    {
        _ = await etapes.ChargerCommandeAsync(f.CommandeId) ?? throw new EntiteIntrouvableException("Commande", f.CommandeId);
        var date = horloge.GetUtcNow().UtcDateTime;
        var fil = new Fil { CommandeId = f.CommandeId, AuteurId = auteurId, Titre = f.Titre.Trim(), Date = date };
        fil.Messages.Add(new Message { AuteurId = auteurId, Texte = f.Texte.Trim(), Date = date });
        await fils.AjouterAsync(fil);
        await fils.EnregistrerAsync();
        return fil.Id;
    }

    public async Task<FilVM?> ChargerAsync(int id)
    {
        var f = await fils.ChargerAsync(id);
        if (f is null) return null;
        var noms = await annuaire.NomsAsync(f.Messages.Select(m => m.AuteurId).Append(f.AuteurId));
        return new FilVM
        {
            Id = f.Id, Titre = f.Titre, Statut = f.Statut.ToString(), Auteur = noms.GetValueOrDefault(f.AuteurId, "?"),
            CommandeCode = f.Commande?.Code ?? "", CommandeTexte = f.Commande?.Texte.Split('\n')[0] ?? "",
            RoleNumero = f.Commande?.Etape?.RoleNumero ?? 0, EtapeNumero = f.Commande?.Etape?.Numero ?? 0,
            Messages = f.Messages.Select(m => new MessageVM { Auteur = noms.GetValueOrDefault(m.AuteurId, "?"), Date = m.Date, TexteHtml = TexteRiche.EnHtml(m.Texte) }).ToList(),
            Reponse = new NouveauMessageVM { FilId = f.Id }
        };
    }

    public async Task<int?> RoleDuFilAsync(int id) => (await fils.ChargerAsync(id))?.Commande?.Etape?.RoleNumero;

    public async Task RepondreAsync(NouveauMessageVM f, string auteurId)
    {
        var fil = await fils.ChargerAsync(f.FilId) ?? throw new EntiteIntrouvableException("Question", f.FilId);
        await fils.AjouterMessageAsync(new Message { FilId = fil.Id, AuteurId = auteurId, Texte = f.Texte.Trim(), Date = horloge.GetUtcNow().UtcDateTime });
        await fils.EnregistrerAsync();
    }

    public async Task ChangerStatutAsync(int id, bool resolu)
    {
        var fil = await fils.ChargerAsync(id) ?? throw new EntiteIntrouvableException("Question", id);
        fil.Statut = resolu ? StatutFil.Resolu : StatutFil.Ouvert;
        await fils.EnregistrerAsync();
    }
}

public class GlossaireService(IGlossaireRepository glossaire) : IGlossaireService
{
    public async Task<List<TermeVM>> ListerAsync()
        => (await glossaire.ListerAsync()).Select(t => new TermeVM
        {
            Id = t.Id, Terme = t.Terme, Definition = t.Definition, Comparaison = t.Comparaison,
            Etapes = t.Etapes.OrderBy(e => e.RoleNumero).ThenBy(e => e.Numero).Select(e => e.Code).ToList()
        }).ToList();
}
