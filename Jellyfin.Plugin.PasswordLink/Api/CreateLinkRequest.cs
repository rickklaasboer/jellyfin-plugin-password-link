namespace Jellyfin.Plugin.PasswordLink.Api;

public sealed record CreateLinkRequest(Guid UserId, int? ExpiresInDays);
