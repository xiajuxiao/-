using Microsoft.Data.Sqlite;

namespace TranslatorTool.Data;

public static class DatabaseInitializer
{
    public static void EnsureDatabase(string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS TranslationHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SourceText TEXT NOT NULL,
                TranslatedText TEXT NOT NULL,
                Notes TEXT,
                PhoneticsJson TEXT,
                SourceLanguage TEXT,
                TargetLanguage TEXT,
                TriggerSource TEXT,
                Engine TEXT,
                CreatedAt TEXT NOT NULL,
                FromOcr INTEGER NOT NULL DEFAULT 0,
                AudioPath TEXT
            );

            CREATE INDEX IF NOT EXISTS IX_TranslationHistory_CreatedAt
            ON TranslationHistory (CreatedAt DESC);
            """;
        command.ExecuteNonQuery();
    }
}
