namespace P6Explorer.Importeur;

/// <summary>
/// Explication par défaut d'une commande, quand le guide ne la commente pas : une ou deux phrases simples,
/// pour un débutant en réseau.
/// </summary>
public static class Explications
{
    private static readonly Dictionary<string, string> ParCommande = new(StringComparer.Ordinal)
    {
        ["ping"] = "Envoie quelques paquets ICMP « echo » pour vérifier que la machine visée répond.",
        ["nc"] = "Tente d'ouvrir une connexion TCP vers un port, sans rien envoyer d'autre : on sait ainsi si le port est joignable.",
        ["curl"] = "Envoie une requête web (HTTP ou HTTPS) et affiche la réponse du serveur.",
        ["dig"] = "Pose une question DNS à un serveur précis (« quelle est l'adresse de ce nom ? ») et affiche la réponse.",
        ["ssh"] = "Ouvre une session chiffrée sur une autre machine, ou y lance une seule commande.",
        ["scp"] = "Copie un fichier vers (ou depuis) une autre machine, à travers une connexion SSH chiffrée.",
        ["nmap"] = "Scanne des ports pour voir lesquels répondent : c'est ce que fait un attaquant pour repérer les services.",
        ["hping3"] = "Fabrique des paquets TCP à la main et les envoie très vite : ici, pour simuler une attaque.",
        ["iperf3"] = "Mesure le débit (en Mbit/s) entre deux machines.",
        ["psql"] = "Se connecte à la base PostgreSQL et lance une requête SQL.",
        ["docker"] = "Pilote Docker : construire, démarrer, inspecter ou entrer dans un conteneur.",
        ["systemctl"] = "Démarre, arrête ou interroge un service du système (Debian).",
        ["service"] = "Démarre ou arrête un service (FreeBSD, sur OPNsense).",
        ["apt"] = "Installe des logiciels depuis les dépôts Debian (la carte NAT doit être branchée le temps du téléchargement).",
        ["apt-get"] = "Installe des logiciels depuis les dépôts Debian (la carte NAT doit être branchée le temps du téléchargement).",
        ["pfctl"] = "Pilote pf, le moteur de filtrage qui applique les règles d'OPNsense.",
        ["nft"] = "Règle ou affiche le pare-feu nftables de la machine Linux.",
        ["vmrun"] = "Pilote VMware en ligne de commande (instantanés, démarrage, arrêt).",
        ["git"] = "Commande Git : gère l'historique et les versions du dépôt du projet.",
        ["tcpdump"] = "Capture les paquets qui passent sur une carte réseau, pour les observer.",
        ["tshark"] = "Lit une capture et n'affiche que les paquets qui correspondent au filtre.",
        ["cat"] = "Affiche le contenu d'un fichier sans le modifier.",
        ["tail"] = "Affiche les dernières lignes d'un fichier (souvent un journal).",
        ["head"] = "Affiche les premières lignes d'un fichier.",
        ["grep"] = "Cherche les lignes qui contiennent un motif.",
        ["ls"] = "Liste les fichiers d'un dossier.",
        ["python3"] = "Lance un programme Python du projet.",
        ["ifconfig"] = "Affiche les cartes réseau et leurs adresses (FreeBSD, sur OPNsense).",
        ["ip"] = "Affiche ou règle les cartes réseau, les adresses et les routes (Linux).",
        ["Test-NetConnection"] = "Teste depuis Windows si une machine répond, et si un port TCP précis est ouvert.",
        ["openssl"] = "Crée un certificat et sa clé, pour que nginx puisse parler en HTTPS.",
        ["chronyc"] = "Interroge le service d'heure (chrony) : qui donne l'heure, avec quel écart.",
        ["configctl"] = "Commande interne d'OPNsense pour piloter ses services (ici Suricata).",
        ["suricata"] = "Lance Suricata pour vérifier les règles, ou pour analyser une capture enregistrée.",
        ["mkdir"] = "Crée un dossier.",
        ["install"] = "Crée un dossier (ou copie un fichier) avec le propriétaire et les droits voulus.",
        ["chmod"] = "Change les droits d'un fichier.",
        ["cd"] = "Se place dans un dossier.",
        ["tee"] = "Écrit le texte reçu dans un fichier.",
        ["jq"] = "Extrait des champs d'un fichier JSON (ici, les alertes de Suricata).",
        ["wc"] = "Compte des lignes.",
        ["column"] = "Met un fichier CSV en colonnes, pour le lire plus facilement.",
        ["nikto"] = "Scanne un site web à la recherche de failles connues.",
        ["netstat"] = "Affiche la table de routage ou les connexions de la machine.",
        ["dmesg"] = "Affiche les messages du noyau (ici, ceux de netmap).",
        ["pgrep"] = "Cherche un programme en cours d'exécution.",
        ["nsenter"] = "Entre dans l'espace réseau d'un conteneur, pour tester exactement depuis sa place.",
        ["wsl"] = "Gère le sous-système Linux de Windows (WSL).",
        ["sudo"] = "Lance la commande avec les droits d'administrateur (compte karim, mot de passe demandé une fois).",
        ["bash"] = "Lance un script shell du projet.",
        ["sh"] = "Lance une suite de commandes shell.",
        ["echo"] = "Affiche un texte (ou l'écrit dans un fichier).",
        ["sleep"] = "Attend quelques secondes.",
        ["named-checkconf"] = "Vérifie la syntaxe de la configuration de bind9.",
        ["named-checkzone"] = "Vérifie la syntaxe d'une zone DNS.",
        ["usermod"] = "Modifie un compte (ici, l'ajoute à un groupe).",
        ["ntpq"] = "Interroge le service d'heure d'OPNsense.",
        ["printf"] = "Affiche un texte mis en forme.",
        ["for"] = "Répète la même commande pour plusieurs valeurs.",
        ["foreach"] = "Répète la même commande pour chaque VM (boucle PowerShell).",
    };

