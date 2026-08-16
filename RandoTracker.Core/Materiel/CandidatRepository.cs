using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace RandoTracker.Core.Materiel;

public static class CandidatRepository
{
    public static bool Existe(SqliteConnection connection, long id)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM MaterielCandidats WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        return cmd.ExecuteScalar() is not null;
    }

    public static List<Candidat> ObtenirParCategorie(SqliteConnection connection, long categorieId)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, CategorieId, Marque, Modele, PrixIndicatif, Url, Statut, Motif, Specs, EssayageRequis, Tags, Disciplines
            FROM MaterielCandidats
            WHERE CategorieId = $categorieId
            ORDER BY Statut = 'ecarte', Id";
        cmd.Parameters.AddWithValue("$categorieId", categorieId);

        using var reader = cmd.ExecuteReader();

        var resultat = new List<Candidat>();
        while (reader.Read()) resultat.Add(Lire(reader));
        return resultat;
    }

    public static Candidat? ObtenirParId(SqliteConnection connection, long id)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, CategorieId, Marque, Modele, PrixIndicatif, Url, Statut, Motif, Specs, EssayageRequis, Tags, Disciplines
            FROM MaterielCandidats
            WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? Lire(reader) : null;
    }

    /// <summary>Tous les candidats, pour les filtres transverses (tag/discipline) sur l'inventaire.</summary>
    public static List<Candidat> ObtenirTous(SqliteConnection connection)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, CategorieId, Marque, Modele, PrixIndicatif, Url, Statut, Motif, Specs, EssayageRequis, Tags, Disciplines
            FROM MaterielCandidats";

        using var reader = cmd.ExecuteReader();

        var resultat = new List<Candidat>();
        while (reader.Read()) resultat.Add(Lire(reader));
        return resultat;
    }

    public static long Creer(SqliteConnection connection, long categorieId, string? marque, string modele,
        double? prixIndicatif, string? url, string statut, string? motif, JsonElement? specs, bool essayageRequis,
        List<string> tags, List<string> disciplines)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO MaterielCandidats
                (CategorieId, Marque, Modele, PrixIndicatif, Url, Statut, Motif, Specs, EssayageRequis, Tags, Disciplines)
            VALUES
                ($categorieId, $marque, $modele, $prixIndicatif, $url, $statut, $motif, $specs, $essayageRequis, $tags, $disciplines);
            SELECT last_insert_rowid();";

        AjouterParametres(cmd, categorieId, marque, modele, prixIndicatif, url, statut, motif, specs, essayageRequis, tags, disciplines);

        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    /// <summary>Remplacement complet (sémantique PUT) : le front renvoie l'objet entier, modifié.</summary>
    public static void Modifier(SqliteConnection connection, long id, long categorieId, string? marque, string modele,
        double? prixIndicatif, string? url, string statut, string? motif, JsonElement? specs, bool essayageRequis,
        List<string> tags, List<string> disciplines)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE MaterielCandidats SET
                CategorieId = $categorieId, Marque = $marque, Modele = $modele,
                PrixIndicatif = $prixIndicatif, Url = $url, Statut = $statut,
                Motif = $motif, Specs = $specs, EssayageRequis = $essayageRequis,
                Tags = $tags, Disciplines = $disciplines
            WHERE Id = $id";

        AjouterParametres(cmd, categorieId, marque, modele, prixIndicatif, url, statut, motif, specs, essayageRequis, tags, disciplines);
        cmd.Parameters.AddWithValue("$id", id);

        cmd.ExecuteNonQuery();
    }

    /// <summary>Utilisé par l'achat : le candidat devient une possession, rien d'autre ne change.</summary>
    public static void ChangerStatut(SqliteConnection connection, long id, string statut)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE MaterielCandidats SET Statut = $statut WHERE Id = $id";
        cmd.Parameters.AddWithValue("$statut", statut);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    private static void AjouterParametres(SqliteCommand cmd, long categorieId, string? marque, string modele,
        double? prixIndicatif, string? url, string statut, string? motif, JsonElement? specs, bool essayageRequis,
        List<string> tags, List<string> disciplines)
    {
        cmd.Parameters.AddWithValue("$categorieId", categorieId);
        cmd.Parameters.AddWithValue("$marque", (object?)marque ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$modele", modele);
        cmd.Parameters.AddWithValue("$prixIndicatif", (object?)prixIndicatif ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$url", (object?)url ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$statut", statut);
        cmd.Parameters.AddWithValue("$motif", (object?)motif ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$specs", specs.HasValue ? specs.Value.GetRawText() : DBNull.Value);
        cmd.Parameters.AddWithValue("$essayageRequis", essayageRequis ? 1 : 0);
        cmd.Parameters.AddWithValue("$tags", JsonSerializer.Serialize(tags));
        cmd.Parameters.AddWithValue("$disciplines", JsonSerializer.Serialize(disciplines));
    }

    private static Candidat Lire(SqliteDataReader reader)
    {
        JsonElement? specs = null;
        if (!reader.IsDBNull(8))
        {
            using JsonDocument document = JsonDocument.Parse(reader.GetString(8));
            specs = document.RootElement.Clone();
        }

        return new Candidat(
            reader.GetInt64(0),
            reader.GetInt64(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetDouble(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            specs,
            reader.GetInt32(9) != 0,
            LireListe(reader, 10),
            LireListe(reader, 11));
    }

    private static List<string> LireListe(SqliteDataReader reader, int index)
    {
        if (reader.IsDBNull(index)) return [];
        return JsonSerializer.Deserialize<List<string>>(reader.GetString(index)) ?? [];
    }
}
