using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Lancamentos.Infrastructure.Data;

public sealed class LancamentosDbContextFactory : IDesignTimeDbContextFactory<LancamentosDbContext>
{
    public LancamentosDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<LancamentosDbContext>()
            .UseNpgsql("Host=localhost;Database=lancamentos_db;Username=postgres;Password=design-time")
            .Options;
        return new LancamentosDbContext(options);
    }
}
