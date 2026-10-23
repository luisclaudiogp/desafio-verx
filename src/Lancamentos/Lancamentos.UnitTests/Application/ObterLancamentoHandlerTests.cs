using Lancamentos.Application.Queries;
using Lancamentos.Domain.Entities;
using Lancamentos.Domain.Repositories;
using Lancamentos.UnitTests.Domain;
using Moq;
using Verx.SharedKernel;

namespace Lancamentos.UnitTests.Application;

public class ObterLancamentoHandlerTests
{
    private readonly Mock<ILancamentoRepository> _repository = new();

    [Fact]
    public async Task Handle_Existente_RetornaDto()
    {
        var conta = Guid.NewGuid();
        var l = Lancamento.Registrar(LancamentoTests.Dados(valor: 5m, conta: conta), DateTimeOffset.UtcNow);
        _repository.Setup(r => r.ObterPorIdAsync(conta, l.Id, It.IsAny<CancellationToken>())).ReturnsAsync(l);

        var dto = await new ObterLancamentoHandler(_repository.Object)
            .HandleAsync(new ObterLancamentoQuery(conta, l.Id), CancellationToken.None);

        Assert.Equal(l.Id, dto.Id);
        Assert.Equal(5m, dto.Valor);
    }

    [Fact]
    public async Task Handle_Inexistente_LancaNaoEncontrado() =>
        await Assert.ThrowsAsync<NaoEncontradoException>(() =>
            new ObterLancamentoHandler(_repository.Object)
                .HandleAsync(new ObterLancamentoQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None));
}
