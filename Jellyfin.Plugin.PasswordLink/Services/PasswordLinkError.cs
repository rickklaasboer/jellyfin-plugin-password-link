namespace Jellyfin.Plugin.PasswordLink.Services;

public enum PasswordLinkError
{
    UserNotFound,
    UserIsAdministrator,
    UserIsDisabled,
    UnsupportedAuthenticationProvider,
    InvalidExpiry,
    InvalidLink,
    PasswordTooShort,
}
