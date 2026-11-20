using System.Buffers.Binary;
using System.Text;
using P6Explorer.Domain.Enumerations;

namespace P6Explorer.Domain.Moteur;

/// <summary>Un champ d'en-tête : où il est dans la trame, sa valeur lisible et ce qu'il veut dire.</summary>
public sealed record ChampTrame(string Nom, int Debut, int Longueur, string Valeur, string Explication, bool Exemple = false);

/// <summary>Une couche (Ethernet, 802.1Q, IPv4, TCP…) : une plage d'octets et ses champs.</summary>
public sealed record CoucheTrame(string Nom, string Code, int Debut, int Longueur, IReadOnlyList<ChampTrame> Champs);

/// <summary>La trame complète, telle qu'elle passe sur le premier câble du chemin.</summary>
public sealed record Trame(string Titre, byte[] Octets, IReadOnlyList<CoucheTrame> Couches);

/// <summary>
/// Construit les octets de la trame émise par la source (premier saut du chemin calculé par le moteur) et la décode :
/// Ethernet, 802.1Q si la carte de sortie est dans un VLAN (la trame passe par le trunk), IPv4, TCP / UDP / ICMP,
/// et un en-tête applicatif simple (DNS, HTTP, TLS ClientHello, NTP, syslog). Les sommes de contrôle sont réellement
/// calculées. Les valeurs qu'on ne peut pas connaître (adresses MAC, port source, numéro de séquence, identifiant IP,
/// nombre aléatoire TLS) sont des exemples fixes, marqués comme tels. Le FCS Ethernet n'est pas montré
/// (les captures ne le contiennent pas).
/// </summary>
public static class DecodeurPaquet
{
    private const int PortSourceExemple = 51514;
    private const uint SequenceExemple = 0x6a3f1c20;

    public static Trame? Construire(TopologieSimulee t, DemandeSimulation demande, ResultatSimulation r)
    {
        if (r.Chemin.Count < 2) return null;
        var premier = r.Chemin[0];
        var sortie = t.CartesDe(demande.Source).FirstOrDefault(c => c.Nom == premier.Sortie);
        var suivant = r.Chemin[1];
        var entree = t.CartesDe(suivant.Noeud).FirstOrDefault(c => c.Nom == suivant.Entree);
        if (sortie is null || entree is null) return null;
        var port = demande.Port ?? SimulateurPaquet.PortHabituel(demande.Protocole);
        return Construire(demande.Protocole, sortie.Adresse, Ipv4.Lire(demande.Destination), port,
            Mac(sortie), Mac(entree), sortie.Vlan, $"Trame émise par {sortie.Noeud.Nom} ({sortie.Nom}) vers {entree.Noeud.Nom} ({entree.Nom})");
    }

    /// <summary>Adresse MAC d'exemple, stable pour une carte (préfixe 00:0c:29 des cartes VMware).</summary>
    public static byte[] Mac(Carte c)
    {
        var h = 0u;
        foreach (var ch in $"{c.Noeud.Id.Valeur}/{c.Nom}") h = h * 31 + ch;
        return [0x00, 0x0c, 0x29, (byte)(h >> 16), (byte)(h >> 8), (byte)h];
    }

