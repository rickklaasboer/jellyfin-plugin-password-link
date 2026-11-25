using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Jellyfin.Data;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.PasswordLink.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PasswordLink.Services;

public sealed class PasswordLinkService : IDisposable
{
    // Users from LDAP, SSO and similar providers keep their password elsewhere.
    internal const string DefaultAuthenticationProvider =
        "Jellyfin.Server.Implementations.Users.DefaultAuthenticationProvider";

    internal const int MaxExpiryDays = 365;

    private const int TokenBytes = 32;

    private readonly PasswordLinkStore _store;
    private readonly IUserManager _userManager;
    private readonly ISessionManager _sessionManager;
    private readonly TimeProvider _time;
    private readonly Func<PluginConfiguration> _getConfiguration;
    private readonly ILogger<PasswordLinkService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly List<PasswordLinkRecord> _links;

    public PasswordLinkService(
        PasswordLinkStore store,
        IUserManager userManager,
        ISessionManager sessionManager,
        TimeProvider time,
        Func<PluginConfiguration> getConfiguration,
        ILogger<PasswordLinkService> logger)
    {
        _store = store;
        _userManager = userManager;
        _sessionManager = sessionManager;
        _time = time;
        _getConfiguration = getConfiguration;
        _logger = logger;
        _links = store.Load();
    }

    public int MinPasswordLength => _getConfiguration().MinPasswordLength;

    public async Task<CreatedPasswordLink> CreateAsync(Guid userId, int? expiresInDays)
    {
        var days = expiresInDays ?? _getConfiguration().DefaultExpiryDays;
        if (days < 1 || days > MaxExpiryDays) throw new PasswordLinkException(PasswordLinkError.InvalidExpiry);

        var user = _userManager.GetUserById(userId)
            ?? throw new PasswordLinkException(PasswordLinkError.UserNotFound);
        EnsureEligible(user);

        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));
        var now = _time.GetUtcNow();
        var record = new PasswordLinkRecord(Guid.NewGuid(), user.Id, HashToken(token), now, now.AddDays(days));

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            // One active link per user: a new link replaces any older one.
            _links.RemoveAll(link => link.UserId == user.Id || link.ExpiresAt <= now);
            _links.Add(record);
            _store.Save(_links);
        }
        finally
        {
            _lock.Release();
        }

        _logger.LogInformation(
            "Created password link {LinkId} for user {Username}, valid until {ExpiresAt}",
            record.Id,
            user.Username,
            record.ExpiresAt);

        return new CreatedPasswordLink(record.Id, token, record.ExpiresAt);
    }

    public async Task<IReadOnlyList<PasswordLinkSummary>> ListAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            RemoveExpired();

            var summaries = new List<PasswordLinkSummary>();
            foreach (var link in _links)
            {
                var user = _userManager.GetUserById(link.UserId);
                if (user is null) continue;

                summaries.Add(new PasswordLinkSummary(link.Id, link.UserId, user.Username, link.CreatedAt, link.ExpiresAt));
            }

            return summaries;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> RevokeAsync(Guid linkId)
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_links.RemoveAll(link => link.Id == linkId) == 0) return false;

            _store.Save(_links);
            _logger.LogInformation("Revoked password link {LinkId}", linkId);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string> GetUsernameAsync(string token)
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var (_, user) = FindUsableLink(token);
            return user.Username;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RedeemAsync(string token, string password)
    {
        if (password.Length < MinPasswordLength) throw new PasswordLinkException(PasswordLinkError.PasswordTooShort);

        User user;
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            (var link, user) = FindUsableLink(token);

            await _userManager.ChangePassword(user.Id, password).ConfigureAwait(false);

            // Only consumed after the password change succeeded, so a failed change keeps the link usable.
            _links.Remove(link);
            _store.Save(_links);
        }
        finally
        {
            _lock.Release();
        }

        // Same as Jellyfin's own password change: sign out every existing session of this user.
        await _sessionManager.RevokeUserTokens(user.Id, string.Empty).ConfigureAwait(false);

        _logger.LogInformation("User {Username} set a new password with a password link", user.Username);
    }

    public void Dispose() => _lock.Dispose();

    internal static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static void EnsureEligible(User user)
    {
        if (user.HasPermission(PermissionKind.IsAdministrator))
        {
            throw new PasswordLinkException(PasswordLinkError.UserIsAdministrator);
        }

        if (user.HasPermission(PermissionKind.IsDisabled))
        {
            throw new PasswordLinkException(PasswordLinkError.UserIsDisabled);
        }

        if (user.AuthenticationProviderId != DefaultAuthenticationProvider)
        {
            throw new PasswordLinkException(PasswordLinkError.UnsupportedAuthenticationProvider);
        }
    }

    // Callers must hold _lock. Every failure is reported as InvalidLink, so the public page
    // cannot tell an unknown token from an expired one or one for an admin.
    private (PasswordLinkRecord Link, User User) FindUsableLink(string token)
    {
        if (string.IsNullOrEmpty(token)) throw new PasswordLinkException(PasswordLinkError.InvalidLink);

        var hash = HashToken(token);
        var now = _time.GetUtcNow();
        var link = _links.FirstOrDefault(l => l.TokenHash == hash && l.ExpiresAt > now)
            ?? throw new PasswordLinkException(PasswordLinkError.InvalidLink);
        var user = _userManager.GetUserById(link.UserId)
            ?? throw new PasswordLinkException(PasswordLinkError.InvalidLink);

        try
        {
            EnsureEligible(user);
        }
        catch (PasswordLinkException)
        {
            throw new PasswordLinkException(PasswordLinkError.InvalidLink);
        }

        return (link, user);
    }

    private void RemoveExpired()
    {
        var now = _time.GetUtcNow();
        if (_links.RemoveAll(link => link.ExpiresAt <= now) > 0) _store.Save(_links);
    }
}
