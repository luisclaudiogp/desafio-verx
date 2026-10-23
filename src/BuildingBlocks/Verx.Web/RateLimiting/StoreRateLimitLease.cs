using System.Threading.RateLimiting;

namespace Verx.Web.RateLimiting;

internal sealed class StoreRateLimitLease(ResultadoTokenBucket resultado) : RateLimitLease
{
    public override bool IsAcquired => resultado.Permitido;

    public override IEnumerable<string> MetadataNames =>
        resultado.RetryAfter is null ? [] : [MetadataName.RetryAfter.Name];

    public override bool TryGetMetadata(string metadataName, out object? metadata)
    {
        if (metadataName == MetadataName.RetryAfter.Name && resultado.RetryAfter is { } espera)
        {
            metadata = espera;
            return true;
        }

        metadata = null;
        return false;
    }
}