    public static Trame Construire(Protocole protocole, Ipv4 source, Ipv4 destination, int? port, byte[] macSource, byte[] macDestination, int? vlan, string titre)
    {
        var transport = protocole.Transport();
        var (charge, coucheApp) = ChargeApplicative(protocole, destination, port);
        var couches = new List<CoucheTrame>();
        var o = new List<byte>();

        // ---------- Ethernet (+ 802.1Q) ----------
        o.AddRange(macDestination);
        o.AddRange(macSource);
        var eth = new List<ChampTrame>
        {
            new("MAC destination", 0, 6, MacTexte(macDestination), "Carte du saut suivant (la passerelle, ou la machine elle-même sur le même segment), trouvée par ARP.", true),
            new("MAC source", 6, 6, MacTexte(macSource), "Carte qui émet la trame.", true),
        };
        if (vlan is int v)
        {
            o.AddRange([0x81, 0x00]);
            o.AddRange(U16(v & 0x0fff));
            couches.Add(new CoucheTrame("Ethernet II", "eth", 0, 12, eth));
            couches.Add(new CoucheTrame("802.1Q (VLAN)", "vlan", 12, 4,
            [
                new("TPID", 12, 2, "0x8100", "Indique qu'une étiquette VLAN suit : la trame voyage sur le trunk entre p6-docker et OPNsense."),
                new("Priorité, DEI, VLAN", 14, 2, $"PCP 0, DEI 0, VLAN {v}", $"Le numéro {v} dit à OPNsense sur quelle sous-interface la trame arrive (et donc quelles règles s'appliquent)."),
            ]));
        }
        var debutType = o.Count;
        o.AddRange([0x08, 0x00]);
        var typeEth = new ChampTrame("Type", debutType, 2, "0x0800 (IPv4)", "Ce qui suit est un paquet IPv4.");
        if (vlan is null) couches.Add(new CoucheTrame("Ethernet II", "eth", 0, 14, [.. eth, typeEth]));
        else couches.Add(new CoucheTrame("Type encapsulé", "vlan", debutType, 2, [typeEth]));

        // ---------- Transport + application ----------
        var segment = transport switch
        {
            Protocole.Tcp => SegmentTcp(source, destination, port ?? 0, charge, protocole),
            Protocole.Udp => SegmentUdp(source, destination, port ?? 0, charge),
            _ => Icmp(charge)
        };

        // ---------- IPv4 ----------
        var debutIp = o.Count;
        var ip = new byte[20];
        ip[0] = 0x45;
        BinaryPrimitives.WriteUInt16BigEndian(ip.AsSpan(2), (ushort)(20 + segment.Octets.Length));
        BinaryPrimitives.WriteUInt16BigEndian(ip.AsSpan(4), 0x1c46);
        BinaryPrimitives.WriteUInt16BigEndian(ip.AsSpan(6), 0x4000);
        ip[8] = 64;
        ip[9] = transport switch { Protocole.Tcp => 6, Protocole.Udp => 17, _ => 1 };
        BinaryPrimitives.WriteUInt32BigEndian(ip.AsSpan(12), source.Valeur);
        BinaryPrimitives.WriteUInt32BigEndian(ip.AsSpan(16), destination.Valeur);
        var somme = SommeControle(ip);
        BinaryPrimitives.WriteUInt16BigEndian(ip.AsSpan(10), somme);
        o.AddRange(ip);
        var nomProto = transport switch { Protocole.Tcp => "6 (TCP)", Protocole.Udp => "17 (UDP)", _ => "1 (ICMP)" };
        couches.Add(new CoucheTrame("IPv4", "ip", debutIp, 20,
        [
            new("Version, IHL", debutIp, 1, "4, 20 octets", "IPv4, en-tête de 5 mots de 32 bits (pas d'options)."),
            new("DSCP, ECN", debutIp + 1, 1, "0", "Pas de priorité particulière."),
            new("Longueur totale", debutIp + 2, 2, $"{20 + segment.Octets.Length} octets", "En-tête IP compris."),
            new("Identification", debutIp + 4, 2, "0x1c46", "Numéro du paquet, utile seulement s'il est fragmenté.", true),
            new("Drapeaux, fragment", debutIp + 6, 2, "DF (ne pas fragmenter)", "Le paquet ne doit pas être découpé en route."),
            new("TTL", debutIp + 8, 1, "64", "Nombre de routeurs qu'il peut encore traverser ; OPNsense le diminue de 1."),
            new("Protocole", debutIp + 9, 1, nomProto, "Ce que transporte le paquet."),
            new("Somme de contrôle", debutIp + 10, 2, $"0x{somme:x4}", "Calculée sur l'en-tête : un routeur qui reçoit un en-tête abîmé le jette."),
            new("Adresse source", debutIp + 12, 4, source.ToString(), "Qui envoie : c'est l'adresse que les règles « source » comparent."),
            new("Adresse destination", debutIp + 16, 4, destination.ToString(), "Adresse visée, avant toute traduction : la Destination NAT d'OPNsense la remplace à l'entrée du WAN."),
        ]));

        var debutSegment = o.Count;
        o.AddRange(segment.Octets);
        couches.Add(new CoucheTrame(segment.Nom, segment.Code, debutSegment, segment.LongueurEnTete,
            segment.Champs.Select(c => c with { Debut = c.Debut + debutSegment }).ToList()));
        if (coucheApp is not null && charge.Length > 0)
        {
            var debutApp = debutSegment + segment.LongueurEnTete;
            couches.Add(coucheApp with { Debut = debutApp, Champs = coucheApp.Champs.Select(c => c with { Debut = c.Debut + debutApp }).ToList() });
        }
        return new Trame(titre, [.. o], couches);
    }

    // ---------------------------------------------------------------- transport

    private sealed record Segment(string Nom, string Code, byte[] Octets, int LongueurEnTete, List<ChampTrame> Champs);

