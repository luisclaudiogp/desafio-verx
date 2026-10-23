using Lancamentos.Domain.Entities;
using Lancamentos.Infrastructure.Outbox;
using Lancamentos.UnitTests.Domain;
using Verx.SharedKernel;

namespace Lancamentos.UnitTests.Infrastructure;

public class LancamentoEventMapperTests
{
    [Fact]
    public void ParaEvento_MapeiaTodosOsCampos()
    {
        var lancamento = Lancamento.Registrar(
            LancamentoTests.Dados(TipoMovimento.Debito, 12.34m), new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));

        var evento = LancamentoEventMapper.ParaEvento(lancamento);

        Assert.Equal(lancamento.Id, evento.LancamentoId);
        Assert.Equal(lancamento.ContaId, evento.ContaId);
        Assert.Equal(TipoMovimento.Debito, evento.Tipo);
        Assert.Equal(12.34m, evento.Valor);
        Assert.Equal(lancamento.OcorridoEm, evento.OcorridoEm);
        Assert.Equal(lancamento.RegistradoEm, evento.RegistradoEm);
    }

    [Fact]
    public void ParaEvento_LancamentoNulo_Lanca() =>
        Assert.Throws<ArgumentNullException>(() => LancamentoEventMapper.ParaEvento(null!));

    [Fact]
    public void OutboxOptions_TemPadroesSeguros()
    {
        var o = new OutboxOptions();

        Assert.True(o.BatchSize > 0);
        Assert.True(o.Paralelismo >= 1);
        Assert.True(o.BackoffMaximoMs >= o.PollOciosoMs);
    }
}
