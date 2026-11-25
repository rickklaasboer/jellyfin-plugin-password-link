namespace Jellyfin.Plugin.PasswordLink.Services;

public enum PasswordLinkError
{
    UserNotFound,
    UserAlreadyExists,
    InvalidUsername,
    UserIsAdministrator,
    UserIsDisabled,
    UnsupportedAuthenticationProvider,
    InvalidExpiry,
    InvalidLink,
    PasswordTooShort,
}
