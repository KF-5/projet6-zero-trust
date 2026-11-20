using P6Explorer.Domain.Enumerations;
using P6Explorer.Domain.Moteur;
using P6Explorer.Domain.Valeurs;
using P6Explorer.Tests.Outils;

namespace P6Explorer.Tests.MoteurReseau;

/// <summary>Cas nommés du critère d'acceptation, et comportement détaillé des trois moteurs.</summary>
public class MoteurTests
{
    // ---------- Critère d'acceptation ----------

    [Fact]
    public void Client_ext_vers_postgres_est_bloque_en_A_par_FA5()
    {
        var r = Labo.Simuler("A", "client-ext", "10.10.0.70", Protocole.PostgreSql);
        Assert.Equal(Verdict.Bloque, r.Verdict);
        Assert.Equal("FA5", r.Regle);
        Assert.Equal(NoeudId.Opnsense, r.Equipement);
        Assert.True(r.Filterlog);
        Assert.Null(r.Nat);
    }

    public static TheoryData<string, string, Protocole, int?> CiblesInterditesAKali()
    {
        var d = new TheoryData<string, string, Protocole, int?>();
        foreach (var etat in new[] { "A", "B" })
        {
            // MGMT : tout ce qui permet d'administrer (SSH, console web, Windows, panneau, VNC).
            foreach (var (ip, port) in new (string, int?)[]
                     {
                         ("10.99.0.1", 443), ("10.99.0.1", 22), ("10.99.0.1", null), ("10.99.0.20", 22), ("10.99.0.10", 22),
                         ("10.99.0.2", 22), ("10.99.0.2", 445), ("10.99.0.2", 3389), ("10.99.0.2", 8080), ("10.99.0.2", 5901),
                         ("10.99.0.2", null)
                     })
                d.Add(etat, ip, port is null ? Protocole.Icmp : Protocole.Tcp, port);
            // La box, Internet.
            foreach (var ip in new[] { "192.168.1.1", "8.8.8.8", "151.101.2.132" })
            {
                d.Add(etat, ip, Protocole.Icmp, null);
                d.Add(etat, ip, Protocole.Https, 443);
                d.Add(etat, ip, Protocole.Dns, 53);
            }
        }
        return d;
    }

    [Theory]
    [MemberData(nameof(CiblesInterditesAKali))]
    public void Kali_ne_joint_jamais_MGMT_la_box_Internet_ni_Windows(string etat, string ip, Protocole protocole, int? port)
    {
        var r = Labo.Simuler(etat, "kali", ip, protocole, port);
        Assert.False(r.FranchitLesFiltres, $"État {etat}, Kali → {ip} {protocole}/{port} : {Labo.Decrire(r)}");
    }

    [Theory]
    [InlineData("A")]
    [InlineData("B")]
    public void Q16_Kali_joint_encore_NTP_et_syslog_du_collecteur_et_le_ping_des_VM(string etat)
    {
        // Constat (docs/questions.md, Q16) : p6_ssh ne filtre que le port 22 (E35) ; chrony accepte 10.99.0.0/24.
        Assert.Equal(Verdict.Passe, Labo.Simuler(etat, "kali", "10.99.0.10", Protocole.Ntp).Verdict);
        Assert.Equal(Verdict.Passe, Labo.Simuler(etat, "kali", "10.99.0.10", Protocole.Syslog).Verdict);
        Assert.Equal(Verdict.Passe, Labo.Simuler(etat, "kali", "10.99.0.20", Protocole.Icmp).Verdict);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("B")]
    public void Q16_avec_restreindre_syslog_Kali_ne_peut_plus_ecrire_au_collecteur(string etat)
    {
        var durci = new OptionsSimulation { ReglesActivees = new HashSet<string> { "p6_syslog" } };
        var kali = Labo.Simuler(etat, "kali", "10.99.0.10", Protocole.Syslog, options: durci);
        Assert.Equal((Verdict.Bloque, "p6_syslog"), (kali.Verdict, kali.Regle));
        Assert.Equal(Verdict.Passe, Labo.Simuler(etat, "opnsense", "10.99.0.10", Protocole.Syslog, options: durci).Verdict);
        Assert.Equal(Verdict.Passe, Labo.Simuler(etat, "kali", "10.99.0.10", Protocole.Ntp, options: durci).Verdict);   // l'heure reste servie
    }

