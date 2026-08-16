using Microsoft.Data.Sqlite;

namespace RandoTracker.Core.Donnees;

/// <summary>
/// Ouvre une connexion SQLite prête à l'emploi : PRAGMA foreign_keys activé
/// et schéma créé s'il n'existe pas encore. À utiliser dans un `using`.
/// </summary>
public sealed class RandoDb : IDisposable
{
    public SqliteConnection Connexion { get; }

    public RandoDb(string cheminFichier)
    {
        Connexion = new SqliteConnection($"Data Source={cheminFichier}");
        Connexion.Open();

        // SQLite ignore les clés étrangères par défaut : sans ce PRAGMA, les
        // contraintes déclarées ne sont pas appliquées, silencieusement.
        using (var pragma = Connexion.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON";
            pragma.ExecuteNonQuery();
        }

        CreerSchema();
    }

    private void CreerSchema()
    {
        var cmd = Connexion.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Sorties (
                Id        INTEGER PRIMARY KEY AUTOINCREMENT,
                Nom       TEXT NOT NULL,
                DateDebut TEXT,
                DateFin   TEXT,
                Lieu      TEXT,
                Type      TEXT
            );

            CREATE TABLE IF NOT EXISTS Traces (
                Id                    INTEGER PRIMARY KEY AUTOINCREMENT,
                SortieId              INTEGER NOT NULL,
                Nom                   TEXT NOT NULL,
                Date                  TEXT,
                Source                TEXT,
                DistanceKm            REAL,
                AltitudeMin           REAL,
                AltitudeMax           REAL,
                DenivelePositif       REAL,
                DeniveleNegatif       REAL,
                PenteMaxMontee        REAL,
                PenteMaxDescente      REAL,
                DureeTotaleMin        REAL,
                DureeMouvementMin     REAL,
                TempsEnMonteeMin      REAL,
                VitesseAscensionnelle REAL,
                SeuilDenivele         REAL,
                SeuilVitesse          REAL,
                Fichier               TEXT NOT NULL,
                -- Choix assumé : deux traces sans horodatage peuvent faire doublon,
                -- SQLite considérant deux NULL comme distincts dans un UNIQUE.
                UNIQUE(Nom, Date),
                FOREIGN KEY (SortieId) REFERENCES Sorties(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS Profils (
                Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                TraceId         INTEGER NOT NULL,
                DistanceCumulee REAL,
                Altitude        REAL,
                Pente           REAL,
                Temps           TEXT,
                Lat             REAL,
                Lon             REAL,
                FOREIGN KEY (TraceId) REFERENCES Traces(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS Photos (
                Id            INTEGER PRIMARY KEY AUTOINCREMENT,
                SortieId      INTEGER NOT NULL,
                NomFichier    TEXT NOT NULL,
                CheminRelatif TEXT NOT NULL,
                DateAjout     TEXT NOT NULL,
                FOREIGN KEY (SortieId) REFERENCES Sorties(Id) ON DELETE CASCADE
            );

            -- ── Domaine matériel : indépendant du domaine GPX ci-dessus, ne
            -- partage que cette base et ce fichier de schéma. Voir
            -- RandoTracker.Core/Materiel pour la logique applicative.
            CREATE TABLE IF NOT EXISTS MaterielCategories (
                Id       INTEGER PRIMARY KEY AUTOINCREMENT,
                Nom      TEXT NOT NULL,
                Priorite INTEGER,
                Notes    TEXT
            );

            CREATE TABLE IF NOT EXISTS MaterielCandidats (
                Id             INTEGER PRIMARY KEY AUTOINCREMENT,
                CategorieId    INTEGER NOT NULL,
                Marque         TEXT,
                Modele         TEXT NOT NULL,
                PrixIndicatif  REAL,
                Url            TEXT,
                Statut         TEXT NOT NULL, -- a_etudier | retenu | ecarte | achete
                Motif          TEXT,
                -- Attributs propres à la catégorie (un baudrier n'a rien de
                -- commun avec un casque) : objet JSON en texte plutôt qu'une
                -- table clé-valeur, voir la discussion dans PLAN/commit.
                Specs          TEXT,
                EssayageRequis INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (CategorieId) REFERENCES MaterielCategories(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS MaterielPossessions (
                Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                CandidatId      INTEGER NOT NULL,
                DateAchat       TEXT,
                PrixPaye        REAL,
                Taille          TEXT,
                Etat            TEXT,
                DateLimiteUsage TEXT, -- EPI à durée de vie limitée (corde, sangles, casque…)
                NotesUsage      TEXT,
                FOREIGN KEY (CandidatId) REFERENCES MaterielCandidats(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS MaterielPhotos (
                Id            INTEGER PRIMARY KEY AUTOINCREMENT,
                CandidatId    INTEGER NOT NULL,
                NomFichier    TEXT NOT NULL,
                CheminRelatif TEXT NOT NULL,
                DateAjout     TEXT NOT NULL,
                FOREIGN KEY (CandidatId) REFERENCES MaterielCandidats(Id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_traces_sortie ON Traces(SortieId);
            CREATE INDEX IF NOT EXISTS idx_profils_trace ON Profils(TraceId);
            CREATE INDEX IF NOT EXISTS idx_photos_sortie ON Photos(SortieId);
            CREATE INDEX IF NOT EXISTS idx_materiel_candidats_categorie ON MaterielCandidats(CategorieId);
            CREATE INDEX IF NOT EXISTS idx_materiel_possessions_candidat ON MaterielPossessions(CandidatId);
            CREATE INDEX IF NOT EXISTS idx_materiel_photos_candidat ON MaterielPhotos(CandidatId);
            ";
        cmd.ExecuteNonQuery();

        // Migration : les bases créées avant l'ajout de ces colonnes n'ont pas
        // ces colonnes — le CREATE TABLE ci-dessus ne les touche pas puisque la
        // table existe déjà (IF NOT EXISTS). Sans danger sur une base neuve :
        // les colonnes sont déjà là, la vérification ne fait rien.
        AjouterColonneSiAbsente("Profils", "Lat", "REAL");
        AjouterColonneSiAbsente("Profils", "Lon", "REAL");
        AjouterColonneSiAbsente("MaterielCandidats", "Tags", "TEXT");
        AjouterColonneSiAbsente("MaterielCandidats", "Disciplines", "TEXT");
        AjouterColonneSiAbsente("Sorties", "Tags", "TEXT");
    }

    private void AjouterColonneSiAbsente(string table, string colonne, string type)
    {
        var pragma = Connexion.CreateCommand();
        pragma.CommandText = $"PRAGMA table_info({table})";

        using (var reader = pragma.ExecuteReader())
        {
            while (reader.Read())
            {
                // Dans PRAGMA table_info, la colonne 1 est le nom de la colonne.
                if (string.Equals(reader.GetString(1), colonne, StringComparison.OrdinalIgnoreCase))
                    return;
            }
        }

        var alter = Connexion.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {colonne} {type}";
        alter.ExecuteNonQuery();
    }

    public void Dispose() => Connexion.Dispose();
}
