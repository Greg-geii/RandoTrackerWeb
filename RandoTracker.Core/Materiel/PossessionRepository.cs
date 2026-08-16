using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace RandoTracker.Core.Materiel;

public static class PossessionRepository
{
    public static bool Existe(SqliteConnection connection, long id)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM MaterielPossessions WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        return cmd.ExecuteScalar() is not null;
    }

    /// <summary>L'inventaire complet, le plus récemment acheté en premier.</summary>
    public static List<PossessionAvecCandidat> ObtenirToutes(SqliteConnection connection)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT p.Id, p.DateAchat, p.PrixPaye, p.Taille, p.Etat, p.DateLimiteUsage, p.NotesUsage,
                   cd.Id, cd.Marque, cd.Modele, c.Id, c.Nom, cd.Tags, cd.Disciplines
            FROM MaterielPossessions p
            JOIN MaterielCandidats cd ON cd.Id = p.CandidatId
            JOIN MaterielCategories c ON c.Id = cd.CategorieId
            ORDER BY p.DateAchat DESC";

        using var reader = cmd.ExecuteReader();

        var resultat = new List<PossessionAvecCandidat>();

        while (reader.Read())
        {
            resultat.Add(new PossessionAvecCandidat(
                reader.GetInt64(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetDouble(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.GetInt64(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.GetString(9),
                reader.GetInt64(10),
                reader.GetString(11),
                LireListe(reader, 12),
                LireListe(reader, 13)));
        }

        return resultat;
    }

    private static List<string> LireListe(SqliteDataReader reader, int index)
    {
        if (reader.IsDBNull(index)) return [];
        return JsonSerializer.Deserialize<List<string>>(reader.GetString(index)) ?? [];
    }

    /// <summary>
    /// Possessions dont la durée de vie est dépassée ou arrive à échéance sous
    /// 90 jours — assez tôt pour prévoir un remplacement avant une sortie,
    /// pas si tôt que l'alerte devienne du bruit permanent.
    /// </summary>
    public static List<Alerte> ObtenirAlertes(SqliteConnection connection)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT p.Id, cd.Id, cd.Marque, cd.Modele, c.Id, c.Nom, p.DateLimiteUsage
            FROM MaterielPossessions p
            JOIN MaterielCandidats cd ON cd.Id = p.CandidatId
            JOIN MaterielCategories c ON c.Id = cd.CategorieId
            WHERE p.DateLimiteUsage IS NOT NULL
              AND date(p.DateLimiteUsage) <= date('now', '+90 days')
            ORDER BY p.DateLimiteUsage";

        using var reader = cmd.ExecuteReader();

        var resultat = new List<Alerte>();

        while (reader.Read())
        {
            resultat.Add(new Alerte(
                reader.GetInt64(0),
                reader.GetInt64(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3),
                reader.GetInt64(4),
                reader.GetString(5),
                reader.GetString(6)));
        }

        return resultat;
    }

    public static long Creer(SqliteConnection connection, long candidatId, string? dateAchat, double? prixPaye,
        string? taille, string? etat, string? dateLimiteUsage, string? notesUsage)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO MaterielPossessions
                (CandidatId, DateAchat, PrixPaye, Taille, Etat, DateLimiteUsage, NotesUsage)
            VALUES
                ($candidatId, $dateAchat, $prixPaye, $taille, $etat, $dateLimiteUsage, $notesUsage);
            SELECT last_insert_rowid();";

        cmd.Parameters.AddWithValue("$candidatId", candidatId);
        cmd.Parameters.AddWithValue("$dateAchat", (object?)dateAchat ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$prixPaye", (object?)prixPaye ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$taille", (object?)taille ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$etat", (object?)etat ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$dateLimiteUsage", (object?)dateLimiteUsage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$notesUsage", (object?)notesUsage ?? DBNull.Value);

        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    public static void Modifier(SqliteConnection connection, long id, string? dateAchat, double? prixPaye,
        string? taille, string? etat, string? dateLimiteUsage, string? notesUsage)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE MaterielPossessions SET
                DateAchat = $dateAchat, PrixPaye = $prixPaye, Taille = $taille,
                Etat = $etat, DateLimiteUsage = $dateLimiteUsage, NotesUsage = $notesUsage
            WHERE Id = $id";

        cmd.Parameters.AddWithValue("$dateAchat", (object?)dateAchat ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$prixPaye", (object?)prixPaye ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$taille", (object?)taille ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$etat", (object?)etat ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$dateLimiteUsage", (object?)dateLimiteUsage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$notesUsage", (object?)notesUsage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$id", id);

        cmd.ExecuteNonQuery();
    }
}
