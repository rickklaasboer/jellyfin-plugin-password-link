namespace Jellyfin.Plugin.PasswordLink.Services;

public sealed record CreatedPasswordLink(Guid Id, string Token, DateTimeOffset ExpiresAt);
