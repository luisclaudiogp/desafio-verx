using Consolidado.Domain.Tempo;
using Verx.SharedKernel;

namespace Consolidado.Domain.Snapshots;

public static class PoliticaDeSnapshot
{
    public const int MargemMinimaDias = 1;

    public static DateOnly DataLimite(DateTimeOffset agora, int margemDias)
    {
        if (margemDias < MargemMinimaDias)
            throw new DomainException($"A margem do snapshot deve ser de pelo menos {MargemMinimaDias} dia.");

        return agora.DiaUtc().AddDays(-margemDias);
    }
}