    private static Segment SegmentTcp(Ipv4 source, Ipv4 destination, int port, byte[] charge, Protocole protocole)
    {
        var donnees = charge.Length > 0;
        var s = new byte[20 + charge.Length];
        BinaryPrimitives.WriteUInt16BigEndian(s.AsSpan(0), PortSourceExemple);
        BinaryPrimitives.WriteUInt16BigEndian(s.AsSpan(2), (ushort)port);
        BinaryPrimitives.WriteUInt32BigEndian(s.AsSpan(4), donnees ? SequenceExemple + 1 : SequenceExemple);
        BinaryPrimitives.WriteUInt32BigEndian(s.AsSpan(8), donnees ? 0x0b4e2a91u : 0);
        s[12] = 0x50;
        s[13] = donnees ? (byte)0x18 : (byte)0x02;
        BinaryPrimitives.WriteUInt16BigEndian(s.AsSpan(14), 64240);
        charge.CopyTo(s, 20);
        var somme = SommeControle(PseudoEnTete(source, destination, 6, s.Length).Concat(s).ToArray());
        BinaryPrimitives.WriteUInt16BigEndian(s.AsSpan(16), somme);
        return new Segment("TCP", "tcp", s, 20,
        [
            new("Port source", 0, 2, PortSourceExemple.ToString(), "Port éphémère choisi par le système de la source.", true),
            new("Port destination", 2, 2, $"{port}{NomPort(port)}", "Le service visé : c'est le port que comparent les règles (« ports 80;443 »)."),
            new("Numéro de séquence", 4, 4, $"0x{(donnees ? SequenceExemple + 1 : SequenceExemple):x8}", "Numérote les octets envoyés.", true),
            new("Accusé de réception", 8, 4, donnees ? "0x0b4e2a91" : "0", donnees ? "Dernier octet reçu du serveur (+1)." : "Pas encore de réponse à accuser.", donnees),
            new("Longueur d'en-tête", 12, 1, "20 octets", "Pas d'options TCP dans cet exemple."),
            new("Drapeaux", 13, 1, donnees ? "PSH, ACK" : "SYN",
                donnees ? $"Premier segment de données, après la poignée de main SYN / SYN-ACK / ACK ({protocole})." : "Demande d'ouverture de connexion : c'est ce paquet que le pare-feu autorise ou jette."),
            new("Fenêtre", 14, 2, "64240", "Octets que la source peut encore recevoir."),
            new("Somme de contrôle", 16, 2, $"0x{somme:x4}", "Calculée sur un pseudo-en-tête IP + le segment : elle protège aussi les adresses."),
            new("Pointeur urgent", 18, 2, "0", "Inutilisé."),
        ]);
    }

    private static Segment SegmentUdp(Ipv4 source, Ipv4 destination, int port, byte[] charge)
    {
        var s = new byte[8 + charge.Length];
        BinaryPrimitives.WriteUInt16BigEndian(s.AsSpan(0), PortSourceExemple);
        BinaryPrimitives.WriteUInt16BigEndian(s.AsSpan(2), (ushort)port);
        BinaryPrimitives.WriteUInt16BigEndian(s.AsSpan(4), (ushort)s.Length);
        charge.CopyTo(s, 8);
        var somme = SommeControle(PseudoEnTete(source, destination, 17, s.Length).Concat(s).ToArray());
        if (somme == 0) somme = 0xffff;
        BinaryPrimitives.WriteUInt16BigEndian(s.AsSpan(6), somme);
        return new Segment("UDP", "udp", s, 8,
        [
            new("Port source", 0, 2, PortSourceExemple.ToString(), "Port éphémère de la source.", true),
            new("Port destination", 2, 2, $"{port}{NomPort(port)}", "Le service visé."),
            new("Longueur", 4, 2, $"{s.Length} octets", "En-tête UDP (8 octets) + données."),
            new("Somme de contrôle", 6, 2, $"0x{somme:x4}", "Calculée sur un pseudo-en-tête IP + le datagramme."),
        ]);
    }

