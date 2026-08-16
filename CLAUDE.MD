# RandoTracker

Application d'analyse de traces GPX, développée par Grégory — apprenti ingénieur
en génie industriel, automaticien de métier, randonneur et chef scout.

Ce fichier définit **comment travailler sur ce projet**. Lis-le entièrement
avant toute intervention.

---

## Objectif du projet

Analyser mes propres traces GPX pour mieux préparer mes sorties : distance,
dénivelé, pente, durée réelle, et à terme un modèle de vitesse personnel qui
remplacera les formules génériques (Naismith, Munter, DIN 33466) par mes
performances réelles par type de terrain.

À terme, l'application couvrira aussi l'inventaire du matériel outdoor et une
bibliothèque de randonnées préparées.

---

## Règle de travail — la plus importante

**C'est un projet d'apprentissage. Commente et explique tout ce que tu fait.**

### Ce que j'attends de toi

- **Explique l'approche avant de proposer du code.** Décris l'algorithme en
  français, indique les briques du langage à utiliser.
- **Quand je bloque, réponds à la question précise**.
- **Signale les erreurs et explique pourquoi c'est faux**.

### Ce qui me fait perdre mon temps

- Les exercices artificiels pour « me faire découvrir » quelque chose que je
  connais déjà (bases de données relationnelles, SQL, clés étrangères, C/C++,
  algorithmique : acquis en BUT GEII et au travail).
- Les corrections de forme présentées comme pédagogiques.
- Les longues explications sur ce que je viens de faire fonctionner.

Va droit au but. Je préfère une objection nette à une explication délayée.

---

## Ce que je maîtrise / ce que j'apprends

**Acquis** : C/C++ embarqué, bases de données relationnelles, SQL, Structured
Text et automatisme industriel, Linux (Fedora KDE), Git, auto-hébergement
(VPS, Docker, Traefik).

**En cours** : C# et .NET (débuté en août 2026), LINQ, ASP.NET Core, la
séparation front/back .

**Peu pratiqué** : Python (je sais lire et modifier, pas construire),
JavaScript et CSS.

---

## Architecture

```
RandoTracker.Core/    Calculs, modèle de données, accès SQLite
RandoTracker.Cli/     Interface console (analyse, archivage, profils ASCII)
RandoTracker.Web/     Serveur ASP.NET Core Minimal API + front statique
```

### Modèle de données

```
Sorties  1 ─── n  Traces  1 ─── n  Profils
```

- Une **Sortie** est un événement vécu (« Raid Champsaur, 3 jours »).
- Une **Trace** est un fichier GPX. Selon la montre et l'habitude de chacun,
  une sortie donne une trace ou une dizaine.
- Un **Profil** est la série de points d'une trace, échantillonnée (25 m par
  défaut) pour limiter le volume.

Les totaux d'une sortie **ne sont pas stockés** : ce sont des agrégats calculés
par jointure. Une trace ajoutée après coup met les totaux à jour toute seule.

---

## Décisions techniques déjà prises

Ne les remets pas en cause sans raison ; si tu penses qu'une est mauvaise,
dis-le et explique pourquoi.

### Filtrage du bruit altimétrique

Le dénivelé est cumulé par **hystérésis** : une altitude de référence qui ne se
déplace que lorsque l'écart dépasse un seuil, dans un sens comme dans l'autre.
Sans ça, le bruit du capteur gonfle le D+ de 15 à 100 %.

Le seuil dépend de la source :
- trace baro (montre) : 0,5 à 1 m — calibré à **0,6 m** sur mes traces
- GPS seul (téléphone) : 3 à 5 m
- trace redessinée sur carte : pas de bruit

`ExtraireSegments` est la **source unique de vérité** : le dénivelé cumulé et
la répartition par tranche de pente en découlent tous les deux, donc les deux
chiffres sont toujours cohérents.

### Pente

