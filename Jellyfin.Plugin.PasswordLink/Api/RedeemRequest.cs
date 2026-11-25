namespace Jellyfin.Plugin.PasswordLink.Api;

public sealed record RedeemRequest(string Token, string Password);
