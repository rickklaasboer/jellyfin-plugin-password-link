namespace Jellyfin.Plugin.PasswordLink.Services;

public sealed record PasswordLinkRecord(
    Guid Id,
    Guid UserId,
    string TokenHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);
