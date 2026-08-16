using Microsoft.Data.Sqlite;
using RandoTracker.Core.Calculs;
using RandoTracker.Core.Modele;

namespace RandoTracker.Core.Donnees;

public static class TraceRepository
{
    /// <summary>
    /// Insère la trace et son profil dans une transaction unique : sans elle,
    /// SQLite ouvrirait une transaction par ligne et l'insertion de plusieurs
    /// centaines de points prendrait des secondes au lieu de millisecondes.
    /// </summary>
    public static ResultatArchivage Archiver(SqliteConnection connection, Analyse a,
                                              long sortieId, double pasProfil)
    {
        using var transaction = connection.BeginTransaction();

        long traceId;

        try
        {
            var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"
                INSERT INTO Traces (
                    SortieId, Nom, Date, Source, DistanceKm, AltitudeMin, AltitudeMax,
                    DenivelePositif, DeniveleNegatif, PenteMaxMontee, PenteMaxDescente,
                    DureeTotaleMin, DureeMouvementMin, TempsEnMonteeMin, VitesseAscensionnelle,
                    SeuilDenivele, SeuilVitesse, Fichier)
                VALUES (
                    $sortieId, $nom, $date, $source, $distanceKm, $altMin, $altMax,
                    $dPlus, $dMoins, $penteMontee, $penteDescente,
                    $dureeTotale, $dureeMouvement, $tempsMontee, $vitesseAsc,
                    $seuilDenivele, $seuilVitesse, $fichier);
                SELECT last_insert_rowid();";

            // ADO.NET ne traduit pas un null C# en NULL SQL : il faut DBNull.Value.
            object Val(object? v) => v ?? DBNull.Value;

            cmd.Parameters.AddWithValue("$sortieId", sortieId);
            cmd.Parameters.AddWithValue("$nom", a.Nom);
            cmd.Parameters.AddWithValue("$date", Val(a.Depart?.ToString("yyyy-MM-dd HH:mm:ss")));
            cmd.Parameters.AddWithValue("$source", a.Source);
            cmd.Parameters.AddWithValue("$distanceKm", a.DistanceTotale / 1000);
            cmd.Parameters.AddWithValue("$altMin", a.AltitudeMin);
            cmd.Parameters.AddWithValue("$altMax", a.AltitudeMax);
            cmd.Parameters.AddWithValue("$dPlus", a.DenivelePositif);
            cmd.Parameters.AddWithValue("$dMoins", a.DeniveleNegatif);
            cmd.Parameters.AddWithValue("$penteMontee", a.PenteMaxMontee);
            cmd.Parameters.AddWithValue("$penteDescente", a.PenteMaxDescente);
            cmd.Parameters.AddWithValue("$dureeTotale", Val(a.DureeTotale?.TotalMinutes));
            cmd.Parameters.AddWithValue("$dureeMouvement", Val(a.DureeMouvement?.TotalMinutes));
            cmd.Parameters.AddWithValue("$tempsMontee", Val(a.TempsEnMontee?.TotalMinutes));
            cmd.Parameters.AddWithValue("$vitesseAsc", Val(a.VitesseAscensionnelle));
            cmd.Parameters.AddWithValue("$seuilDenivele", a.SeuilDenivele);
            cmd.Parameters.AddWithValue("$seuilVitesse", a.SeuilVitesse);
            cmd.Parameters.AddWithValue("$fichier", a.Fichier);

            traceId = Convert.ToInt64(cmd.ExecuteScalar());
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // contrainte d'unicité
        {
            transaction.Rollback();
            return new ResultatArchivage(DejaExistante: true, TraceId: 0, PointsStockes: 0, PointsTotal: a.Profil.Count);
        }

        // ── Profil ──

        List<PointProfil> aStocker = Echantillonnage.EchantillonnerProfil(a.Profil, pasProfil);

        var insertion = connection.CreateCommand();
        insertion.Transaction = transaction;
        insertion.CommandText = @"
            INSERT INTO Profils (TraceId, DistanceCumulee, Altitude, Pente, Temps, Lat, Lon)
            VALUES ($traceId, $distance, $altitude, $pente, $temps, $lat, $lon)";

        // Les paramètres sont créés une fois puis réaffectés à chaque tour :
        // SQLite réutilise alors la requête compilée.
        var pTrace = insertion.CreateParameter(); pTrace.ParameterName = "$traceId";
        var pDist = insertion.CreateParameter(); pDist.ParameterName = "$distance";
        var pAlt = insertion.CreateParameter(); pAlt.ParameterName = "$altitude";
        var pPente = insertion.CreateParameter(); pPente.ParameterName = "$pente";
        var pTemps = insertion.CreateParameter(); pTemps.ParameterName = "$temps";
        var pLat = insertion.CreateParameter(); pLat.ParameterName = "$lat";
        var pLon = insertion.CreateParameter(); pLon.ParameterName = "$lon";

        insertion.Parameters.AddRange(new[] { pTrace, pDist, pAlt, pPente, pTemps, pLat, pLon });
        pTrace.Value = traceId;

        foreach (PointProfil p in aStocker)
        {
            pDist.Value = p.DistanceCumulee;
            pAlt.Value = p.Altitude;
            pPente.Value = p.Pente;
            pTemps.Value = (object?)p.Time?.ToString("yyyy-MM-dd HH:mm:ss") ?? DBNull.Value;
            pLat.Value = p.Lat;
            pLon.Value = p.Lon;

            insertion.ExecuteNonQuery();
        }

        transaction.Commit();

        return new ResultatArchivage(DejaExistante: false, TraceId: traceId,
                                      PointsStockes: aStocker.Count, PointsTotal: a.Profil.Count);
    }

    public static bool Existe(SqliteConnection connection, long id)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Traces WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    /// <summary>Détail complet d'une trace, avec l'identité de sa sortie, ou null si elle n'existe pas.</summary>
    public static TraceDetail? ObtenirDetail(SqliteConnection connection, long id)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT t.Id, t.Nom, t.Date, t.SortieId, s.Nom, t.Source, t.DistanceKm, t.AltitudeMin, t.AltitudeMax,
                   t.DenivelePositif, t.DeniveleNegatif, t.PenteMaxMontee, t.PenteMaxDescente,
                   t.DureeTotaleMin, t.DureeMouvementMin, t.TempsEnMonteeMin, t.VitesseAscensionnelle,
                   t.SeuilDenivele, t.SeuilVitesse, t.Fichier
            FROM Traces t
            JOIN Sorties s ON s.Id = t.SortieId
            WHERE t.Id = $id";
        cmd.Parameters.AddWithValue("$id", id);

        using var reader = cmd.ExecuteReader();

        if (!reader.Read()) return null;

        return new TraceDetail(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.GetInt64(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetDouble(6),
            reader.GetDouble(7),
            reader.GetDouble(8),
            reader.GetDouble(9),
            reader.GetDouble(10),
            reader.GetDouble(11),
            reader.GetDouble(12),
            reader.IsDBNull(13) ? null : reader.GetDouble(13),
            reader.IsDBNull(14) ? null : reader.GetDouble(14),
            reader.IsDBNull(15) ? null : reader.GetDouble(15),
            reader.IsDBNull(16) ? null : reader.GetDouble(16),
            reader.GetDouble(17),
            reader.GetDouble(18),
            reader.GetString(19));
    }

    /// <summary>La sortie d'une trace, ou null si la trace n'existe pas.</summary>
    public static long? ObtenirSortieId(SqliteConnection connection, long id)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT SortieId FROM Traces WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);

