"""API REST du catalogue - Projet 6 (composant C5).

Specification : rapport de conception, section 13.1.
ATTENTION : GET /api/produit est VOLONTAIREMENT vulnerable a l'injection SQL
(scenario S-03). A n'utiliser que dans le laboratoire isole.
Toutes les autres requetes sont parametrees.
"""
import os
from decimal import Decimal

import psycopg2
import psycopg2.extras
from flask import Flask, jsonify, request

app = Flask(__name__)
app.json.ensure_ascii = False


def connexion():
    return psycopg2.connect(
        host=os.environ["DB_HOST"],
        dbname=os.environ.get("DB_NAME", "catalogue"),
        user=os.environ["DB_USER"],
        password=os.environ["DB_PASSWORD"],
        connect_timeout=2,
        cursor_factory=psycopg2.extras.RealDictCursor,
    )


def erreur(message, code):
    return jsonify({"erreur": message}), code


def propre(ligne):
    """Convertit les Decimal en float pour le JSON."""
    return {k: float(v) if isinstance(v, Decimal) else v for k, v in ligne.items()}


@app.get("/api/sante")
def sante():
    try:
        with connexion() as cnx, cnx.cursor() as cur:
            cur.execute("SELECT 1")
        return jsonify({"statut": "ok", "base": "ok"}), 200
    except psycopg2.OperationalError:
        return jsonify({"statut": "degrade", "base": "injoignable"}), 503


@app.get("/api/produits")
def produits():
    categorie = request.args.get("categorie")
    if categorie is not None and not categorie.lstrip("-").isdigit():
        return erreur("categorie doit etre un entier", 400)
    sql = ("SELECT p.id, p.nom, c.nom AS categorie, p.prix, p.stock "
           "FROM produit p JOIN categorie c ON c.id = p.categorie_id")
    params = ()
    if categorie is not None:
        sql += " WHERE p.categorie_id = %s"
        params = (int(categorie),)
    sql += " ORDER BY p.id"
    try:
        with connexion() as cnx, cnx.cursor() as cur:
            cur.execute(sql, params)
            return jsonify([propre(l) for l in cur.fetchall()]), 200
    except psycopg2.OperationalError:
        return erreur("base injoignable", 503)


@app.get("/api/produit")
def produit():
    # VULNERABLE (S-03) : concatenation directe du parametre dans la requete.
    ident = request.args.get("id", "")
    sql = "SELECT id, nom, prix, stock FROM produit WHERE id = " + ident
    try:
        with connexion() as cnx, cnx.cursor() as cur:
            cur.execute(sql)
            lignes = [propre(l) for l in cur.fetchall()]
    except psycopg2.OperationalError:
        return erreur("base injoignable", 503)
    except psycopg2.Error as e:
        # Message SQL renvoye tel quel : aide volontaire a l'injection (laboratoire).
        return erreur("erreur SQL : " + str(e).strip(), 500)
    if not lignes:
        return erreur("produit introuvable", 404)
    return jsonify(lignes[0] if len(lignes) == 1 else lignes), 200


@app.post("/api/commande")
def commande():
    corps = request.get_json(silent=True) or {}
    try:
        produit_id = int(corps["produit_id"])
        utilisateur_id = int(corps["utilisateur_id"])
        quantite = int(corps["quantite"])
        if quantite <= 0:
            raise ValueError
    except (KeyError, TypeError, ValueError):
        return erreur("champ manquant ou invalide", 400)
    try:
        with connexion() as cnx, cnx.cursor() as cur:
            cur.execute("UPDATE produit SET stock = stock - %s "
                        "WHERE id = %s AND stock >= %s RETURNING id",
                        (quantite, produit_id, quantite))
            if cur.fetchone() is None:
                cur.execute("SELECT 1 FROM produit WHERE id = %s", (produit_id,))
                if cur.fetchone() is None:
                    return erreur("champ manquant ou invalide", 400)
                return erreur("stock insuffisant", 409)
            cur.execute("INSERT INTO commande (produit_id, utilisateur_id, quantite) "
                        "VALUES (%s, %s, %s) RETURNING id",
                        (produit_id, utilisateur_id, quantite))
            nouvelle = cur.fetchone()["id"]
        return jsonify({"commande_id": nouvelle}), 201
    except psycopg2.errors.ForeignKeyViolation:
        return erreur("champ manquant ou invalide", 400)
    except psycopg2.OperationalError:
        return erreur("base injoignable", 503)


@app.errorhandler(404)
def introuvable(_):
    return erreur("ressource introuvable", 404)


@app.errorhandler(405)
def methode(_):
    return erreur("methode non autorisee", 405)
