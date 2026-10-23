using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Distributed;

namespace Bff.API.Sessao;

public sealed class RedisTicketStore(IDistributedCache cache, TimeProvider timeProvider, SessaoOptions options) : ITicketStore
{
    private const string Prefixo = "sessao:";

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var chave = Guid.NewGuid().ToString("N");
        await RenewAsync(chave, ticket);
        return chave;
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var expira = ticket.Properties.ExpiresUtc ?? timeProvider.GetUtcNow().AddMinutes(options.InatividadeMinutos);
        return cache.SetAsync(
            Prefixo + key,
            TicketSerializer.Default.Serialize(ticket),
            new DistributedCacheEntryOptions { AbsoluteExpiration = expira });
    }

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        var bytes = await cache.GetAsync(Prefixo + key);
        return bytes is null ? null : TicketSerializer.Default.Deserialize(bytes);
    }

    public Task RemoveAsync(string key) => cache.RemoveAsync(Prefixo + key);
}
