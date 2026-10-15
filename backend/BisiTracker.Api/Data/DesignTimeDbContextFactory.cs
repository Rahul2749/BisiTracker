using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BisiTracker.Api.Data;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<LedgerDbContext>
{
    public LedgerDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Database")
            ?? "Host=localhost;Port=54329;Database=bisitracker;Username=bisi;Password=bisi-local-only";
        return new LedgerDbContext(new DbContextOptionsBuilder<LedgerDbContext>().UseNpgsql(connection).Options);
    }
}
