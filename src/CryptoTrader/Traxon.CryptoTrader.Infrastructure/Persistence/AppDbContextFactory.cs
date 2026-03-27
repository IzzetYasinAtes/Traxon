using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Traxon.CryptoTrader.Infrastructure.Persistence;

/// <summary>
/// Design-time factory — EF Core CLI migration araçları için kullanılır.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=TraxonDev;Trusted_Connection=True;")
            .Options;
        return new AppDbContext(options);
    }
}
