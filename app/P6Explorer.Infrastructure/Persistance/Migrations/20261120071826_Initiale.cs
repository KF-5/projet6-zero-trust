using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace P6Explorer.Infrastructure.Persistance.Migrations
{
    /// <inheritdoc />
    public partial class Initiale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NumeroRole = table.Column<int>(type: "int", nullable: true),
                    NomAffiche = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EtatsReseau",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EtatsReseau", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "Glossaire",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Terme = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Definition = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    Comparaison = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Glossaire", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Liens",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    De = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Vers = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Zone = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Branche = table.Column<bool>(type: "bit", nullable: false),
                    EtatReseauCode = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    Points = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Liens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Noeuds",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Nom = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    SousTitre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Type = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Zone = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Passerelle = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    Hote = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    MembresSeulement = table.Column<bool>(type: "bit", nullable: false),
                    X = table.Column<double>(type: "float", nullable: false),
                    Y = table.Column<double>(type: "float", nullable: false),
                    Largeur = table.Column<double>(type: "float", nullable: false),
                    Hauteur = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Noeuds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles6",
                columns: table => new
                {
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Cle = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Titre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CourtNom = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Couleur = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Mission = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Introduction = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Demonstration = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles6", x => x.Numero);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Revisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Lot = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Entite = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    EntiteId = table.Column<int>(type: "int", nullable: false),
                    EtapeId = table.Column<int>(type: "int", nullable: true),
                    Champ = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Avant = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Apres = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuteurId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Revisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Revisions_AspNetUsers_AuteurId",
                        column: x => x.AuteurId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Regles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EtatReseauCode = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    Equipement = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Interface = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Ordre = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false),
                    Proto = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Destination = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Ports = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Journal = table.Column<bool>(type: "bit", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Regles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Regles_EtatsReseau_EtatReseauCode",
                        column: x => x.EtatReseauCode,
                        principalTable: "EtatsReseau",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReglesNat",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EtatReseauCode = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Interface = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Proto = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    AdressePublique = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: false),
                    PortPublic = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Cible = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: false),
                    PortCible = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceAutorisee = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReglesNat", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReglesNat_EtatsReseau_EtatReseauCode",
                        column: x => x.EtatReseauCode,
                        principalTable: "EtatsReseau",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Reseaux",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nom = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Zone = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Vlan = table.Column<int>(type: "int", nullable: true),
                    Prefixe_Adresse = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    Prefixe_Longueur = table.Column<int>(type: "int", nullable: true),
                    Passerelle = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    EtatReseauCode = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    CadreX = table.Column<double>(type: "float", nullable: true),
                    CadreY = table.Column<double>(type: "float", nullable: true),
                    CadreLargeur = table.Column<double>(type: "float", nullable: true),
                    CadreHauteur = table.Column<double>(type: "float", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reseaux", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reseaux_EtatsReseau_EtatReseauCode",
                        column: x => x.EtatReseauCode,
                        principalTable: "EtatsReseau",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Simulations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UtilisateurId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Titre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Destination = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Protocole = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Port = table.Column<int>(type: "int", nullable: true),
                    EtatReseauCode = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    Verdict = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Regle = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Chemin = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PourDemo = table.Column<bool>(type: "bit", nullable: false),
                    OrdreDemo = table.Column<int>(type: "int", nullable: true),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Simulations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Simulations_AspNetUsers_UtilisateurId",
                        column: x => x.UtilisateurId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Simulations_EtatsReseau_EtatReseauCode",
                        column: x => x.EtatReseauCode,
                        principalTable: "EtatsReseau",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Etapes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleNumero = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Titre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quand = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Fait = table.Column<bool>(type: "bit", nullable: false),
                    EtatTexte = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Statut = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    MiseAJour = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    But = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Pourquoi = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Prerequis = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Livrable = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Schema = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Etapes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Etapes_Roles6_RoleNumero",
                        column: x => x.RoleNumero,
                        principalTable: "Roles6",
                        principalColumn: "Numero",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Interfaces",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NoeudId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Nom = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReseauCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ReseauId = table.Column<int>(type: "int", nullable: true),
                    Adresse = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    Prefixe = table.Column<int>(type: "int", nullable: true),
                    Vlan = table.Column<int>(type: "int", nullable: true),
                    Branchee = table.Column<bool>(type: "bit", nullable: false),
                    EtatReseauCode = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Interfaces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Interfaces_Noeuds_NoeudId",
                        column: x => x.NoeudId,
                        principalTable: "Noeuds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Interfaces_Reseaux_ReseauId",
                        column: x => x.ReseauId,
                        principalTable: "Reseaux",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Commandes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EtapeId = table.Column<int>(type: "int", nullable: false),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Ordre = table.Column<int>(type: "int", nullable: false),
                    Ou = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Shell = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Compte = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Bloc = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Texte = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TexteGuide = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Explication = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sortie = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstTest = table.Column<bool>(type: "bit", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Commandes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Commandes_Commandes_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Commandes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Commandes_Etapes_EtapeId",
                        column: x => x.EtapeId,
                        principalTable: "Etapes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Criteres",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EtapeId = table.Column<int>(type: "int", nullable: false),
                    Ordre = table.Column<int>(type: "int", nullable: false),
                    Texte = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Criteres", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Criteres_Etapes_EtapeId",
                        column: x => x.EtapeId,
                        principalTable: "Etapes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EtapeTerme",
                columns: table => new
                {
                    EtapesId = table.Column<int>(type: "int", nullable: false),
                    TermesId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EtapeTerme", x => new { x.EtapesId, x.TermesId });
                    table.ForeignKey(
                        name: "FK_EtapeTerme_Etapes_EtapesId",
                        column: x => x.EtapesId,
                        principalTable: "Etapes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EtapeTerme_Glossaire_TermesId",
                        column: x => x.TermesId,
                        principalTable: "Glossaire",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Pannes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EtapeId = table.Column<int>(type: "int", nullable: false),
                    Ordre = table.Column<int>(type: "int", nullable: false),
                    Symptome = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Cause = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Solution = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pannes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pannes_Etapes_EtapeId",
                        column: x => x.EtapeId,
                        principalTable: "Etapes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Preuves",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EtapeId = table.Column<int>(type: "int", nullable: false),
                    NomFichier = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NomOriginal = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    TypeMime = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Taille = table.Column<long>(type: "bigint", nullable: false),
                    Legende = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AuteurId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Preuves", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Preuves_AspNetUsers_AuteurId",
                        column: x => x.AuteurId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Preuves_Etapes_EtapeId",
                        column: x => x.EtapeId,
                        principalTable: "Etapes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Quiz",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EtapeId = table.Column<int>(type: "int", nullable: false),
                    Seuil = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quiz", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Quiz_Etapes_EtapeId",
                        column: x => x.EtapeId,
                        principalTable: "Etapes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Effets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CommandeId = table.Column<int>(type: "int", nullable: false),
                    Ordre = table.Column<int>(type: "int", nullable: false),
                    Automatique = table.Column<bool>(type: "bit", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Signature = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Blocage = table.Column<bool>(type: "bit", nullable: true),
                    Noeud = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Carte = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Ip = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    EtatReseau = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    ActionRegle = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: true),
                    IdRegle = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Resume = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReseauDocker = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Lien = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Branche = table.Column<bool>(type: "bit", nullable: true),
                    EtatVm = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: true),
                    CheminFichier = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Cree = table.Column<bool>(type: "bit", nullable: true),
                    Nom = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Restauration = table.Column<bool>(type: "bit", nullable: true),
                    De = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Ligne = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Vers = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Proto = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: true),
                    Port = table.Column<int>(type: "int", nullable: true),
                    AdresseVisee = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Chemin = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Nat_Regle = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Nat_AdresseAvant = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    Nat_PortAvant = table.Column<int>(type: "int", nullable: true),
                    Nat_AdresseApres = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    Nat_PortApres = table.Column<int>(type: "int", nullable: true),
                    Verdict = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: true),
                    Regle = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Vlan = table.Column<int>(type: "int", nullable: true),
                    Ok = table.Column<bool>(type: "bit", nullable: true),
                    Compteur = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Demarre = table.Column<bool>(type: "bit", nullable: true),
                    Texte = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Effets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Effets_Commandes_CommandeId",
                        column: x => x.CommandeId,
                        principalTable: "Commandes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Fils",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CommandeId = table.Column<int>(type: "int", nullable: false),
                    AuteurId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Titre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Statut = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fils", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Fils_AspNetUsers_AuteurId",
                        column: x => x.AuteurId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Fils_Commandes_CommandeId",
                        column: x => x.CommandeId,
                        principalTable: "Commandes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Progressions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UtilisateurId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CommandeId = table.Column<int>(type: "int", nullable: false),
                    Comprise = table.Column<bool>(type: "bit", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Progressions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Progressions_AspNetUsers_UtilisateurId",
                        column: x => x.UtilisateurId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Progressions_Commandes_CommandeId",
                        column: x => x.CommandeId,
                        principalTable: "Commandes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Questions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuizId = table.Column<int>(type: "int", nullable: false),
                    Ordre = table.Column<int>(type: "int", nullable: false),
                    Texte = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Statut = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Questions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Questions_Quiz_QuizId",
                        column: x => x.QuizId,
                        principalTable: "Quiz",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tentatives",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuizId = table.Column<int>(type: "int", nullable: false),
                    UtilisateurId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Score = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tentatives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tentatives_AspNetUsers_UtilisateurId",
                        column: x => x.UtilisateurId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Tentatives_Quiz_QuizId",
                        column: x => x.QuizId,
                        principalTable: "Quiz",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FilId = table.Column<int>(type: "int", nullable: false),
                    AuteurId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Texte = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Messages_AspNetUsers_AuteurId",
                        column: x => x.AuteurId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Messages_Fils_FilId",
                        column: x => x.FilId,
                        principalTable: "Fils",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Choix",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuestionId = table.Column<int>(type: "int", nullable: false),
                    Ordre = table.Column<int>(type: "int", nullable: false),
                    Texte = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Correct = table.Column<bool>(type: "bit", nullable: false),
                    Explication = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Choix", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Choix_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Reponses",
                columns: table => new
                {
                    TentativeId = table.Column<int>(type: "int", nullable: false),
                    ChoixId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reponses", x => new { x.TentativeId, x.ChoixId });
                    table.ForeignKey(
                        name: "FK_Reponses_Choix_ChoixId",
                        column: x => x.ChoixId,
                        principalTable: "Choix",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reponses_Tentatives_TentativeId",
                        column: x => x.TentativeId,
                        principalTable: "Tentatives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Choix_QuestionId",
                table: "Choix",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_Commandes_Code",
                table: "Commandes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Commandes_EtapeId_Ordre",
                table: "Commandes",
                columns: new[] { "EtapeId", "Ordre" });

            migrationBuilder.CreateIndex(
                name: "IX_Commandes_ParentId",
                table: "Commandes",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Criteres_EtapeId",
                table: "Criteres",
                column: "EtapeId");

            migrationBuilder.CreateIndex(
                name: "IX_Effets_CommandeId_Ordre",
                table: "Effets",
                columns: new[] { "CommandeId", "Ordre" });

            migrationBuilder.CreateIndex(
                name: "IX_Etapes_Code",
                table: "Etapes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Etapes_RoleNumero_Numero",
                table: "Etapes",
                columns: new[] { "RoleNumero", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EtapeTerme_TermesId",
                table: "EtapeTerme",
                column: "TermesId");

            migrationBuilder.CreateIndex(
                name: "IX_Fils_AuteurId",
                table: "Fils",
                column: "AuteurId");

            migrationBuilder.CreateIndex(
                name: "IX_Fils_CommandeId_Statut",
                table: "Fils",
                columns: new[] { "CommandeId", "Statut" });

            migrationBuilder.CreateIndex(
                name: "IX_Glossaire_Terme",
                table: "Glossaire",
                column: "Terme",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Interfaces_NoeudId_Nom_EtatReseauCode",
                table: "Interfaces",
                columns: new[] { "NoeudId", "Nom", "EtatReseauCode" });

            migrationBuilder.CreateIndex(
                name: "IX_Interfaces_ReseauId",
                table: "Interfaces",
                column: "ReseauId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_AuteurId",
                table: "Messages",
                column: "AuteurId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_FilId",
                table: "Messages",
                column: "FilId");

            migrationBuilder.CreateIndex(
                name: "IX_Pannes_EtapeId",
                table: "Pannes",
                column: "EtapeId");

            migrationBuilder.CreateIndex(
                name: "IX_Preuves_AuteurId",
                table: "Preuves",
                column: "AuteurId");

            migrationBuilder.CreateIndex(
                name: "IX_Preuves_EtapeId",
                table: "Preuves",
                column: "EtapeId");

            migrationBuilder.CreateIndex(
                name: "IX_Preuves_NomFichier",
                table: "Preuves",
                column: "NomFichier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Progressions_CommandeId",
                table: "Progressions",
                column: "CommandeId");

            migrationBuilder.CreateIndex(
                name: "IX_Progressions_UtilisateurId_CommandeId",
                table: "Progressions",
                columns: new[] { "UtilisateurId", "CommandeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Questions_QuizId",
                table: "Questions",
                column: "QuizId");

            migrationBuilder.CreateIndex(
                name: "IX_Quiz_EtapeId",
                table: "Quiz",
                column: "EtapeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Regles_EtatReseauCode_Equipement_Interface_Ordre",
                table: "Regles",
                columns: new[] { "EtatReseauCode", "Equipement", "Interface", "Ordre" });

            migrationBuilder.CreateIndex(
                name: "IX_ReglesNat_EtatReseauCode",
                table: "ReglesNat",
                column: "EtatReseauCode");

            migrationBuilder.CreateIndex(
                name: "IX_Reponses_ChoixId",
                table: "Reponses",
                column: "ChoixId");

            migrationBuilder.CreateIndex(
                name: "IX_Reseaux_Code",
                table: "Reseaux",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reseaux_EtatReseauCode",
                table: "Reseaux",
                column: "EtatReseauCode");

            migrationBuilder.CreateIndex(
                name: "IX_Revisions_AuteurId",
                table: "Revisions",
                column: "AuteurId");

            migrationBuilder.CreateIndex(
                name: "IX_Revisions_Entite_EntiteId",
                table: "Revisions",
                columns: new[] { "Entite", "EntiteId" });

            migrationBuilder.CreateIndex(
                name: "IX_Revisions_EtapeId_Date",
                table: "Revisions",
                columns: new[] { "EtapeId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Roles6_Cle",
                table: "Roles6",
                column: "Cle",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Simulations_EtatReseauCode",
                table: "Simulations",
                column: "EtatReseauCode");

            migrationBuilder.CreateIndex(
                name: "IX_Simulations_PourDemo_OrdreDemo",
                table: "Simulations",
                columns: new[] { "PourDemo", "OrdreDemo" });

            migrationBuilder.CreateIndex(
                name: "IX_Simulations_UtilisateurId",
                table: "Simulations",
                column: "UtilisateurId");

            migrationBuilder.CreateIndex(
                name: "IX_Tentatives_QuizId",
                table: "Tentatives",
                column: "QuizId");

            migrationBuilder.CreateIndex(
                name: "IX_Tentatives_UtilisateurId_QuizId",
                table: "Tentatives",
                columns: new[] { "UtilisateurId", "QuizId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "Criteres");

            migrationBuilder.DropTable(
                name: "Effets");

            migrationBuilder.DropTable(
                name: "EtapeTerme");

            migrationBuilder.DropTable(
                name: "Interfaces");

            migrationBuilder.DropTable(
                name: "Liens");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "Pannes");

            migrationBuilder.DropTable(
                name: "Preuves");

            migrationBuilder.DropTable(
                name: "Progressions");

            migrationBuilder.DropTable(
                name: "Regles");

            migrationBuilder.DropTable(
                name: "ReglesNat");

            migrationBuilder.DropTable(
                name: "Reponses");

            migrationBuilder.DropTable(
                name: "Revisions");

            migrationBuilder.DropTable(
                name: "Simulations");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "Glossaire");

            migrationBuilder.DropTable(
                name: "Noeuds");

            migrationBuilder.DropTable(
                name: "Reseaux");

            migrationBuilder.DropTable(
                name: "Fils");

            migrationBuilder.DropTable(
                name: "Choix");

            migrationBuilder.DropTable(
                name: "Tentatives");

            migrationBuilder.DropTable(
                name: "EtatsReseau");

            migrationBuilder.DropTable(
                name: "Commandes");

            migrationBuilder.DropTable(
                name: "Questions");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Quiz");

            migrationBuilder.DropTable(
                name: "Etapes");

            migrationBuilder.DropTable(
                name: "Roles6");
        }
    }
}