Mesurée sur une **fenêtre centrée** (50 m de part et d'autre, soit 100 m).
Une pente point à point ne veut rien dire : elle explose sur les intervalles
courts. La fenêtre doit rester un paramètre, et toute « pente max » doit
préciser sur quelle longueur elle est mesurée.

### Durée en mouvement

Seuil de vitesse **très bas** (0,1 m/s) : en montagne on progresse souvent à
1-2 km/h sans être à l'arrêt, et un passage d'escalade se fait à 0,3 km/h en
plein effort. Seuls les vrais zéros doivent être écartés.

Calibré contre une trace réelle dont je connais le déroulement : 3h42 de
mouvement pour 4h20 de durée totale.

### Culture et parsing

- Lecture des nombres : **toujours en culture invariante**. Le cast
  `(double?)` de LINQ to XML le fait nativement, c'est pour ça qu'on le préfère
  à `double.Parse` sur `.Value`.
- Affichage : culture locale (française).
- Dates stockées en `yyyy-MM-dd HH:mm:ss` — se trie correctement en TEXT.

### SQLite

- `PRAGMA foreign_keys = ON` à chaque ouverture, sinon les contraintes sont
  ignorées **silencieusement**.
- Insertion des profils dans une **transaction** : sinon SQLite en ouvre une
  par ligne et l'insertion prend des secondes.
- `AddWithValue` ne traduit pas un `null` C# en `NULL` SQL — il faut
  `DBNull.Value`, sinon SQLite lève « Value must be set ».
- Unicité des traces sur `(Nom, Date)`. **Choix assumé** : deux traces sans
  horodatage peuvent faire doublon, SQLite considérant deux `NULL` comme
  distincts.

### Robustesse GPX

Les fichiers réels sont irréguliers. Le code doit encaisser sans planter :
- balise `<name>` absente → repli sur le nom du fichier
- points sans `<ele>` ou sans `<time>`
- fichier vide, sans trace, ou XML invalide
- namespace GPX obligatoire sur les éléments (`gpx + "trkpt"`), mais **pas**
  sur les attributs (`lat`, `lon`)

---

## Conventions de code

- Commentaires, noms de variables et messages utilisateur **en français**.
- PascalCase pour les types et méthodes, camelCase pour les variables locales.
- Accolades Style K&R .
- Commentaires XML `///` sur les fonctions publiques.
- Les commentaires expliquent **pourquoi**, pas quoi. Un commentaire qui
  paraphrase la ligne suivante est du bruit.
- Pattern matching (`is double a`, `is not null`) plutôt que casts et
  comparaisons à `null`.
- Pas de `SELECT *` : les index de colonne du reader dépendent de l'ordre du
  `SELECT`, et une colonne ajoutée décalerait tout silencieusement.

---

## Avant de proposer quoi que ce soit

1. **Vérifie plutôt que supposer.** Si tu n'es pas certain d'une API, d'une
   cotation ou d'un comportement, dis-le. Je préfère « je ne sais pas » à une
   affirmation fausse — c'est déjà arrivé et ça m'a coûté du temps.
2. **Teste sur les fichiers de `tests/`** : ils couvrent les cas tordus
   (trace vide, points sans coordonnées, sans nom ni horodatage).
3. **Compile** (`dotnet build`) avant d'annoncer que c'est bon.

---

## État actuel et pistes

Fait : lecture GPX, distance (Haversine), D+/D− filtrés, pente sur fenêtre,
durée totale et en mouvement, vitesse ascensionnelle, répartition par tranche
de pente, profils ASCII, base à trois tables, traitement par lot, API JSON,
front minimal.

À venir, dans cet ordre approximatif :
1. Coordonnées `Lat`/`Lon` dans `Profils` — prérequis à la carte
2. Route de détail `/api/sorties/{id}` et vue liste → détail
3. Carte Leaflet (tuiles IGN ou OSM)
4. Graphiques dynamiques (zoom, mise à jour) — je travaille sur des courbes de
   supervision toute la journée, du statique ne me suffira pas
5. Modèle de vitesse personnel par tranche de pente, puis prédiction de durée
   sur une trace planifiée
6. Authentification avant exposition sur mon VPS
7. Inventaire du matériel, bibliothèque de randos préparées# RandoTracker
