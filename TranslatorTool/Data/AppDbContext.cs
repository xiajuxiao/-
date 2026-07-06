using Microsoft.Data.Sqlite;
using System.IO;

namespace TranslatorTool.Data;

public class AppDbContext
{
    public AppDbContext(string? databasePath = null)
    {
        DatabasePath = databasePath ?? GetDefaultDatabasePath();
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath
        }.ToString();
        DatabaseInitializer.EnsureDatabase(ConnectionString);
    }

    public string DatabasePath { get; }
    public string ConnectionString { get; }

    public SqliteConnection CreateConnection()
    {
        return new SqliteConnection(ConnectionString);
    }

    private static string GetDefaultDatabasePath()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TranslatorTool");
        return Path.Combine(folder, "history.db");
    }
}
