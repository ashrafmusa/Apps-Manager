using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ExcellCore.Domain.Data;
using Microsoft.EntityFrameworkCore;

namespace ExcellCore.Tests;

internal sealed class TestSqliteContextFactory : IDbContextFactory<ExcellCoreContext>, IDisposable
{
    private readonly DbContextOptions<ExcellCoreContext> _options;
    private readonly string _databasePath;

    public TestSqliteContextFactory()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"excellcore-tests-{Guid.NewGuid():N}.db");
        _options = new DbContextOptionsBuilder<ExcellCoreContext>()
            .UseSqlite($"Data Source={_databasePath}")
            .Options;

        using var context = new ExcellCoreContext(_options);
        context.Database.EnsureDeleted();
        context.Database.Migrate();
    }

    public ExcellCoreContext CreateDbContext()
    {
        return new ExcellCoreContext(_options);
    }

    public Task<ExcellCoreContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateDbContext());
    }

    public void Dispose()
    {
        try
        {
            using var context = new ExcellCoreContext(_options);
            context.Database.EnsureDeleted();
        }
        catch
        {
            // ignore cleanup failures in tests
        }

        try
        {
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
        catch
        {
            // ignore cleanup failures in tests
        }
    }
}