    private static Segment Icmp(byte[] charge)
    {
        var donnees = Encoding.ASCII.GetBytes("p6-ping-abcdefgh");
        var s = new byte[8 + donnees.Length];
        s[0] = 8;
        BinaryPrimitives.WriteUInt16BigEndian(s.AsSpan(4), 0x0001);
        BinaryPrimitives.WriteUInt16BigEndian(s.AsSpan(6), 0x0001);
        donnees.CopyTo(s, 8);
        var somme = SommeControle(s);
        BinaryPrimitives.WriteUInt16BigEndian(s.AsSpan(2), somme);
        _ = charge;
        return new Segment("ICMP", "icmp", s, s.Length,
        [
            new("Type", 0, 1, "8 (demande d'écho)", "C'est un ping ; la réponse sera de type 0."),
            new("Code", 1, 1, "0", "Pas de précision pour un écho."),
            new("Somme de contrôle", 2, 2, $"0x{somme:x4}", "Calculée sur tout le message ICMP."),
            new("Identifiant", 4, 2, "1", "Relie les réponses à la commande ping qui les attend.", true),
            new("Numéro de séquence", 6, 2, "1", "Premier ping de la série.", true),
            new("Données", 8, donnees.Length, "p6-ping-abcdefgh", "Contenu quelconque, renvoyé tel quel par la cible.", true),
        ]);
    }

    // ---------------------------------------------------------------- application

    private static (byte[] Charge, CoucheTrame? Couche) ChargeApplicative(Protocole p, Ipv4 destination, int? port)
    {
        switch (p)
        {
            case Protocole.Dns:
            {
                var q = new List<byte> { 0x2b, 0x1d, 0x01, 0x00, 0, 1, 0, 0, 0, 0, 0, 0 };
                foreach (var etiquette in new[] { "www", "p6", "lab" }) { q.Add((byte)etiquette.Length); q.AddRange(Encoding.ASCII.GetBytes(etiquette)); }
                q.AddRange([0, 0, 1, 0, 1]);
                return ([.. q], new CoucheTrame("DNS (question)", "app", 0, q.Count,
                [
                    new("Identifiant", 0, 2, "0x2b1d", "Relie la réponse à la question.", true),
                    new("Drapeaux", 2, 2, "0x0100 (récursion demandée)", "bind9 ne fait pas de récursion : il répond seulement pour p6.lab."),
                    new("Nombre de questions", 4, 2, "1", "Une seule question."),
                    new("Autres compteurs", 6, 6, "0, 0, 0", "Pas de réponse ni d'autorité dans une question."),
                    new("Nom demandé", 12, q.Count - 16, "www.p6.lab", "Chaque morceau du nom est précédé de sa longueur."),
                    new("Type, classe", q.Count - 4, 4, "A, IN", "On demande l'adresse IPv4 (réponse attendue : 203.0.113.1)."),
                ]));
            }
            case Protocole.Http:
            {
                var texte = $"GET / HTTP/1.1\r\nHost: {destination}\r\nUser-Agent: curl/8.14.1\r\nAccept: */*\r\n\r\n";
                var o = Encoding.ASCII.GetBytes(texte);
                return (o, new CoucheTrame("HTTP (requête)", "app", 0, o.Length,
                [
                    new("Ligne de requête", 0, 16, "GET / HTTP/1.1", "Méthode, chemin et version : le texte circule en clair."),
                    new("En-têtes", 16, o.Length - 20, $"Host: {destination} …", "Host dit quel site est demandé ; nginx le lit pour choisir le serveur."),
                    new("Fin des en-têtes", o.Length - 4, 4, "\\r\\n\\r\\n", "Une ligne vide termine la requête."),
                ]));
            }
            case Protocole.Https:
            {
                var o = ClientHello("www.p6.lab");
                return (o, new CoucheTrame("TLS (ClientHello)", "app", 0, o.Length,
                [
                    new("Type d'enregistrement", 0, 1, "22 (handshake)", "Début de la négociation TLS : rien n'est encore chiffré."),
                    new("Version, longueur", 1, 4, $"0x0301, {o.Length - 5} octets", "Valeur historique, la vraie version est négociée plus loin."),
                    new("Message", 5, 4, $"ClientHello, {o.Length - 9} octets", "Le client propose ses algorithmes."),
                    new("Version client", 9, 2, "0x0303 (TLS 1.2, TLS 1.3 en extension)", "Version annoncée pour la compatibilité."),
                    new("Aléa", 11, 32, "32 octets", "Nombre aléatoire du client.", true),
                    new("Suites proposées", 43, 5, "TLS_AES_128_GCM_SHA256", "L'algorithme de chiffrement proposé."),
                    new("Compression, extension SNI", 48, o.Length - 48, "aucune ; www.p6.lab", "Extension en clair : c'est ce que voit un IDS avant le chiffrement."),
                ]));
            }
            case Protocole.Ntp:
            {
                var o = new byte[48];
                o[0] = 0x23;
                return (o, new CoucheTrame("NTP (requête client)", "app", 0, 48,
                [
                    new("LI, version, mode", 0, 1, "0, 4, 3 (client)", "Une machine demande l'heure au collecteur (chrony, local stratum 8)."),
                    new("Strate, intervalle, précision", 1, 3, "0", "Remplis par le serveur dans sa réponse."),
                    new("Horodatages", 4, 44, "zéros", "Le serveur renvoie ses horodatages ; le client en déduit l'écart et le délai."),
                ]));
            }
            case Protocole.Syslog:
            {
                var texte = "<134>1 2026-11-20T09:12:01+01:00 fw-p6 filterlog - - - 77,,,fae5,em0,match,block,in,4,0x0,,64,0,0,DF,6,tcp,60,203.0.113.66,203.0.113.1,51514,22,0,S";
                var o = Encoding.ASCII.GetBytes(texte);
                var debutMessage = texte.IndexOf("77,", StringComparison.Ordinal);
                return (o, new CoucheTrame("Syslog (RFC 5424)", "app", 0, o.Length,
                [
                    new("Priorité", 0, 5, "<134> local0.info", "Catégorie (16 × 8) + gravité (6)."),
                    new("En-tête", 5, debutMessage - 5, "version, date, machine, programme", "La date vient de l'horloge réglée par NTP : sans elle, impossible de corréler (C11)."),
                    new("Message filterlog", debutMessage, o.Length - debutMessage, "…,match,block,in,…", "Une ligne par paquet journalisé : règle, action, sens, adresses et ports.", true),
                ]));
            }
            default:
                _ = port;
                return ([], null);
        }
    }

