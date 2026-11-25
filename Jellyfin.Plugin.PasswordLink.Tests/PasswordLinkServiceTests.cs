using Jellyfin.Data;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.PasswordLink.Configuration;
using Jellyfin.Plugin.PasswordLink.Services;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.PasswordLink.Tests;

public sealed class PasswordLinkServiceTests : IDisposable
{
    private readonly string _storePath = Path.Combine(Path.GetTempPath(), $"password-link-{Guid.NewGuid()}", "links.json");
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 25, 12, 0, 0, TimeSpan.Zero));
    private readonly Mock<IUserManager> _userManager = new();
    private readonly Mock<ISessionManager> _sessionManager = new();
    private readonly User _user = new("friend", PasswordLinkService.DefaultAuthenticationProvider, "default");

    public PasswordLinkServiceTests()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_storePath)!);
        _userManager.Setup(m => m.GetUserById(_user.Id)).Returns(_user);
    }

    public void Dispose() => Directory.Delete(Path.GetDirectoryName(_storePath)!, recursive: true);

    [Fact]
    public async Task RedeemAsync_LinkExpired_ThrowsInvalidLink()
    {
        using var service = CreateService();
        var link = await service.CreateAsync(_user.Id, 7);

        _time.Advance(TimeSpan.FromDays(7));

        var e = await Assert.ThrowsAsync<PasswordLinkException>(() => service.RedeemAsync(link.Token, "newpass456"));
        Assert.Equal(PasswordLinkError.InvalidLink, e.Error);
        _userManager.Verify(m => m.ChangePassword(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RedeemAsync_JustBeforeExpiry_ChangesPassword()
    {
        using var service = CreateService();
        var link = await service.CreateAsync(_user.Id, 7);

        _time.Advance(TimeSpan.FromDays(7) - TimeSpan.FromSeconds(1));
        await service.RedeemAsync(link.Token, "newpass456");

        _userManager.Verify(m => m.ChangePassword(_user.Id, "newpass456"), Times.Once);
    }

    [Fact]
    public async Task RedeemAsync_PasswordChangeFails_KeepsLinkUsable()
    {
        using var service = CreateService();
        var link = await service.CreateAsync(_user.Id, 7);
        _userManager.Setup(m => m.ChangePassword(_user.Id, It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RedeemAsync(link.Token, "newpass456"));

        Assert.Equal("friend", await service.GetUsernameAsync(link.Token));
        _sessionManager.Verify(m => m.RevokeUserTokens(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RedeemAsync_UserBecameAdministrator_ThrowsInvalidLink()
    {
        using var service = CreateService();
        var link = await service.CreateAsync(_user.Id, 7);

        _user.SetPermission(PermissionKind.IsAdministrator, true);

        var e = await Assert.ThrowsAsync<PasswordLinkException>(() => service.RedeemAsync(link.Token, "newpass456"));
        Assert.Equal(PasswordLinkError.InvalidLink, e.Error);
    }

    [Fact]
    public async Task CreateAsync_ServiceRestarted_LinkStillUsable()
    {
        CreatedPasswordLink link;
        using (var service = CreateService())
        {
            link = await service.CreateAsync(_user.Id, 7);
        }

        using var restarted = CreateService();

        Assert.Equal("friend", await restarted.GetUsernameAsync(link.Token));
    }

    [Fact]
    public async Task CreateAsync_Always_StoresOnlyTokenHash()
    {
        using var service = CreateService();
        var link = await service.CreateAsync(_user.Id, 7);

        var stored = await File.ReadAllTextAsync(_storePath);

        Assert.DoesNotContain(link.Token, stored, StringComparison.Ordinal);
        Assert.Contains(PasswordLinkService.HashToken(link.Token), stored, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(PasswordLinkService.MaxExpiryDays + 1)]
    public async Task CreateAsync_ExpiryOutOfRange_ThrowsInvalidExpiry(int days)
    {
        using var service = CreateService();

        var e = await Assert.ThrowsAsync<PasswordLinkException>(() => service.CreateAsync(_user.Id, days));

        Assert.Equal(PasswordLinkError.InvalidExpiry, e.Error);
    }

    [Fact]
    public async Task CreateAsync_NoExpiryGiven_UsesConfiguredDefault()
    {
        using var service = CreateService(new PluginConfiguration { DefaultExpiryDays = 3 });

        var link = await service.CreateAsync(_user.Id, null);

        Assert.Equal(_time.GetUtcNow().AddDays(3), link.ExpiresAt);
    }

    [Fact]
    public async Task CreateAsync_UserWithOtherAuthenticationProvider_ThrowsUnsupportedProvider()
    {
        _user.AuthenticationProviderId = "Jellyfin.Plugin.Ldap";
        using var service = CreateService();

        var e = await Assert.ThrowsAsync<PasswordLinkException>(() => service.CreateAsync(_user.Id, 7));

        Assert.Equal(PasswordLinkError.UnsupportedAuthenticationProvider, e.Error);
    }

    private PasswordLinkService CreateService(PluginConfiguration? configuration = null)
    {
        var config = configuration ?? new PluginConfiguration();
        return new PasswordLinkService(
            new PasswordLinkStore(_storePath),
            _userManager.Object,
            _sessionManager.Object,
            _time,
            () => config,
            NullLogger<PasswordLinkService>.Instance);
    }
}
