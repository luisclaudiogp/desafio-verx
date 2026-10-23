using Verx.SharedKernel;

namespace Lancamentos.Domain.Entities;

public sealed class Lancamento
{
    public const int TamanhoMaximoChaveIdempotencia = 100;

    private static readonly TimeSpan ToleranciaRelogio = TimeSpan.FromMinutes(5);

    public Guid Id { get; private set; }
    public Guid ContaId { get; private set; }
    public TipoMovimento Tipo { get; private set; }
    public Dinheiro Valor { get; private set; } = null!;
    public DateTimeOffset OcorridoEm { get; private set; }
    public DateTimeOffset RegistradoEm { get; private set; }
    public string RegistradoPor { get; private set; } = string.Empty;
    public string ChaveIdempotencia { get; private set; } = string.Empty;
    public string HashRequisicao { get; private set; } = string.Empty;
    public Guid? EstornoDeId { get; private set; }

    private Lancamento() { }

    public static Lancamento Registrar(NovoLancamento dados, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(dados);

        if (dados.ContaId == Guid.Empty)
            throw new DomainException("A conta e obrigatoria.");
        if (!Enum.IsDefined(dados.Tipo))
            throw new DomainException("Tipo de lancamento invalido.");
        ValidarChave(dados.ChaveIdempotencia);

        var ocorrencia = dados.OcorridoEm ?? agora;
        if (ocorrencia > agora + ToleranciaRelogio)
            throw new DomainException("A data de ocorrencia nao pode estar no futuro.");

        return new Lancamento
        {
            Id = Guid.NewGuid(),
            ContaId = dados.ContaId,
            Tipo = dados.Tipo,
            Valor = Dinheiro.Criar(dados.Valor),
            OcorridoEm = ocorrencia.ToUniversalTime(),
            RegistradoEm = agora.ToUniversalTime(),
            RegistradoPor = dados.RegistradoPor,
            ChaveIdempotencia = dados.ChaveIdempotencia,
            HashRequisicao = dados.HashRequisicao
        };
    }

    public Lancamento Estornar(string chaveIdempotencia, string registradoPor, DateTimeOffset agora)
    {
        if (EstornoDeId is not null)
            throw new DomainException("Um estorno nao pode ser estornado.");
        ValidarChave(chaveIdempotencia);

        return new Lancamento
        {
            Id = Guid.NewGuid(),
            ContaId = ContaId,
            Tipo = Tipo == TipoMovimento.Credito ? TipoMovimento.Debito : TipoMovimento.Credito,
            Valor = Valor,
            OcorridoEm = agora.ToUniversalTime(),
            RegistradoEm = agora.ToUniversalTime(),
            RegistradoPor = registradoPor,
            ChaveIdempotencia = chaveIdempotencia,
            HashRequisicao = HashDaRequisicao.DoEstorno(Id),
            EstornoDeId = Id
        };
    }

    public bool CorrespondeAoHash(string hash) =>
        string.Equals(HashRequisicao, hash, StringComparison.Ordinal);

    private static void ValidarChave(string chave)
    {
        if (string.IsNullOrWhiteSpace(chave) || chave.Length > TamanhoMaximoChaveIdempotencia)
            throw new DomainException(
                $"A chave de idempotencia e obrigatoria e deve ter ate {TamanhoMaximoChaveIdempotencia} caracteres.");
    }
}
