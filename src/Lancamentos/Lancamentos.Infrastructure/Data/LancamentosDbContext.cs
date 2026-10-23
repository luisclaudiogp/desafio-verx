using Lancamentos.Domain.Entities;
using Lancamentos.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Verx.SharedKernel;

namespace Lancamentos.Infrastructure.Data;

public class LancamentosDbContext(DbContextOptions<LancamentosDbContext> options) : DbContext(options)
{
    public DbSet<Lancamento> Lancamentos => Set<Lancamento>();
    public DbSet<PosicaoConta> Posicoes => Set<PosicaoConta>();
    public DbSet<OutboxEvento> OutboxEventos => Set<OutboxEvento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Lancamento>(entity =>
        {
            entity.ToTable("lancamentos");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Valor)
                .HasConversion(v => v.Valor, v => Dinheiro.Criar(v))
                .HasPrecision(18, 2)
                .IsRequired();
            entity.Property(e => e.Tipo).HasConversion<short>().IsRequired();
            entity.Property(e => e.OcorridoEm).IsRequired();
            entity.Property(e => e.RegistradoEm).IsRequired();
            entity.Property(e => e.RegistradoPor).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ChaveIdempotencia)
                .HasMaxLength(Lancamento.TamanhoMaximoChaveIdempotencia)
                .IsRequired();

            entity.Property(e => e.HashRequisicao).HasMaxLength(64).IsRequired().HasDefaultValue(string.Empty);

            entity.HasIndex(e => new { e.ContaId, e.ChaveIdempotencia }).IsUnique();
            entity.HasIndex(e => e.EstornoDeId).IsUnique().HasFilter("\"EstornoDeId\" IS NOT NULL");
            entity.HasIndex(e => new { e.ContaId, e.OcorridoEm });
        });

        modelBuilder.Entity<PosicaoConta>(entity =>
        {
            entity.ToTable("posicoes_contas");
            entity.HasKey(e => e.ContaId);
            entity.Property(e => e.ContaId).ValueGeneratedNever();
            entity.Property(e => e.Saldo).HasPrecision(18, 2).IsRequired();
            entity.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        });

        modelBuilder.Entity<OutboxEvento>(entity =>
        {
            entity.ToTable("outbox_eventos");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Payload).IsRequired();
            entity.HasIndex(e => e.CriadoEm);
        });
    }
}
