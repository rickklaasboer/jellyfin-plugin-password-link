# Password Link for Jellyfin

Send a user a one-time link to set their own password. They open it on any device, choose a password and sign in with it in every Jellyfin app.

Useful when you invite people to your server or reset a forgotten password, without sending passwords around.

## Install

1. In the Jellyfin dashboard, go to **Plugins > Manage Repositories** and add:

   ```
   https://raw.githubusercontent.com/rickklaasboer/jellyfin-plugin-password-link/main/manifest.json
   ```

2. Install **Password Link** from the catalog and restart Jellyfin.

Requires Jellyfin 12.1 or newer.

## Use

Open **Password Link** in the dashboard menu, pick a user and create a link. Send the link to that user. The page shows their username and asks for a new password. The public page is available in English and Dutch.

Creating a new link for a user replaces their previous one. Active links can be revoked from the same page. The default validity (7 days) and the minimum password length (8) are under **Settings**.

## Security

- The token is in the URL fragment (`#...`), so it never reaches the server in a request line or shows up in proxy access logs.
- Only a SHA-256 hash of the token is stored, in `<data>/password-link/links.json`.
- A link works once and expires. Unknown, used and expired links return the same response.
- Setting a password signs out the user's existing sessions, like Jellyfin's own password change.
- Links cannot be created for administrators, disabled users or users who sign in through another provider (LDAP, SSO).

## Build

```
dotnet test
dotnet publish Jellyfin.Plugin.PasswordLink -c Release -o publish
```

Copy `publish/Jellyfin.Plugin.PasswordLink.dll` to `<config>/data/plugins/PasswordLink/` and restart Jellyfin.

Releases are built by pushing a tag like `v1.0.0.0`. The workflow attaches the zip to a GitHub release and adds it to `manifest.json`.

## License

GPL-3.0
