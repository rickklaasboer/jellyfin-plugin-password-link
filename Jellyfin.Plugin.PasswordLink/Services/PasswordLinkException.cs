namespace Jellyfin.Plugin.PasswordLink.Services;

public class PasswordLinkException : Exception
{
    public PasswordLinkException(PasswordLinkError error)
        : base(error.ToString())
    {
        Error = error;
    }

    public PasswordLinkError Error { get; }
}
