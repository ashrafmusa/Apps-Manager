using System;
using System.Data.Common;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ExcellCore.Domain.Data;

public static class ExcellCoreContextMigrationExtensions
{
    private const string InitialMigrationId = "202512270001_InitialCreate";
    private const string InitialProductVersion = "8.0.0";

    public static async Task EnsureDatabaseMigratedAsync(this ExcellCoreContext dbContext, CancellationToken cancellationToken)
    {
        await EnsureMigrationHistoryAsync(dbContext, cancellationToken);
        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    private static async Task EnsureMigrationHistoryAsync(ExcellCoreContext dbContext, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var agreementsExists = await TableExistsAsync(connection, "Agreements", cancellationToken);
            var historyExists = await TableExistsAsync(connection, "__EFMigrationsHistory", cancellationToken);

            if (agreementsExists && !historyExists)
            {
                await ExecuteNonQueryAsync(connection,
                    "CREATE TABLE \"__EFMigrationsHistory\" (\"MigrationId\" TEXT NOT NULL CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY, \"ProductVersion\" TEXT NOT NULL);",
                    cancellationToken);

                await ExecuteNonQueryAsync(connection,
                    "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES (@MigrationId, @ProductVersion);",
                    cancellationToken,
                    ("@MigrationId", InitialMigrationId),
                    ("@ProductVersion", InitialProductVersion));
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name;";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@name";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null && Convert.ToInt32(result, CultureInfo.InvariantCulture) > 0;
    }

    private static async Task ExecuteNonQueryAsync(DbConnection connection, string sql, CancellationToken cancellationToken, params (string Name, string Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