    [Fact]
    public void S05_en_A_le_mouvement_lateral_aboutit_sans_aucune_ligne_filterlog()
    {
        var r = Labo.Simuler("A", "client-03", "10.10.0.70", Protocole.PostgreSql);
        Assert.Equal(Verdict.Passe, r.Verdict);
        Assert.False(r.Filterlog);
        Assert.DoesNotContain(r.Chemin, s => s.Noeud == NoeudId.Opnsense);
        Assert.Equal([NoeudId.Client03, NoeudId.Postgres], r.Chemin.Select(s => s.Noeud));
    }

    [Fact]
    public void S05_en_B_le_mouvement_lateral_est_jete_et_journalise_par_F99()
    {
        var r = Labo.Simuler("B", "client-03", "10.10.0.70", Protocole.PostgreSql);
        Assert.Equal(Verdict.Bloque, r.Verdict);
        Assert.Equal("F99", r.Regle);
        Assert.True(r.Filterlog);
        Assert.Contains(r.Chemin, s => s.Noeud == NoeudId.Opnsense && s.Entree == "vlan0.11");
    }

    [Fact]
    public void S04_en_B_la_dmz_ne_joint_plus_la_base()
    {
        var a = Labo.Simuler("A", "dmz-pivot", "10.10.0.70", Protocole.PostgreSql);
        var b = Labo.Simuler("B", "dmz-pivot", "10.10.0.70", Protocole.PostgreSql);
        Assert.Equal((Verdict.Passe, "FA3"), (a.Verdict, a.Regle));
        Assert.Equal((Verdict.Bloque, "F99"), (b.Verdict, b.Regle));
    }

    // ---------- NAT ----------

    [Fact]
    public void FA1_traduit_203_0_113_1_vers_nginx_avant_le_filtrage()
    {
        var r = Labo.Simuler("A", "kali", "203.0.113.1", Protocole.Https);
        Assert.Equal(Verdict.Passe, r.Verdict);
        Assert.NotNull(r.Nat);
        Assert.Equal(("FA1", "203.0.113.1", 443, "10.20.0.10", 443), (r.Nat!.Regle, r.Nat.AdresseAvant, r.Nat.PortAvant, r.Nat.AdresseApres, r.Nat.PortApres));
        Assert.Equal(NoeudId.Nginx, r.Destination);
        Assert.Equal([NoeudId.Kali, NoeudId.Opnsense, NoeudId.Nginx], r.Chemin.Select(s => s.Noeud));
        var opn = r.Chemin[1];
        Assert.Equal(("em0", "vlan02", 20), (opn.Entree, opn.Sortie, opn.Vlan));
    }

    [Fact]
    public void FA6_ne_passe_que_pendant_les_mesures_et_seulement_depuis_client_ext()
    {
        var mesure = new OptionsSimulation { ReglesActivees = new HashSet<string> { "FA6" } };
        Assert.Equal(Verdict.Bloque, Labo.Simuler("A", "client-ext", "203.0.113.1", Protocole.Iperf3).Verdict);
        var r = Labo.Simuler("A", "client-ext", "203.0.113.1", Protocole.Iperf3, options: mesure);
        Assert.Equal((Verdict.Passe, "FA6"), (r.Verdict, r.Regle));
        Assert.Equal(Verdict.Bloque, Labo.Simuler("A", "kali", "203.0.113.1", Protocole.Iperf3, options: mesure).Verdict);
    }

