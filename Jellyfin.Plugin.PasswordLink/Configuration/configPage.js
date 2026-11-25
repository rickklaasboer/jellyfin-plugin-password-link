const PLUGIN_ID = '2ae0d2b9-67aa-486d-a157-3d0464f6be86';
const DEFAULT_AUTH_PROVIDER = 'Jellyfin.Server.Implementations.Users.DefaultAuthenticationProvider';

const ERRORS = {
    UserNotFound: 'This user no longer exists.',
    UserIsAdministrator: 'Links cannot be created for administrators.',
    UserIsDisabled: 'This user is disabled. Enable the account first.',
    UnsupportedAuthenticationProvider: 'This user signs in through another provider.',
    InvalidExpiry: 'Choose a validity between 1 and 365 days.',
};

const dateFormat = new Intl.DateTimeFormat(undefined, {dateStyle: 'medium', timeStyle: 'short'});

function serverAddress() {
    return window.ApiClient.serverAddress().replace(/\/+$/, '');
}

async function api(method, path, body) {
    const response = await fetch(`${serverAddress()}/${path}`, {
        method,
        headers: {
            Authorization: `MediaBrowser Token="${window.ApiClient.accessToken()}"`,
            ...(body === undefined ? {} : {'Content-Type': 'application/json'}),
        },
        body: body === undefined ? undefined : JSON.stringify(body),
    });

    if (!response.ok) {
        const error = await response.json().catch(() => ({}));
        throw new Error(ERRORS[error.Error] ?? `Request failed with HTTP ${response.status}.`);
    }

    return response.status === 204 ? null : response.json();
}

function isEligible(user) {
    return !user.Policy.IsAdministrator
        && !user.Policy.IsDisabled
        && user.Policy.AuthenticationProviderId === DEFAULT_AUTH_PROVIDER;
}

async function loadUsers(root) {
    const users = (await window.ApiClient.getUsers()).filter(isEligible);
    const select = root.querySelector('#pl-user');
    select.replaceChildren(...users.map((user) => new Option(user.Name, user.Id)));
}

async function loadLinks(root) {
    const links = await api('GET', 'PasswordLink/Links');
    const rows = links.map((link) => {
        const row = document.createElement('tr');
        for (const value of [link.Username, dateFormat.format(new Date(link.CreatedAt)), dateFormat.format(new Date(link.ExpiresAt))]) {
            row.insertCell().textContent = value;
        }

        const revoke = document.createElement('button');
        revoke.type = 'button';
        revoke.className = 'raised emby-button pl-button';
        revoke.textContent = 'Revoke';
        revoke.addEventListener('click', () => run(async () => {
            await api('DELETE', `PasswordLink/Links/${link.Id}`);
            await loadLinks(root);
        }));
        row.insertCell().append(revoke);
        return row;
    });

    root.querySelector('#pl-links').replaceChildren(...rows);
    root.querySelector('.pl-table').hidden = links.length === 0;
    root.querySelector('#pl-empty').hidden = links.length > 0;
}

async function loadConfiguration(root) {
    const config = await window.ApiClient.getPluginConfiguration(PLUGIN_ID);
    root.querySelector('#pl-days').value = config.DefaultExpiryDays;
    root.querySelector('#pl-default-days').value = config.DefaultExpiryDays;
    root.querySelector('#pl-min-length').value = config.MinPasswordLength;
}

async function createLink(root) {
    const select = root.querySelector('#pl-user');
    const link = await api('POST', 'PasswordLink/Links', {
        UserId: select.value,
        ExpiresInDays: Number(root.querySelector('#pl-days').value),
    });

    // The token goes in the fragment so it never reaches a server or proxy access log.
    root.querySelector('#pl-url').value = `${serverAddress()}/PasswordLink/Set#${link.Token}`;
    root.querySelector('#pl-result-label').textContent = `Link for ${select.selectedOptions[0].text}`;
    root.querySelector('#pl-result-expiry').textContent = `Valid until ${dateFormat.format(new Date(link.ExpiresAt))}.`;
    root.querySelector('#pl-copy').textContent = 'Copy';
    root.querySelector('#pl-result').hidden = false;

    await loadLinks(root);
}

async function saveConfiguration(root) {
    const config = await window.ApiClient.getPluginConfiguration(PLUGIN_ID);
    config.DefaultExpiryDays = Number(root.querySelector('#pl-default-days').value);
    config.MinPasswordLength = Number(root.querySelector('#pl-min-length').value);

    const result = await window.ApiClient.updatePluginConfiguration(PLUGIN_ID, config);
    window.Dashboard.processPluginConfigurationUpdateResult(result);
}

async function run(action) {
    window.Dashboard.showLoadingMsg();
    try {
        await action();
    } catch (e) {
        console.error('[PasswordLink]', e);
        window.Dashboard.alert(e.message);
    } finally {
        window.Dashboard.hideLoadingMsg();
    }
}

function bind(root) {
    root.dataset.bound = 'true';

    root.querySelector('#pl-create-form').addEventListener('submit', (event) => {
        event.preventDefault();
        run(() => createLink(root));
    });

    root.querySelector('#pl-settings-form').addEventListener('submit', (event) => {
        event.preventDefault();
        run(() => saveConfiguration(root));
    });

    root.querySelector('#pl-copy').addEventListener('click', () => run(async () => {
        await navigator.clipboard.writeText(root.querySelector('#pl-url').value);
        root.querySelector('#pl-copy').textContent = 'Copied';
    }));

    const refresh = () => run(() => Promise.all([loadUsers(root), loadLinks(root), loadConfiguration(root)]));
    root.closest('.page').addEventListener('pageshow', refresh);
    refresh();
}

function bindIfNew() {
    const root = document.getElementById('password-link-root');
    if (root && root.dataset.bound !== 'true') bind(root);
}

bindIfNew();

// A module runs once per document, but the dashboard re-injects this page's HTML after it
// evicts the view. Watch for a fresh, unbound copy and bind that one too.
new MutationObserver(bindIfNew).observe(document.body, {childList: true, subtree: true});
