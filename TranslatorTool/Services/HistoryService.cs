using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TranslatorTool.Data;
using TranslatorTool.Models;

namespace TranslatorTool.Services;

public class HistoryService(AppDbContext dbContext)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public async Task AddAsync(TranslationResult result, string triggerSource, bool fromOcr, string? audioPath, CancellationToken cancellationToken)
    {
        await using var connection = dbContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO TranslationHistory (
                SourceText, TranslatedText, Notes, PhoneticsJson, SourceLanguage,
                TargetLanguage, TriggerSource, Engine, CreatedAt, FromOcr, AudioPath
            )
            VALUES (
                $sourceText, $translatedText, $notes, $phoneticsJson, $sourceLanguage,
                $targetLanguage, $triggerSource, $engine, $createdAt, $fromOcr, $audioPath
            );
            """;

        command.Parameters.AddWithValue("$sourceText", result.SourceText);
        command.Parameters.AddWithValue("$translatedText", result.TranslatedText);
        command.Parameters.AddWithValue("$notes", result.Notes);
        command.Parameters.AddWithValue("$phoneticsJson", JsonSerializer.Serialize(result.Phonetics, JsonOptions));
        command.Parameters.AddWithValue("$sourceLanguage", result.SourceLanguage);
        command.Parameters.AddWithValue("$targetLanguage", result.TargetLanguage);
        command.Parameters.AddWithValue("$triggerSource", triggerSource);
        command.Parameters.AddWithValue("$engine", result.Engine);
        command.Parameters.AddWithValue("$createdAt", DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$fromOcr", fromOcr ? 1 : 0);
        command.Parameters.AddWithValue("$audioPath", (object?)audioPath ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<List<TranslationHistoryItem>> GetRecentAsync(int limit, CancellationToken cancellationToken)
    {
        await using var connection = dbContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, SourceText, TranslatedText, Notes, PhoneticsJson, SourceLanguage,
                   TargetLanguage, TriggerSource, Engine, CreatedAt, FromOcr, AudioPath
            FROM TranslationHistory
            ORDER BY CreatedAt DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        var items = new List<TranslationHistoryItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadHistoryItem(reader));
        }

        return items;
    }

    public async Task ClearAsync(CancellationToken cancellationToken)
    {
        await using var connection = dbContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM TranslationHistory;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static TranslationHistoryItem ReadHistoryItem(SqliteDataReader reader)
    {
        return new TranslationHistoryItem
        {
            Id = reader.GetInt64(0),
            SourceText = reader.GetString(1),
            TranslatedText = reader.GetString(2),
            Notes = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
            PhoneticsJson = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
            SourceLanguage = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
            TargetLanguage = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
            TriggerSource = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
            Engine = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(9), CultureInfo.InvariantCulture),
            FromOcr = reader.GetInt64(10) == 1,
            AudioPath = reader.IsDBNull(11) ? null : reader.GetString(11)
        };
    }
}
