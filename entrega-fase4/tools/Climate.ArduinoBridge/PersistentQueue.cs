using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Climate.ArduinoBridge;

public sealed class PersistentQueue
{
    private readonly string connectionString;
    private readonly BufferOptions options;
    private readonly SemaphoreSlim gate = new(1, 1);
    public PersistentQueue(IOptions<BridgeOptions> config)
    {
        options = config.Value.Buffer;
        connectionString = new SqliteConnectionStringBuilder { DataSource = options.Path }.ToString();
        using SqliteConnection connection = new(connectionString);
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL; CREATE TABLE IF NOT EXISTS pending (id TEXT PRIMARY KEY, payload TEXT NOT NULL, created INTEGER NOT NULL);";
        command.ExecuteNonQuery();
    }
    public async Task EnqueueAsync(TelemetryReading reading, CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            using SqliteConnection connection = new(connectionString);
            await connection.OpenAsync(token);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT count(*) FROM pending";
            long count = Convert.ToInt64(await command.ExecuteScalarAsync(token));
            if (count >= options.MaxReadings) throw new IOException("Persistent buffer full; acquisition paused. Restore backend connectivity.");
            command.CommandText = "INSERT OR IGNORE INTO pending(id,payload,created) VALUES($id,$payload,$created)";
            command.Parameters.AddWithValue("$id", reading.ReadingId.ToString());
            command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(reading, Protocol.Json));
            command.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            await command.ExecuteNonQueryAsync(token);
        }
        finally { gate.Release(); }
    }
    public async Task<TelemetryReading?> PeekAsync(CancellationToken token)
    {
        using SqliteConnection connection = new(connectionString);
        await connection.OpenAsync(token);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT payload FROM pending ORDER BY rowid LIMIT 1";
        string? payload = await command.ExecuteScalarAsync(token) as string;
        return payload is null ? null : JsonSerializer.Deserialize<TelemetryReading>(payload, Protocol.Json);
    }
    public async Task RemoveAsync(Guid id, CancellationToken token)
    {
        using SqliteConnection connection = new(connectionString);
        await connection.OpenAsync(token);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM pending WHERE id=$id";
        command.Parameters.AddWithValue("$id", id.ToString());
        await command.ExecuteNonQueryAsync(token);
    }
}
