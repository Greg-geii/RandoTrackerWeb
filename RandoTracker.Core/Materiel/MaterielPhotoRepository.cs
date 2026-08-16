using Microsoft.Data.Sqlite;

namespace RandoTracker.Core.Materiel;

public static class MaterielPhotoRepository
{
    public static long Ajouter(SqliteConnection connection, long candidatId, string nomFichier, string cheminRelatif)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO MaterielPhotos (CandidatId, NomFichier, CheminRelatif, DateAjout)
            VALUES ($candidatId, $nomFichier, $cheminRelatif, $dateAjout);
            SELECT last_insert_rowid();";

        cmd.Parameters.AddWithValue("$candidatId", candidatId);
        cmd.Parameters.AddWithValue("$nomFichier", nomFichier);
        cmd.Parameters.AddWithValue("$cheminRelatif", cheminRelatif);
        cmd.Parameters.AddWithValue("$dateAjout", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    public static List<MaterielPhoto> ObtenirPourCandidat(SqliteConnection connection, long candidatId)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, CandidatId, NomFichier, CheminRelatif FROM MaterielPhotos
            WHERE CandidatId = $candidatId
            ORDER BY DateAjout";
        cmd.Parameters.AddWithValue("$candidatId", candidatId);

        using var reader = cmd.ExecuteReader();

        var resultat = new List<MaterielPhoto>();

        while (reader.Read())
        {
            resultat.Add(new MaterielPhoto(reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2), reader.GetString(3)));
        }

        return resultat;
    }

    /// <summary>Une photo par candidat, pour composer les listes sans une requête par candidat.</summary>
    public static Dictionary<long, List<MaterielPhoto>> ObtenirGroupeesParCandidat(SqliteConnection connection, IEnumerable<long> candidatIds)
    {
        var ids = candidatIds.ToList();
        var resultat = ids.ToDictionary(id => id, _ => new List<MaterielPhoto>());
        if (ids.Count == 0) return resultat;

        var cmd = connection.CreateCommand();
        string placeholders = string.Join(",", ids.Select((_, i) => $"$id{i}"));
        cmd.CommandText = $@"
            SELECT Id, CandidatId, NomFichier, CheminRelatif FROM MaterielPhotos
            WHERE CandidatId IN ({placeholders})
            ORDER BY DateAjout";
        for (int i = 0; i < ids.Count; i++) cmd.Parameters.AddWithValue($"$id{i}", ids[i]);

        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var photo = new MaterielPhoto(reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2), reader.GetString(3));
            resultat[photo.CandidatId].Add(photo);
        }

        return resultat;
    }

    /// <summary>Le chemin relatif d'une photo — nécessaire avant Supprimer() pour effacer aussi le fichier sur disque.</summary>
    public static string? ObtenirChemin(SqliteConnection connection, long id)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT CheminRelatif FROM MaterielPhotos WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);

        return cmd.ExecuteScalar() as string;
    }

    public static void Supprimer(SqliteConnection connection, long id)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM MaterielPhotos WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }
}
