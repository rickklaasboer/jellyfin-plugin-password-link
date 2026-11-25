namespace Jellyfin.Plugin.PasswordLink.Api;

public sealed record InspectResponse(string Username, int MinPasswordLength);