    public static string PourCommande(string texte)
    {
        var jetons = Texte.Jetons(texte.Split('\n')[0]);
        while (jetons.Count > 0 && (jetons[0] is "sudo" or "timeout" or "nohup" || jetons[0].Contains('=')))
        {
            if (jetons[0] == "timeout" && jetons.Count > 1) jetons.RemoveAt(1);
            jetons.RemoveAt(0);
        }
        var cmd = jetons.FirstOrDefault() ?? "";
        if (cmd.StartsWith("foreach")) cmd = "foreach";
        if (cmd == "&" && texte.Contains("$vr")) cmd = "vmrun";
        if (cmd.StartsWith("Get-")) return "Commande PowerShell de lecture : affiche un réglage de Windows sans rien changer.";
        if (cmd.StartsWith("$")) return "Mémorise une valeur dans une variable PowerShell, réutilisée par les commandes suivantes.";
        if (cmd == "Select-String") return "Cherche un motif dans des fichiers (ici, les fichiers .vmx des VM).";
        if (cmd.EndsWith(".sh")) return "Lance un script shell du projet.";
        if (cmd == "docker" && jetons.Count > 1)
        {
            return jetons[1] switch
            {
                "compose" => "Docker Compose : construit ou démarre les conteneurs décrits dans le fichier du projet.",
                "exec" => $"Lance une commande à l'intérieur du conteneur {jetons.ElementAtOrDefault(2)}, avec son adresse réseau.",
                "run" => "Démarre un conteneur temporaire pour faire un test, puis le supprime (--rm).",
                "network" => "Gère les réseaux Docker (ici macvlan : chaque conteneur a sa propre adresse dans son VLAN).",
                "pull" => "Télécharge une image Docker (carte NAT branchée le temps du téléchargement).",
                "build" => "Construit une image Docker à partir d'un Dockerfile.",
                "inspect" => "Affiche les détails d'un conteneur (adresse, processus…).",
                "logs" => "Affiche ce que le conteneur a écrit sur sa sortie (ici, le journal d'accès).",
                _ => ParCommande["docker"]
            };
        }
        return ParCommande.TryGetValue(cmd, out var e) ? e : "Commande du guide, tapée sur la machine indiquée.";
    }
}
