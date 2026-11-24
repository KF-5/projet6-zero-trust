# Format de `synthese.csv` (rôle 6)

P6 Explorer lit `resultats/synthese/synthese.csv` pour le plan **P-7** de la vidéo (barres A contre B) et pour l'écran de fin **P-8**. Tant que ce fichier est absent ou invalide, P-7 montre des valeurs d'exemple sous un bandeau rouge « ILLUSTRATION — données fictives ».

Déposer le fichier suffit : pas besoin de recompiler ni de redémarrer l'application (il est relu à chaque affichage).

## Colonnes

Fichier CSV en UTF-8, séparateur virgule, **point** décimal, une ligne d'en-tête exactement :

```
indicateur,libelle,etat,valeur,unite
```

| Colonne | Contenu | Exemple |
|---|---|---|
| `indicateur` | identifiant court, sans espace ; le même pour les lignes A et B | `taux_detection` |
| `libelle` | texte affiché à l'écran | `Taux de détection` |
| `etat` | `A` ou `B` | `A` |
| `valeur` | nombre positif, point décimal, sans unité | `87.5` |
| `unite` | unité affichée après la valeur ; la même en A et en B | `%`, `s`, `Mbit/s` |

## Règles

- Chaque indicateur a **exactement une ligne A et une ligne B**.
- Autant d'indicateurs que nécessaire : la vue suit le fichier (une paire de barres par indicateur, dans l'ordre du fichier).
- Une seule erreur (en-tête, état, valeur illisible, ligne A ou B manquante) et tout le fichier est écarté : la vue repasse en illustration. La liste des erreurs s'affiche sur la page `/Video`.

## Exemple (trois indicateurs attendus)

```
indicateur,libelle,etat,valeur,unite
taux_detection,Taux de détection,A,…,%
taux_detection,Taux de détection,B,…,%
temps_detection,Temps moyen de détection,A,…,s
temps_detection,Temps moyen de détection,B,…,s
effet_debit,Débit avec la sécurité active,A,…,Mbit/s
effet_debit,Débit avec la sécurité active,B,…,Mbit/s
```

Remplacer chaque `…` par la valeur mesurée (voir `resultats/A/` et `resultats/B/`).