    [Fact]
    public void Redirection_register_rule_ouvre_aussi_l_acces_direct_E36()
    {
        // Ce que faisait « Register rule » avant le passage en « Pass » : Kali joignait nginx sans traduction.
        var a = TopologieAvec(nat => nat.Filtrage = nat.Code == "FA1" ? FiltrageNat.Associee : nat.Filtrage);
        var r = a.Simuler(new DemandeSimulation(NoeudId.Kali, "10.20.0.10", Protocole.Http));
        Assert.Equal((Verdict.Passe, "FA1"), (r.Verdict, r.Regle));
        Assert.Null(r.Nat);
    }

    [Fact]
    public void Q15_en_B_sans_F01_le_site_reste_joignable_par_la_redirection_et_l_acces_direct_est_jete()
    {
        // Recommandation Q15 / E43 appliquée : F01 et F02 ne sont pas saisies, les redirections « Pass » suffisent.
        var direct = Labo.Simuler("B", "kali", "10.20.0.10", Protocole.Http);
        Assert.Equal((Verdict.Bloque, "F99"), (direct.Verdict, direct.Regle));
        var publie = Labo.Simuler("B", "kali", "203.0.113.1", Protocole.Https);
        Assert.Equal((Verdict.Passe, "F01"), (publie.Verdict, publie.Regle));
        // Avec F01 saisie comme dans regles-B.md, le trou E36 reviendrait.
        var avecF01 = Labo.Simuler("B", "kali", "10.20.0.10", Protocole.Http, options: new OptionsSimulation { ReglesActivees = new HashSet<string> { "F01" } });
        Assert.Equal((Verdict.Passe, "F01"), (avecF01.Verdict, avecF01.Regle));
    }

    // ---------- Routage ----------

    [Fact]
    public void En_B_chaque_conteneur_prend_la_passerelle_de_son_segment()
    {
        var r = Labo.Simuler("B", "bind9", "10.10.0.70", Protocole.PostgreSql);
        Assert.Contains("vers sa passerelle 10.20.0.33", r.Chemin[0].Texte);
        Assert.Equal(("vlan0.22", 22), (r.Chemin[1].Entree, r.Chemin[0].Vlan));
    }

    [Fact]
    public void Q10_un_poste_du_LAN_ne_peut_pas_administrer_une_VM_MGMT()
    {
        // FA4 laisse passer vers MGMT ; p6_ssh jette le SSH ; ailleurs, la VM n'a pas de route de retour vers le LAN.
        var ssh = Labo.Simuler("A", "client-03", "10.99.0.20", Protocole.Ssh);
        Assert.Equal((Verdict.Bloque, "p6_ssh"), (ssh.Verdict, ssh.Regle));
        var ntp = Labo.Simuler("A", "client-03", "10.99.0.10", Protocole.Ntp);
        Assert.Equal(Verdict.Injoignable, ntp.Verdict);
        Assert.Contains("route de retour", ntp.Explication);
    }

    [Fact]
    public void Windows_n_a_pas_de_route_vers_le_LAN()
        => Assert.Equal(Verdict.Injoignable, Labo.Simuler("A", "hote", "10.10.0.70", Protocole.PostgreSql).Verdict);

    [Fact]
    public void Un_service_qui_n_ecoute_que_sur_127_0_0_1_n_est_pas_joignable_par_le_reseau()
    {
        Assert.Equal(Verdict.Passe, Labo.Simuler("A", "hote", "127.0.0.1", Protocole.Tcp, 8080).Verdict);
        Assert.Equal(Verdict.Refuse, Labo.Simuler("A", "hote", "10.99.0.2", Protocole.Tcp, 8080).Verdict);
    }

    // ---------- Services ----------

    [Fact]
    public void SSH_d_OPNsense_est_desactive_sauf_si_on_le_reactive_Q13()
    {
        var arrete = Labo.Simuler("A", "collecteur", "10.99.0.1", Protocole.Ssh);
        Assert.Equal((Verdict.Refuse, "Admin du pare-feu"), (arrete.Verdict, arrete.Regle));
        Assert.Contains("SSH désactivé", arrete.Explication);
        var actif = Labo.Simuler("A", "collecteur", "10.99.0.1", Protocole.Ssh, options: new OptionsSimulation { SshOpnsenseActif = true });
        Assert.Equal(Verdict.Passe, actif.Verdict);
    }

