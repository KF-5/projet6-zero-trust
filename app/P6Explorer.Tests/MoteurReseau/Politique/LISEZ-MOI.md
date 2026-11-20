# Jeux de test de `test_flux` (états A et B)

Fichiers recopiés pour les tests du moteur réseau (jalon 3). Ils ne remplacent pas `politique/` du rôle 2.

- `flows.csv`, `segments.csv`, `hosts.csv` : exemples de la page **Contrats** du guide (`sources/guide/data/contrats.js`).
- `tests-A.csv`, `tests-B.csv` : produits par le **vrai** `politique/c2.py` du guide (rôle 2, étape 6), sans modification :

```
python3 c2.py --etat A --dossier politique   # 8 règles sur 3 interfaces, 8 tests, 1 avertissement (FA4)
python3 c2.py --etat B --dossier politique   # 16 règles sur 7 interfaces, 42 tests, 0 avertissement
```

Ce sont les nombres annoncés dans le guide. Quand le rôle 2 commitera ses propres fichiers dans `politique/`,
il suffira de relancer `c2.py` et de recopier `tests.csv` ici.
