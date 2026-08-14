using System.Xml.Linq;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/api/sorties", () => ObtenirSorties());
app.UseDefaultFiles();  
app.UseStaticFiles();



app.Run();

static List<SortieDto> ObtenirSorties()
{
    using var connection = new SqliteConnection("Data Source=randos.db");
    connection.Open();

    var cmd = connection.CreateCommand();
    cmd.CommandText = "SELECT Id, Nom, DateDebut FROM Sorties ORDER BY DateDebut DESC";

    var resultat = new List<SortieDto>();
    using var reader = cmd.ExecuteReader();

    while (reader.Read())
    {
        resultat.Add(new SortieDto(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2)));
    }

    return resultat;
}


record SortieDto(long Id, string Nom, string? DateDebut);