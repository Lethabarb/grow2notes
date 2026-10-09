# Break-glass and operator commands

The operator commands are part of the app's own binary (design.md §7.4). The developer runs them from the web app's
SSH console, signed in to Azure with their own account; a command runs with the app's settings and its identity, and
writes its audit event. This file says how to open the console, and for each command when to run it, what to have
ready, how to run it and what it does.

## The SSH console

The console is a shell inside the web app's running container, so a command run there reaches the database and Key
Vault as the app does.

1. In the Azure portal, signed in with your own account, open the web app: `app-grow2notes-test` in
   `rg-grow2notes-test-ause`, or `app-grow2notes-prod` in `rg-grow2notes-prod-ause`.
2. Under **Development Tools**, select **SSH**, then **Go**. A new tab opens the Kudu site's console,
   `https://app-grow2notes-<env>.scm.azurewebsites.net/webssh/host`, signed in with the same account. Kudu takes that
   Microsoft Entra sign-in from an account whose role on the app includes `Microsoft.Web/sites/publish/Action`, such as
   Owner, Contributor or Website Contributor, so this works with the Kudu site's basic authentication off
   (`infra/modules/app.bicep`). Never turn basic authentication on to get in.
3. Check by name, printing no value, that the shell has the settings the commands read:

   ```shell
   for name in ConnectionStrings__Grow2Notes ManagedIdentity__ClientId DataProtection__KeyVaultKeyUri App__Origin \
     IDENTITY_ENDPOINT IDENTITY_HEADER; do
     if printenv "$name" > /dev/null; then echo "$name: set"; else echo "$name: MISSING"; fi
   done
   ```

   All six must say `set`. The first four are the app settings that `infra/main.bicep` gives the app: the database's
   connection string, the client ID of the app's identity, the Key Vault key that wraps the key ring that setup tokens
   are made with, and the address the app is reached at, which setup links start with. The last two are how App
   Service lets a process in the container sign in as the app's identity. If any says `MISSING`, stop and run nothing.
   Never print the values, with `env`, `printenv` alone or `echo`: `IDENTITY_HEADER` guards the identity's tokens.
4. Change nothing else: the shell runs as root. `/home/site/wwwroot` is the deployed `app.zip`, mounted read-only
   (A49), and the web app keeps serving while a command runs beside it.
5. When done, type `exit` and close the tab.

## admin bootstrap

Creates an organisation and its first manager's invite, and prints the manager's setup link (design.md §7.4, §8.1).
There is no sign-up screen (D23, A1), so this is how every organisation starts.

### When to run it

- **Once for each organisation.** Every run that ends with exit code 0 creates another organisation, as onboarding
  another provider would (D3), and nothing removes one. A run is never repeated to put an earlier one right: see *If
  it is refused or fails*.
- **In test,** only with made-up names and an `example.org` address (A35), such as `Sample Support Services`,
  `Alex Sample` and `alex.sample.20261012@example.org`, with a new address each time, and only when a test
  organisation is needed, since it stays for good. The first run in test is S00.04.02's after-deploy check, which
  opens the link and completes setup with it.
- **In production,** once, for the provider, as S06.05.02 says: only when F06.01's list of high and critical findings
  is empty, because the first manager's name and address are the first real personal information in production (D1),
  and with the provider's name checked by the same parent-company-name check as the rest of the app (D42).

### What to have ready

- The organisation's name, exactly as reports and exports are to print it in their headers (A1, design.md §5.3), at
  most 200 characters. No screen changes it later, so check its spelling first.
- The first manager's name, as the app is to show it on what they write and review, at most 100 characters, and
  their email address, at most 256 characters, which has no account yet: one address has one account across the whole
  app (A27).
- The app answering `200` at `/healthz/ready`, such as `https://app-grow2notes-test.azurewebsites.net/healthz/ready`,
  which shows that it reaches its database. In test the database pauses when idle and the first request wakes it,
  which can take a minute, so refresh until it answers `200`.

### Running it