        object? resultat = cmd.ExecuteScalar();
        return resultat is null or DBNull ? null : Convert.ToInt64(resultat);
    }

    /// <summary>
    /// Un tracé sous-échantillonné (toutes traces de la sortie mises bout à
    /// bout), pour un croquis léger dans le fil des sorties — sans instancier
    /// une carte Leaflet par carte. Pas plus de <paramref name="maxPoints"/>
    /// points renvoyés, quelle que soit la densité du tracé d'origine.
    /// </summary>
    public static List<PointCarte> ObtenirApercuSortie(SqliteConnection connection, long sortieId, int maxPoints = 50)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT p.Lat, p.Lon FROM Profils p
            JOIN Traces t ON t.Id = p.TraceId
            WHERE t.SortieId = $sortieId AND p.Lat IS NOT NULL AND p.Lon IS NOT NULL
            ORDER BY t.Id, p.DistanceCumulee";
        cmd.Parameters.AddWithValue("$sortieId", sortieId);

        using var reader = cmd.ExecuteReader();

        var tous = new List<PointCarte>();
        while (reader.Read()) tous.Add(new PointCarte(reader.GetDouble(0), reader.GetDouble(1)));

        if (tous.Count <= maxPoints) return tous;

        var resultat = new List<PointCarte>(maxPoints);
        double pas = (double)tous.Count / maxPoints;
        for (int i = 0; i < maxPoints; i++) resultat.Add(tous[(int)(i * pas)]);
        return resultat;
    }

    /// <summary>Supprime la trace et, par cascade, son profil.</summary>
    public static void Supprimer(SqliteConnection connection, long id)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Traces WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public static void Renommer(SqliteConnection connection, long id, string nom)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Traces SET Nom = $nom WHERE Id = $id";
        cmd.Parameters.AddWithValue("$nom", nom);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Réaffecte la trace à une autre sortie — les dates des deux sorties ne sont pas recalculées ici.</summary>
    public static void Reassigner(SqliteConnection connection, long id, long nouvelleSortieId)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Traces SET SortieId = $sortieId WHERE Id = $id";
        cmd.Parameters.AddWithValue("$sortieId", nouvelleSortieId);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Le profil altitude/pente d'une trace, dans l'ordre du parcours — de quoi
    /// tracer les graphiques altitude et pente.
    /// </summary>
    public static List<PointProfilDto> ObtenirProfil(SqliteConnection connection, long traceId)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT DistanceCumulee, Altitude, Pente, Lat, Lon, Temps FROM Profils
            WHERE TraceId = $id
            ORDER BY DistanceCumulee";
        cmd.Parameters.AddWithValue("$id", traceId);

        using var reader = cmd.ExecuteReader();

        var resultat = new List<PointProfilDto>();

        while (reader.Read())
        {
            resultat.Add(new PointProfilDto(
                reader.GetDouble(0),
                reader.GetDouble(1),
                reader.GetDouble(2),
                reader.IsDBNull(3) ? null : reader.GetDouble(3),
                reader.IsDBNull(4) ? null : reader.GetDouble(4),
                reader.IsDBNull(5) ? null : reader.GetString(5)));
        }

        return resultat;
    }

    /// <summary>Toutes les traces, rattachées à leur sortie.</summary>
    public static List<TraceAvecSortie> ObtenirToutes(SqliteConnection connection)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT t.Id, t.Nom, t.Date, t.DistanceKm, t.DenivelePositif, t.DeniveleNegatif,
                   t.DureeMouvementMin, t.VitesseAscensionnelle, t.SortieId, s.Nom
            FROM Traces t
            JOIN Sorties s ON s.Id = t.SortieId
            ORDER BY t.Date DESC";

        using var reader = cmd.ExecuteReader();

        var resultat = new List<TraceAvecSortie>();

        while (reader.Read())
        {
            resultat.Add(new TraceAvecSortie(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetDouble(3),
                reader.GetDouble(4),
                reader.GetDouble(5),
                reader.IsDBNull(6) ? null : reader.GetDouble(6),
                reader.IsDBNull(7) ? null : reader.GetDouble(7),
                reader.GetInt64(8),
                reader.GetString(9)));
        }

        return resultat;
    }
}
