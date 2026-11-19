# P6 Explorer — à lancer avec Claude Code

Ce dépôt contient tout ce qu'il faut pour que Claude Code construise **P6 Explorer**, une plateforme ASP.NET Core MVC (.NET 8) qui rejoue graphiquement chaque commande de chaque rôle du projet.

## 1. Ce qu'il faut installer avant

| Outil | Pourquoi |
|---|---|
| **Claude Code** | Il écrit l'application |
| **Visual Studio 2022** avec la charge « Développement web et ASP.NET » | Ouvrir, compiler et lancer la solution |
| **SDK .NET 8** | Normalement installé avec Visual Studio |
| **SQL Server LocalDB** | La base de données (installée avec Visual Studio) |
| **Node.js** (version 18 ou plus) | Convertir les fichiers du guide en JSON pendant l'import |
| **Git** | Le dossier est un dépôt Git |

## 2. Lancer Claude Code

1. Clone le dépôt : `git clone https://github.com/KF-5/projet6-zero-trust.git` puis `cd projet6-zero-trust`.
2. Ouvre un terminal **dans le dossier du dépôt** (à la racine, là où se trouvent `README.md` et `ECARTS.md`).
3. Lance `claude`.
4. Écris-lui :

   > Lis `sources/PROMPT-claude-code-plateforme-P6.md` en entier et suis-le.

Le prompt se trouve dans `sources/`, avec ses sources :
- `sources/etapes-par-role-P6.md` : les 47 étapes des 6 rôles, détaillées (source principale) ;
- `sources/guide/` : le guide de réalisation d'origine (données, schémas, couleurs).

## 3. Comment ça se déroule

- Claude Code travaille **par jalons** (7 au total) et **s'arrête après chacun** pour que tu valides. Le premier jalon est une étude : il rend `docs/style-reference.md` et `docs/plan-plateforme.md`, sans écrire de code. Lis-les avant de dire « continue ».
- Ouvre **une nouvelle conversation Claude Code par jalon**, en commençant par : « Relis `sources/PROMPT-claude-code-plateforme-P6.md` et `docs/plan-plateforme.md`, puis fais le jalon N ». Une conversation trop longue lui fait perdre le fil.
- Après chaque jalon, ouvre `app/P6Explorer.sln` dans Visual Studio, lance avec F5 et vérifie que ça marche.
- Il travaille sur la branche `role-1-plateforme-graphique`, avec des commits au format `[rôle 1] …`.
- Quand il pose une question dans `docs/questions.md` (une information manque ou se contredit dans le guide), transmets-la à Karim (rôle 1) : c'est lui qui connaît la plateforme réelle.
- Durée estimée : 25 à 40 heures de travail de Claude Code, sur 2 à 4 semaines. Le plus long est le jalon 6 (traduire les commandes des rôles 2 à 6 en effets visuels).

## 4. Rendre le travail

Pousse la branche : `git push -u origin role-1-plateforme-graphique`. Karim verra ton avancement sur GitHub.

## 5. À ne jamais faire

- Ne mets aucun mot de passe, aucune clé privée, aucun fichier `.env` dans le dépôt.
- Le mot de passe du compte admin de la plateforme se règle avec `dotnet user-secrets`, jamais dans le code (le prompt le prévoit).
