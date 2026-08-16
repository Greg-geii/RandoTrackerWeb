using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace RandoTracker.Core.Donnees;

public static class SortieRepository
{
    public static bool Existe(SqliteConnection connection, long id)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Sorties WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    /// <summary>Sorties dont la période chevauche celle du lot, à un jour près.</summary>
    public static List<(long Id, string Nom, string Periode)> Proches(
        SqliteConnection connection, DateTime? debut, DateTime? fin)
    {
        var resultat = new List<(long, string, string)>();

        var cmd = connection.CreateCommand();

        if (debut is null)
        {
            cmd.CommandText = @"
                SELECT Id, Nom, DateDebut, DateFin FROM Sorties
                ORDER BY DateDebut DESC LIMIT 5";
        }
        else
        {
            cmd.CommandText = @"
                SELECT Id, Nom, DateDebut, DateFin FROM Sorties
                WHERE DateFin   >= $marge_avant
                  AND DateDebut <= $marge_apres
                ORDER BY DateDebut DESC LIMIT 5";

            cmd.Parameters.AddWithValue("$marge_avant",
                debut.Value.AddDays(-1).ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("$marge_apres",
                (fin ?? debut.Value).AddDays(1).ToString("yyyy-MM-dd 23:59:59"));
        }

        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            string d = reader.IsDBNull(2) ? "?" : reader.GetString(2)[..10];
            string f = reader.IsDBNull(3) ? "?" : reader.GetString(3)[..10];

            resultat.Add((reader.GetInt64(0), reader.GetString(1),
                          d == f ? d : $"{d} → {f}"));
        }

        return resultat;
    }

    public static long Creer(SqliteConnection connection, string nom,
                             DateTime? debut = null, DateTime? fin = null)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Sorties (Nom, DateDebut, DateFin) VALUES ($nom, $debut, $fin);
            SELECT last_insert_rowid();";

        cmd.Parameters.AddWithValue("$nom", nom);
        cmd.Parameters.AddWithValue("$debut",
            (object?)debut?.ToString("yyyy-MM-dd HH:mm:ss") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$fin",
            (object?)fin?.ToString("yyyy-MM-dd HH:mm:ss") ?? DBNull.Value);

        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    /// <summary>Supprime la sortie et, par cascade (FOREIGN KEY ON DELETE CASCADE), ses traces et leurs profils.</summary>
    public static void Supprimer(SqliteConnection connection, long id)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Sorties WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public static void Renommer(SqliteConnection connection, long id, string nom)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Sorties SET Nom = $nom WHERE Id = $id";
        cmd.Parameters.AddWithValue("$nom", nom);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public static void ModifierTags(SqliteConnection connection, long id, List<string> tags)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Sorties SET Tags = $tags WHERE Id = $id";
        cmd.Parameters.AddWithValue("$tags", JsonSerializer.Serialize(tags));
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Recale les dates de la sortie sur les traces qu'elle contient.</summary>
    public static void RecalculerDates(SqliteConnection connection, long sortieId)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE Sorties SET
                DateDebut = (SELECT MIN(Date) FROM Traces WHERE SortieId = $id),
                DateFin   = (SELECT MAX(Date) FROM Traces WHERE SortieId = $id)
            WHERE Id = $id
              AND EXISTS (SELECT 1 FROM Traces WHERE SortieId = $id AND Date IS NOT NULL)";

        cmd.Parameters.AddWithValue("$id", sortieId);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Toutes les sorties avec leurs totaux, calculés par jointure.</summary>
    public static List<SortieAvecTotaux> ObtenirToutes(SqliteConnection connection)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT s.Id, s.Nom, s.DateDebut, s.DateFin, COUNT(t.Id),
                   SUM(t.DistanceKm), SUM(t.DenivelePositif), SUM(t.DeniveleNegatif),
                   SUM(t.DureeMouvementMin),
                   -- Vitesse ascensionnelle globale : D+ cumulé / temps de montée
                   -- cumulé. Une moyenne des vitesses par trace donnerait le même
                   -- poids à une trace de 200 m et à une de 1200 m.
                   CASE WHEN SUM(t.TempsEnMonteeMin) > 0
                        THEN SUM(t.DenivelePositif) / (SUM(t.TempsEnMonteeMin) / 60.0)
                   END,
                   s.Tags
            FROM Sorties s
            LEFT JOIN Traces t ON t.SortieId = s.Id
            GROUP BY s.Id
            ORDER BY s.DateDebut DESC";

        using var reader = cmd.ExecuteReader();

        var resultat = new List<SortieAvecTotaux>();

        while (reader.Read())
        {
            resultat.Add(new SortieAvecTotaux(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetInt32(4),
                reader.IsDBNull(5) ? 0 : reader.GetDouble(5),
                reader.IsDBNull(6) ? 0 : reader.GetDouble(6),
                reader.IsDBNull(7) ? 0 : reader.GetDouble(7),
                reader.IsDBNull(8) ? null : reader.GetDouble(8),
                reader.IsDBNull(9) ? null : reader.GetDouble(9),
                LireTags(reader, 10)));
        }

        return resultat;
    }

    /// <summary>Une sortie et ses totaux, ou null si elle n'existe pas.</summary>
    public static SortieAvecTotaux? ObtenirParId(SqliteConnection connection, long sortieId)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT s.Id, s.Nom, s.DateDebut, s.DateFin,
                   COUNT(t.Id), SUM(t.DistanceKm), SUM(t.DenivelePositif),
                   SUM(t.DeniveleNegatif), SUM(t.DureeMouvementMin),
                   CASE WHEN SUM(t.TempsEnMonteeMin) > 0
                        THEN SUM(t.DenivelePositif) / (SUM(t.TempsEnMonteeMin) / 60.0)
                   END,
                   s.Tags
            FROM Sorties s
            LEFT JOIN Traces t ON t.SortieId = s.Id
            WHERE s.Id = $id
            GROUP BY s.Id";
        cmd.Parameters.AddWithValue("$id", sortieId);

        using var reader = cmd.ExecuteReader();

        if (!reader.Read()) return null;

        return new SortieAvecTotaux(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetInt32(4),
            reader.IsDBNull(5) ? 0 : reader.GetDouble(5),
            reader.IsDBNull(6) ? 0 : reader.GetDouble(6),
            reader.IsDBNull(7) ? 0 : reader.GetDouble(7),
            reader.IsDBNull(8) ? null : reader.GetDouble(8),
            reader.IsDBNull(9) ? null : reader.GetDouble(9),
            LireTags(reader, 10));
    }

    private static List<string> LireTags(SqliteDataReader reader, int index)
    {
        if (reader.IsDBNull(index)) return [];
        return JsonSerializer.Deserialize<List<string>>(reader.GetString(index)) ?? [];
    }

    /// <summary>Les traces d'une sortie (jointure Sorties → Traces).</summary>
    public static List<TraceDeSortie> ObtenirTraces(SqliteConnection connection, long sortieId)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, Nom, Date, DistanceKm, DenivelePositif, DeniveleNegatif
            FROM Traces WHERE SortieId = $id ORDER BY Date";
        cmd.Parameters.AddWithValue("$id", sortieId);

        using var reader = cmd.ExecuteReader();

        var resultat = new List<TraceDeSortie>();

        while (reader.Read())
        {
            resultat.Add(new TraceDeSortie(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetDouble(3),
                reader.GetDouble(4),
                reader.GetDouble(5)));
        }

        return resultat;
    }
}
