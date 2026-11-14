using System.Text.RegularExpressions;
using Dapper;

namespace PharmaCore.Persistence;

/// <summary>Applies numbered .sql scripts (NNN_name.sql) once each, in order, tracked in dbo.SchemaVersions.</summary>
public sealed class DbMigrator(ISqlConnectionFactory db)
{
    public async Task<IReadOnlyList<string>> ApplyAsync(string migrationsDirectory, CancellationToken ct = default)
    {
        var applied = new List<string>();
        await using var conn = await db.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            IF OBJECT_ID('dbo.SchemaVersions') IS NULL
            CREATE TABLE dbo.SchemaVersions (
                Version INT NOT NULL CONSTRAINT PK_SchemaVersions PRIMARY KEY,
                Name NVARCHAR(200) NOT NULL,
                AppliedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_SchemaVersions_Applied DEFAULT SYSUTCDATETIME())", cancellationToken: ct));

        var done = (await conn.QueryAsync<int>(new CommandDefinition("SELECT Version FROM dbo.SchemaVersions", cancellationToken: ct))).ToHashSet();

        foreach (var file in Directory.GetFiles(migrationsDirectory, "*.sql").OrderBy(f => f, StringComparer.Ordinal))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (!int.TryParse(name.Split('_')[0], out var version) || done.Contains(version)) continue;

            var batches = Regex.Split(await File.ReadAllTextAsync(file, ct), @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
                .Where(b => !string.IsNullOrWhiteSpace(b));

            await using var tx = await conn.BeginTransactionAsync(ct);
            foreach (var batch in batches)
                await conn.ExecuteAsync(new CommandDefinition(batch, transaction: tx, cancellationToken: ct));
            await conn.ExecuteAsync(new CommandDefinition("INSERT dbo.SchemaVersions (Version, Name) VALUES (@version, @name)",
                new { version, name }, tx, cancellationToken: ct));
            await tx.CommitAsync(ct);
            applied.Add(name);
        }
        return applied;
    }
}
