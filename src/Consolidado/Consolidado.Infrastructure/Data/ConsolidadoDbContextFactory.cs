using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Consolidado.Infrastructure.Data;

public sealed class ConsolidadoDbContextFactory : IDesignTimeDbContextFactory<ConsolidadoDbContext>
{
    public ConsolidadoDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ConsolidadoDbContext>()
            .UseNpgsql("Host=localhost;Database=consolidado_db;Username=postgres;Password=design-time")
            .Options;
        return new ConsolidadoDbContext(options);
    }
}