    [Fact]
    public void Le_SSH_de_Kali_n_ecoute_que_sur_MGMT()
    {
        Assert.Equal(Verdict.Passe, Labo.Simuler("A", "collecteur", "10.99.0.66", Protocole.Ssh).Verdict);
        Assert.Equal(Verdict.Passe, Labo.Simuler("A", "hote", "10.99.0.66", Protocole.Ssh).Verdict);
        var wan = Labo.Simuler("A", "client-ext", "203.0.113.66", Protocole.Ssh);                    // même segment WAN, sans pare-feu
        Assert.Equal((Verdict.Bloque, "p6_ssh"), (wan.Verdict, wan.Regle));
    }

    [Fact]
    public void Le_collecteur_recoit_syslog_d_OPNsense()
    {
        var r = Labo.Simuler("A", "opnsense", "10.99.0.10", Protocole.Syslog);
        Assert.Equal(Verdict.Passe, r.Verdict);
        Assert.Equal([NoeudId.Opnsense, NoeudId.Collecteur], r.Chemin.Select(s => s.Noeud));
    }

    [Fact]
    public void Adresse_absente_du_segment_injoignable()
    {
        var r = Labo.Simuler("A", "client-01", "10.10.0.99", Protocole.Icmp);
        Assert.Equal(Verdict.Injoignable, r.Verdict);
        Assert.Contains("ARP", r.Explication);
    }

    private static SimulateurPaquet TopologieAvec(Action<Domain.Topologie.RegleNat> changer)
    {
        var a = Depot.LireTopologie("A");
        a.ReglesNat.ForEach(changer);
        return new SimulateurPaquet(TopologieSimulee.Depuis("A", [a, Depot.LireTopologie("B")]));
    }
}

/// <summary>Lecture des adresses et des champs de règles.</summary>
public class AdressesTests
{
    [Theory]
    [InlineData("10.10.0.0/26", "10.10.0.63", true)]
    [InlineData("10.10.0.0/26", "10.10.0.64", false)]
    [InlineData("10.10.0.64/27", "10.10.0.70", true)]
    [InlineData("10.10.0.96/27", "10.10.0.100", true)]
    [InlineData("0.0.0.0/0", "8.8.8.8", true)]
    [InlineData("10.99.0.2", "10.99.0.2", true)]
    [InlineData("10.99.0.2", "10.99.0.20", false)]
    public void Prefixe_contient(string prefixe, string ip, bool attendu)
    {
        Assert.True(Prefixe4.TryLire(prefixe, out var p));
        Assert.Equal(attendu, p.Contient(Ipv4.Lire(ip)));
    }

    [Theory]
    [InlineData("10.10.0")]
    [InlineData("10.10.0.256")]
    [InlineData("abc")]
    [InlineData("")]
    public void Adresse_invalide(string texte) => Assert.False(Ipv4.TryLire(texte, out _));

    [Fact]
    public void Champs_de_regle()
    {
        Assert.True(Champs.AdresseCorrespond("10.99.0.2;10.99.0.10", Ipv4.Lire("10.99.0.10"), _ => false));
        Assert.False(Champs.AdresseCorrespond("10.99.0.2;10.99.0.10", Ipv4.Lire("10.99.0.66"), _ => false));
        Assert.True(Champs.AdresseCorrespond("self", Ipv4.Lire("10.99.0.1"), ip => ip == Ipv4.Lire("10.99.0.1")));
        Assert.True(Champs.PortCorrespond("80;443", 443));
        Assert.True(Champs.PortCorrespond("5901-5904", 5903));
        Assert.False(Champs.PortCorrespond("22", null));
        Assert.True(Champs.PortCorrespond("any", null));
        Assert.True(Champs.ProtoCorrespond("tcp/udp", "udp"));
        Assert.False(Champs.ProtoCorrespond("tcp", "icmp"));
        Assert.Equal("203.0.113.66", Ipv4.Lire("203.0.113.66").ToString());
    }
}
