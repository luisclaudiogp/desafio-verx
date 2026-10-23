using Consolidado.Domain.Entities;
using Consolidado.Infrastructure.Projecao;
using Microsoft.EntityFrameworkCore;

namespace Consolidado.Infrastructure.Data;

public class ConsolidadoDbContext(DbContextOptions<ConsolidadoDbContext> options) : DbContext(options)
{
    public DbSet<Movimento> Movimentos => Set<Movimento>();
    public DbSet<SaldoDiario> Saldos => Set<SaldoDiario>();
    public DbSet<SaldoSnapshot> Snapshots => Set<SaldoSnapshot>();
    public DbSet<EventoRejeitado> EventosRejeitados => Set<EventoRejeitado>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Movimento>(entity =>
        {
            entity.ToTable("movimentos");
            entity.HasKey(e => e.LancamentoId);
            entity.Property(e => e.Tipo).HasConversion<short>().IsRequired();
            entity.Property(e => e.Valor).HasPrecision(18, 2).IsRequired();
            entity.Ignore(e => e.Dia);
            entity.HasIndex(e => new { e.ContaId, e.OcorridoEm }).IncludeProperties(e => new { e.Tipo, e.Valor });
        });

        modelBuilder.Entity<SaldoDiario>(entity =>
        {
            entity.ToTable("saldos_diarios");
            entity.HasKey(e => new { e.ContaId, e.Data });
            entity.Property(e => e.TotalCreditos).HasPrecision(18, 2).IsRequired();
            entity.Property(e => e.TotalDebitos).HasPrecision(18, 2).IsRequired();
            entity.Ignore(e => e.Saldo);
        });

        modelBuilder.Entity<SaldoSnapshot>(entity =>
        {
            entity.ToTable("saldo_snapshots");
            entity.HasKey(e => new { e.ContaId, e.Data });
            entity.Property(e => e.TotalCreditos).HasPrecision(18, 2).IsRequired();
            entity.Property(e => e.TotalDebitos).HasPrecision(18, 2).IsRequired();
            entity.Property(e => e.GeradoEm).IsRequired();
            entity.Ignore(e => e.Saldo);
        });

        modelBuilder.Entity<EventoRejeitado>(entity =>
        {
            entity.ToTable("eventos_rejeitados");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Payload).IsRequired();
            entity.Property(e => e.Motivo).HasMaxLength(500).IsRequired();
            entity.HasIndex(e => e.RejeitadoEm);
        });
    }
}
