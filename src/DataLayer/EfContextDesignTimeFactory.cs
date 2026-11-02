using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Pgvector.EntityFrameworkCore;
using System;
using System.IO;

namespace Fistix.TaskManager.DataLayer;

public class EfContextDesignTimeFactory : IDesignTimeDbContextFactory<WriteEfContext>
{
    public WriteEfContext CreateDbContext(string[] args)
    {
        var basePath = ResolveConfigBasePath();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("MainDb")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__MainDb");

        var optionsBuilder = new DbContextOptionsBuilder<WriteEfContext>();
        optionsBuilder.UseNpgsql(connectionString, o => o.UseVector());

        return new WriteEfContext(optionsBuilder.Options);
    }

    private static string ResolveConfigBasePath()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "..", "WebApi"),
            Directory.GetCurrentDirectory()
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(Path.Combine(candidate, "appsettings.json")))
                return candidate;
        }

        return Directory.GetCurrentDirectory();
    }
}