Open the SSH console and check its settings (above). Then paste this block in one go: the shell reads all of it
first, then asks for the three values and runs the command from `/home/site/wwwroot`.

```shell
cd /home/site/wwwroot && {
  printf 'Organisation name: '; read -r organisation
  printf 'First manager name: '; read -r manager_name
  printf 'First manager email address: '; read -r manager_email
  dotnet Grow2Notes.Web.dll admin bootstrap --organisation "$organisation" --manager-name "$manager_name" \
    --manager-email "$manager_email"
  echo "Exit code: $?"
  unset organisation manager_name manager_email
}
```

- `read` asks for the values, so the manager's name and address are not saved in the shell's history, and an
  apostrophe or a space in a value reaches the command as typed.
- `/home/site/wwwroot` is where App Service mounts the app, and the command runs from there so that it reads the
  app's `appsettings.json`, as the web app does.
- The command builds the app with all its services and settings and runs in place of the web server (design.md
  §7.4). It takes a few seconds.

### What it writes and prints

In one transaction (design.md §5.9), so all of it or none of it, it writes:

- the organisation, with its name and the time it was created;
- the first manager's account: a Manager, Invited, which cannot sign in before setup completes, invited by no one,
  with the name as their display name, the address as their user name and email, no password, and an authenticator
  key generated now, so that no setup step before completion changes the security stamp (design.md §8.1 step 1);
- the audit event `admin.bootstrap`, with no actor, for the organisation, which holds the manager's ID and never their
  name or address (A28).

Once that has committed, it prints the setup link on standard output, and nothing else, and the block then prints
`Exit code: 0`:

```text
https://app-grow2notes-test.azurewebsites.net/setup#u=<the manager's ID>&t=<the setup token>
```

It sends no email, logs nothing and keeps no copy of the link: the link holds the token and the address is personal,
so neither goes to telemetry (design.md §9.5).

### The setup link

- **It works once, and for 7 days** (A23). The token holds the manager's security stamp, which completing setup
  changes (design.md §8.1 step 5), so it stops working then, and in any case 7 days after it was printed.
- **It goes to the first manager directly,** and to no one else: whoever opens it can set up the account. In
  production, S06.05.02 settles how it reaches them. In test the address is made up and receives nothing, so the
  operator opens it.
- **It is kept nowhere else:** not in notes, a ticket, a chat, an email to anyone else, this repository or `ops/`. The
  console's screen is the only place it is shown, so close the tab once the link has gone to the manager. The token
  is in the link's fragment, after `#`, which a browser never sends to a server, so no server log holds it either
  (design.md §8.1 step 2).
- If it is lost, or 7 days pass before setup, do not run `admin bootstrap` again: the address already has an account,
  so the run is refused. `admin reset-signin` (S00.03.06) prints a new link.

### If it is refused or fails

- **Exit code 1: refused.** It writes one line on standard error, `Refused: <why> Nothing was written.`, followed by
  the usage line when an argument was at fault. Put right what it names and run it again. Before writing anything, it
  refuses an unknown command or option, an option that is missing, empty, given twice or too long, and an `App__Origin`
  that is missing or not an absolute address, which the refusal calls `App:Origin`. Inside the transaction, which then
  rolls back, it refuses an address that already has an account (A27) or that is not an email address.
- **A refusal wrote nothing, with one exception: a first run with a new address that is refused as already having an
  account.** That happens only when a brief database failure hid the commit of the run's first attempt, and the
  retry was refused on the address that attempt had saved. The organisation and the Invited manager exist, but no
  link was printed. Do not run it again with another address or organisation name, which would add a second
  organisation: the manager's link comes from `admin reset-signin` (S00.03.06).
- **Any other exit code: a failure the command did not expect,** such as a database that stayed unreachable through
  its retries; .NET writes the exception on standard error. Check `/healthz/ready` as above, then run it again with
  the same three values. If the failed run had committed, the address refuses this one, which is the exception above;
  if not, it runs as usual.
