namespace Jellyfin.Plugin.PasswordLink.Services;

public sealed record PasswordLinkSummary(
    Guid Id,
    Guid UserId,
    string Username,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);
