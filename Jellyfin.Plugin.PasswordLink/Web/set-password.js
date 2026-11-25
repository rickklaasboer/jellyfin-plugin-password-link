const STRINGS = {
    en: {
        title: 'Choose a password',
        usernameLabel: 'Username',
        passwordLabel: 'New password',
        show: 'Show',
        hide: 'Hide',
        hint: 'At least {n} characters',
        submit: 'Save password',
        failed: 'Saving failed. Try again.',
        doneTitle: 'Password saved',
        doneText: 'Sign in as {username} with your new password.',
        signIn: 'Sign in',
        invalidTitle: 'This link no longer works',
        invalidText: 'Ask for a new link.',
        unreachableTitle: 'Server unreachable',
        unreachableText: 'Try again in a moment.',
    },
    nl: {
        title: 'Kies een wachtwoord',
        usernameLabel: 'Gebruikersnaam',
        passwordLabel: 'Nieuw wachtwoord',
        show: 'Toon',
        hide: 'Verberg',
        hint: 'Minimaal {n} tekens',
        submit: 'Wachtwoord opslaan',
        failed: 'Opslaan is mislukt. Probeer het opnieuw.',
        doneTitle: 'Wachtwoord opgeslagen',
        doneText: 'Log in als {username} met je nieuwe wachtwoord.',
        signIn: 'Inloggen',
        invalidTitle: 'Deze link werkt niet meer',
        invalidText: 'Vraag om een nieuwe link.',
        unreachableTitle: 'Server niet bereikbaar',
        unreachableText: 'Probeer het zo nog eens.',
    },
};

const language = navigator.languages
    .map((tag) => tag.slice(0, 2).toLowerCase())
    .find((code) => code in STRINGS) ?? 'en';
const text = STRINGS[language];

const $ = (id) => document.getElementById(id);
const format = (template, values) => template.replace(/\{(\w+)\}/g, (_, key) => values[key]);

// The token lives in the fragment, so it is not in any request URL or access log. Drop it from
// the address bar to keep it out of history and bookmarks, and keep it in this tab's
// sessionStorage so a reload still works until the password is saved.
const TOKEN_KEY = 'password-link-token';
if (location.hash.length > 1) {
    sessionStorage.setItem(TOKEN_KEY, decodeURIComponent(location.hash.slice(1)));
    history.replaceState(null, '', location.pathname + location.search);
}
const token = sessionStorage.getItem(TOKEN_KEY);

function show(view) {
    for (const id of ['loading', 'form-view', 'done-view', 'invalid-view']) {
        $(id).hidden = id !== view;
    }
}

function showProblem(titleKey, textKey) {
    const view = $('invalid-view');
    view.querySelector('h1').textContent = text[titleKey];
    view.querySelector('p').textContent = text[textKey];
    show('invalid-view');
}

async function post(path, body) {
    return fetch(path, {
        method: 'POST',
        headers: {'Content-Type': 'application/json'},
        body: JSON.stringify(body),
    });
}

async function loadServerName() {
    const response = await fetch('../System/Info/Public');
    if (!response.ok) return;

    const info = await response.json();
    $('server').textContent = info.ServerName;
    document.title = info.ServerName;
}

async function inspect() {
    if (!token) return showProblem('invalidTitle', 'invalidText');

    const response = await post('Inspect', {Token: token});
    if (response.status === 404) return showProblem('invalidTitle', 'invalidText');
    if (!response.ok) throw new Error(`Inspect failed with HTTP ${response.status}`);

    const {Username: username, MinPasswordLength: minLength} = await response.json();
    $('username').textContent = username;
    $('username-field').value = username;
    $('hint').textContent = format(text.hint, {n: minLength});
    const [before, after] = text.doneText.split('{username}');
    const name = document.createElement('strong');
    name.textContent = username;
    $('done-text').replaceChildren(before, name, after);

    const password = $('password');
    password.minLength = minLength;

    $('toggle').addEventListener('click', () => {
        const visible = password.type === 'text';
        password.type = visible ? 'password' : 'text';
        $('toggle').textContent = visible ? text.show : text.hide;
    });

    $('form').addEventListener('submit', (event) => {
        event.preventDefault();
        redeem(password.value, minLength);
    });

    show('form-view');
    password.focus();
}

async function redeem(password, minLength) {
    const error = $('error');
    const tooShort = password.length < minLength;
    $('hint').classList.toggle('invalid', tooShort);
    if (tooShort) return;

    error.textContent = '';
    $('submit').disabled = true;
    try {
        const response = await post('Redeem', {Token: token, Password: password});
        if (response.status === 404) return showProblem('invalidTitle', 'invalidText');
        if (!response.ok) throw new Error(`Redeem failed with HTTP ${response.status}`);

        sessionStorage.removeItem(TOKEN_KEY);
        show('done-view');
    } catch (e) {
        console.error(e);
        error.textContent = text.failed;
    } finally {
        $('submit').disabled = false;
    }
}

document.documentElement.lang = language;
for (const element of document.querySelectorAll('[data-text]')) {
    element.textContent = text[element.dataset.text];
}

loadServerName().catch((e) => console.error('Could not load server name', e));
inspect().catch((e) => {
    console.error(e);
    showProblem('unreachableTitle', 'unreachableText');
});
