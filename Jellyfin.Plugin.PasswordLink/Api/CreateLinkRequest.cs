namespace Jellyfin.Plugin.PasswordLink.Api;

// Either UserId for an existing user, or Username to create a new user first.
public sealed record CreateLinkRequest(Guid? UserId, string? Username, int? ExpiresInDays);
