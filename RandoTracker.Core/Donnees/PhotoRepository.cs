using Microsoft.Data.Sqlite;

namespace RandoTracker.Core.Donnees;

/// <summary>Une photo rattachée à une sortie. CheminRelatif est relatif à wwwroot.</summary>
public record Photo(long Id, long SortieId, string NomFichier, string CheminRelatif);

public static class PhotoRepository
{
    public static long Ajouter(SqliteConnection connection, long sortieId, string nomFichier, string cheminRelatif)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Photos (SortieId, NomFichier, CheminRelatif, DateAjout)
            VALUES ($sortieId, $nomFichier, $cheminRelatif, $dateAjout);
            SELECT last_insert_rowid();";

        cmd.Parameters.AddWithValue("$sortieId", sortieId);
        cmd.Parameters.AddWithValue("$nomFichier", nomFichier);
        cmd.Parameters.AddWithValue("$cheminRelatif", cheminRelatif);
        cmd.Parameters.AddWithValue("$dateAjout", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    public static List<Photo> ObtenirPourSortie(SqliteConnection connection, long sortieId)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, SortieId, NomFichier, CheminRelatif FROM Photos
            WHERE SortieId = $sortieId
            ORDER BY DateAjout";
        cmd.Parameters.AddWithValue("$sortieId", sortieId);

        using var reader = cmd.ExecuteReader();

        var resultat = new List<Photo>();

        while (reader.Read())
        {
            resultat.Add(new Photo(reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2), reader.GetString(3)));
        }

        return resultat;
    }

    /// <summary>Une entrée par sortie demandée, même sans photo (liste vide) — pour composer le fil sans une requête par sortie.</summary>
    public static Dictionary<long, List<Photo>> ObtenirGroupeesParSortie(SqliteConnection connection, IEnumerable<long> sortieIds)
    {
        var ids = sortieIds.ToList();
        var resultat = ids.ToDictionary(id => id, _ => new List<Photo>());
        if (ids.Count == 0) return resultat;

        var cmd = connection.CreateCommand();
        string placeholders = string.Join(",", ids.Select((_, i) => $"$id{i}"));
        cmd.CommandText = $@"
            SELECT Id, SortieId, NomFichier, CheminRelatif FROM Photos
            WHERE SortieId IN ({placeholders})
            ORDER BY DateAjout";
        for (int i = 0; i < ids.Count; i++) cmd.Parameters.AddWithValue($"$id{i}", ids[i]);

        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var photo = new Photo(reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2), reader.GetString(3));
            resultat[photo.SortieId].Add(photo);
        }

        return resultat;
    }

    /// <summary>Le chemin relatif d'une photo — nécessaire avant Supprimer() pour effacer aussi le fichier sur disque.</summary>
    public static string? ObtenirChemin(SqliteConnection connection, long id)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT CheminRelatif FROM Photos WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);

        return cmd.ExecuteScalar() as string;
    }

    public static void Supprimer(SqliteConnection connection, long id)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Photos WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }
}
