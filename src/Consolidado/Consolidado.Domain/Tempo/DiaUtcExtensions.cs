namespace Consolidado.Domain.Tempo;

public static class DiaUtcExtensions
{
    public static DateOnly DiaUtc(this DateTimeOffset instante) =>
        DateOnly.FromDateTime(instante.UtcDateTime);

    public static DateTimeOffset InicioDoDiaUtc(this DateOnly dia) =>
        new(dia.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
