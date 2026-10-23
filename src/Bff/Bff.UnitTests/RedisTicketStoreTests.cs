using System.Security.Claims;
using Bff.API.Sessao;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Bff.UnitTests;

public class RedisTicketStoreTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly IDistributedCache _cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
    private readonly RedisTicketStore _store;

    public RedisTicketStoreTests()
    {
        _store = new RedisTicketStore(_cache, TimeProvider.System, new SessaoOptions { InatividadeMinutos = 30 });
    }

    private static AuthenticationTicket Ticket(string usuario = "u1", DateTimeOffset? expira = null)
    {
        var identidade = new ClaimsIdentity(
            [new Claim("sub", usuario), new Claim("conta_id", "11111111-1111-1111-1111-111111111111")], "Cookies");
        var propriedades = new AuthenticationProperties { ExpiresUtc = expira };
        propriedades.StoreTokens([new AuthenticationToken { Name = "access_token", Value = "abc" }]);
        return new AuthenticationTicket(new ClaimsPrincipal(identidade), propriedades, "Cookies");
    }

    [Fact]
    public async Task Armazenar_DevolveChaveOpacaERecuperaOMesmoBilhete()
    {
        var chave = await _store.StoreAsync(Ticket());

        var recuperado = await _store.RetrieveAsync(chave);

        Assert.Matches("^[0-9a-f]{32}$", chave);
        Assert.NotNull(recuperado);
        Assert.Equal("u1", recuperado.Principal.FindFirstValue("sub"));
        Assert.Equal("11111111-1111-1111-1111-111111111111", recuperado.Principal.FindFirstValue("conta_id"));
        Assert.Equal("abc", recuperado.Properties.GetTokenValue("access_token"));
    }

    [Fact]
    public async Task ChavesSaoUnicasPorSessao()
    {
        var a = await _store.StoreAsync(Ticket());
        var b = await _store.StoreAsync(Ticket());

        Assert.NotEqual(a, b);
    }

    [Fact]
    public async Task ChaveInexistente_RetornaNulo() =>
        Assert.Null(await _store.RetrieveAsync("nao-existe"));

    [Fact]
    public async Task Renovar_SubstituiOConteudoDaMesmaChave()
    {
        var chave = await _store.StoreAsync(Ticket("antigo"));

        await _store.RenewAsync(chave, Ticket("novo"));

        Assert.Equal("novo", (await _store.RetrieveAsync(chave))!.Principal.FindFirstValue("sub"));
    }

    [Fact]
    public async Task Remover_ApagaASessao()
    {
        var chave = await _store.StoreAsync(Ticket());

        await _store.RemoveAsync(chave);

        Assert.Null(await _store.RetrieveAsync(chave));
    }

    [Fact]
    public async Task Armazenar_SemExpiracaoNoBilhete_UsaInatividadeConfigurada()
    {
        var cache = new Moq.Mock<IDistributedCache>();
        DistributedCacheEntryOptions? opcoes = null;
        cache.Setup(c => c.SetAsync(Moq.It.IsAny<string>(), Moq.It.IsAny<byte[]>(), Moq.It.IsAny<DistributedCacheEntryOptions>(), Moq.It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>((_, _, o, _) => opcoes = o)
            .Returns(Task.CompletedTask);
        var store = new RedisTicketStore(cache.Object, new FixedTimeProvider(Agora), new SessaoOptions { InatividadeMinutos = 30 });

        await store.StoreAsync(Ticket());

        Assert.Equal(Agora.AddMinutes(30), opcoes!.AbsoluteExpiration);
    }

    [Fact]
    public async Task Armazenar_ComExpiracaoNoBilhete_RespeitaAExpiracaoDoBilhete()
    {
        var cache = new Moq.Mock<IDistributedCache>();
        DistributedCacheEntryOptions? opcoes = null;
        cache.Setup(c => c.SetAsync(Moq.It.IsAny<string>(), Moq.It.IsAny<byte[]>(), Moq.It.IsAny<DistributedCacheEntryOptions>(), Moq.It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>((_, _, o, _) => opcoes = o)
            .Returns(Task.CompletedTask);
        var store = new RedisTicketStore(cache.Object, new FixedTimeProvider(Agora), new SessaoOptions());

        await store.StoreAsync(Ticket(expira: Agora.AddHours(2)));

        Assert.Equal(Agora.AddHours(2), opcoes!.AbsoluteExpiration);
    }
}
