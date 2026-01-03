using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ExcellCore.Domain.Data;

public sealed class DesignTimeExcellCoreContextFactory : IDesignTimeDbContextFactory<ExcellCoreContext>
{
    public ExcellCoreContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ExcellCoreContext>();

        var dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ExcellCore");
        Directory.CreateDirectory(dataDirectory);
        var databasePath = Path.Combine(dataDirectory, "excellcore.db");

        optionsBuilder.UseSqlite($"Data Source={databasePath}");

        return new ExcellCoreContext(optionsBuilder.Options);
    }
}
