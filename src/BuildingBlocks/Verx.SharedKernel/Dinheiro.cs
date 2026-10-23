namespace Verx.SharedKernel;

public sealed record Dinheiro
{
    public const decimal Maximo = 999_999_999_999.99m;

    public decimal Valor { get; }

    private Dinheiro(decimal valor) => Valor = valor;

    public static Dinheiro Criar(decimal valor)
    {
        if (valor <= 0)
            throw new DomainException("O valor deve ser maior que zero.");
        if (valor > Maximo)
            throw new DomainException("O valor excede o limite permitido.");
        if (decimal.Round(valor, 2) != valor)
            throw new DomainException("O valor deve ter no maximo duas casas decimais.");

        return new Dinheiro(valor);
    }

    public override string ToString() => Valor.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
}
