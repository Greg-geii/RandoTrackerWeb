using Microsoft.Data.Sqlite;

namespace RandoTracker.Core.Materiel;

public static class CategorieRepository
{
    public static bool Existe(SqliteConnection connection, long id)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM MaterielCategories WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        return cmd.ExecuteScalar() is not null;
    }

    /// <summary>
    /// Toutes les catégories, triées par priorité (les non précisées en
    /// dernier), avec le nombre de candidats et le niveau d'avancement le
    /// plus élevé atteint parmi eux.
    /// </summary>
    public static List<CategorieAvecCompteurs> ObtenirToutes(SqliteConnection connection)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT c.Id, c.Nom, c.Priorite, c.Notes,
                   COUNT(cd.Id) AS NombreCandidats,
                   MAX(CASE
                       WHEN cd.Statut = 'achete' THEN 3
                       WHEN cd.Statut = 'retenu' THEN 2
                       WHEN cd.Id IS NOT NULL THEN 1
                       ELSE 0
                   END) AS NiveauAvancement,
                   -- Photo d'un candidat de la catégorie, en priorisant le plus
                   -- avancé (acheté > retenu > le reste) puis le plus récent.
                   (SELECT p.CheminRelatif FROM MaterielPhotos p
                    JOIN MaterielCandidats cd2 ON cd2.Id = p.CandidatId
                    WHERE cd2.CategorieId = c.Id
                    ORDER BY CASE cd2.Statut WHEN 'achete' THEN 3 WHEN 'retenu' THEN 2 ELSE 1 END DESC,
                             p.DateAjout DESC
                    LIMIT 1) AS PhotoCheminRelatif
            FROM MaterielCategories c
            LEFT JOIN MaterielCandidats cd ON cd.CategorieId = c.Id
            GROUP BY c.Id
            ORDER BY c.Priorite IS NULL, c.Priorite, c.Nom";

        using var reader = cmd.ExecuteReader();

        var resultat = new List<CategorieAvecCompteurs>();

        while (reader.Read())
        {
            string statutAvancement = reader.GetInt32(5) switch
            {
                3 => "achete",
                2 => "tranche",
                1 => "en_cours",
                _ => "rien",
            };

            resultat.Add(new CategorieAvecCompteurs(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetInt32(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetInt32(4),
                statutAvancement,
                reader.IsDBNull(6) ? null : reader.GetString(6)));
        }

        return resultat;
    }

    public static long Creer(SqliteConnection connection, string nom, int? priorite, string? notes)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO MaterielCategories (Nom, Priorite, Notes) VALUES ($nom, $priorite, $notes);
            SELECT last_insert_rowid();";

        cmd.Parameters.AddWithValue("$nom", nom);
        cmd.Parameters.AddWithValue("$priorite", (object?)priorite ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$notes", (object?)notes ?? DBNull.Value);

        return Convert.ToInt64(cmd.ExecuteScalar());
    }
}