    private static byte[] ClientHello(string nom)
    {
        var sni = new List<byte>();
        var n = Encoding.ASCII.GetBytes(nom);
        sni.AddRange([0x00, 0x00]);                               // extension server_name
        sni.AddRange(U16(n.Length + 5));
        sni.AddRange(U16(n.Length + 3));
        sni.Add(0x00);
        sni.AddRange(U16(n.Length));
        sni.AddRange(n);
        var corps = new List<byte>();
        corps.AddRange([0x03, 0x03]);
        for (var i = 0; i < 32; i++) corps.Add((byte)(0x50 + i));   // aléa d'exemple
        corps.Add(0x00);                                           // pas d'identifiant de session
        corps.AddRange([0x00, 0x02, 0x13, 0x01]);                  // une suite : TLS_AES_128_GCM_SHA256
        corps.AddRange([0x01, 0x00]);                              // compression : aucune
        corps.AddRange(U16(sni.Count));
        corps.AddRange(sni);
        var hs = new List<byte> { 0x01, 0, (byte)(corps.Count >> 8), (byte)corps.Count };
        hs.AddRange(corps);
        var rec = new List<byte> { 0x16, 0x03, 0x01 };
        rec.AddRange(U16(hs.Count));
        rec.AddRange(hs);
        return [.. rec];
    }

    // ---------------------------------------------------------------- outils

    /// <summary>Somme de contrôle Internet (RFC 1071) : complément à un de la somme des mots de 16 bits.</summary>
    public static ushort SommeControle(ReadOnlySpan<byte> octets)
    {
        uint somme = 0;
        for (var i = 0; i + 1 < octets.Length; i += 2) somme += (uint)(octets[i] << 8 | octets[i + 1]);
        if (octets.Length % 2 == 1) somme += (uint)(octets[^1] << 8);
        while (somme >> 16 != 0) somme = (somme & 0xffff) + (somme >> 16);
        return (ushort)~somme;
    }

    public static byte[] PseudoEnTete(Ipv4 source, Ipv4 destination, byte protocole, int longueur)
    {
        var p = new byte[12];
        BinaryPrimitives.WriteUInt32BigEndian(p.AsSpan(0), source.Valeur);
        BinaryPrimitives.WriteUInt32BigEndian(p.AsSpan(4), destination.Valeur);
        p[9] = protocole;
        BinaryPrimitives.WriteUInt16BigEndian(p.AsSpan(10), (ushort)longueur);
        return p;
    }

    private static byte[] U16(int v) => [(byte)(v >> 8), (byte)v];

    private static string MacTexte(byte[] m) => string.Join(":", m.Select(b => b.ToString("x2")));

    private static string NomPort(int port) => port switch
    {
        22 => " (SSH)", 53 => " (DNS)", 80 => " (HTTP)", 123 => " (NTP)", 443 => " (HTTPS)", 445 => " (SMB)", 514 => " (syslog)",
        3389 => " (RDP)", 5000 => " (API Flask)", 5201 => " (iperf3)", 5432 => " (PostgreSQL)", _ => ""
    };
}
