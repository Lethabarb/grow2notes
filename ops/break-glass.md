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

Creates an organisation, with its Every note group, and its first manager's invite, and prints the manager's setup
link (design.md §7.4, §8.1). There is no sign-up screen (D23, A1), so this is how every organisation starts.

### When to run it

- **Once for each organisation.** Every run that ends with exit code 0 creates another organisation, as onboarding
  another provider would (D3), and nothing removes one. A run is never repeated to put an earlier one right: see *If
  it is refused or fails*.
- **In test,** only with made-up names and an `example.org` address (A35), such as `Sample Support Services`,
  `Alex Sample` and `alex.sample.20261012@example.org`, with a new address each time, and only when a test
  organisation is needed, since it stays for good. The first run in test is step 3 of
  [first-use-in-test.md](first-use-in-test.md), which opens the link and completes setup with it.
- **In production,** once, for the provider, as S06.05.02 and [go-live.md](go-live.md) D say: only when F06.01's list
  of high and critical findings is empty, because the first manager's name and address are the first real personal
  information in production (design.md §14 M6), only once the provider has told you, in person or on a call you made to
  a number you already hold for it, who its first manager is (A105, D74), and with the provider's name checked by hand
  for the parent company's name, as every change to the app is (D42, D72).

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
- the organisation's Every note group, the built-in group of common items that every note shows first (A43,
  design.md §5.3), named "Every note", with no items yet; it has no audit event of its own, as it is part of the
  organisation that `admin.bootstrap` records;
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
- **It goes to the first manager directly,** and to no one else: whoever opens it can set up the account. In test the
  address is made up and receives nothing, so the operator opens it. In production:
  - Send it in one text message to the first manager's own mobile, the number the provider gave when it confirmed who
    they are. Send it while you are with them in person or on a call you made to that number, so they open it in their
    phone's browser and set up there and then (A105).
  - Copy it from the console and paste it straight into the message, from your computer through your phone's messages
    (such as Phone Link, Messages for web or Messages on a Mac). Never open it, retype it or read it out.
  - Once it has gone, clear the clipboard, and its history if the computer keeps one (on Windows, Win+V, then Clear
    all).
  - Once they have finished setup, delete the message from your phone and from every computer your messages sync to.
  - Finishing setup ends the link (first bullet), so a copy missed after that cannot be used.
  - If setup is not finished then, they finish it from the same message within 7 days, and you delete your copies once
    they have.
- **It is kept nowhere else:** not in notes, a ticket, a chat, an email, this repository or `ops/`, and in production
  nowhere but the one text message above. Apart from that, the console's screen is the only place it is shown, so close
  the tab once the link has gone to the manager. The token is in the link's fragment, after `#`, which a browser never
  sends to a server, so no server log holds it either (design.md §8.1 step 2).
- If it is lost, or 7 days pass before setup, do not run `admin bootstrap` again: the address already has an account,
  so the run is refused. [admin reset-signin](#admin-reset-signin) prints a new link for the manager, and ends the
  old one.

### If it is refused or fails

- **Exit code 1: refused.** It writes one line on standard error, `Refused: <why> Nothing was written.`, followed by
  the usage line when an argument was at fault. Put right what it names and run it again. Before writing anything, it
  refuses an unknown command or option, an option that is missing, empty, given twice or too long, and an `App__Origin`
  that is missing or not an absolute address, which the refusal calls `App:Origin`. Inside the transaction, which then
  rolls back, it refuses an address that already has an account (A27) or that is not an email address
  (`InvalidEmail`), and one with a character that the app does not take in an address (`InvalidUserName`): a space, a
  tab, an invisible character that came with a paste, or a letter outside ASCII, such as an accented one. Type the
  address again rather than pasting it.
- **A refusal wrote nothing, with one exception: a first run with a new address that is refused as already having an
  account.** That happens only when a brief database failure hid the commit of the run's first attempt, and the
  retry was refused on the address that attempt had saved. The organisation, its Every note group and the Invited
  manager exist, but no link was printed. Do not run it again with another address or organisation name, which would
  add a second organisation: the manager's link comes from [admin reset-signin](#admin-reset-signin).
- **Any other exit code: a failure the command did not expect,** such as a database that stayed unreachable through
  its retries; .NET writes the exception on standard error. Check `/healthz/ready` as above, then run it again with
  the same three values. If the failed run had committed, the address refuses this one, which is the exception above;
  if not, it runs as usual.

## admin reset-signin

**Run it only once the person's organisation has confirmed who the person is** (design.md §7.4, §8.6). Someone at
the organisation who knows the person, such as one of its managers, tells you, by phone or in person, that the person
asking for a new link is the one whose address it is. Whoever opens the printed link can set up the account and read
every participant's notes (D20), so never run it on a request that came only by email or chat, or from the person
alone.

It does what a manager's Reset sign-in does in the app (design.md §4.11, §8.6), and prints the new setup link instead
of emailing it. For an Active person, it removes their password and passkeys, turns off their authenticator app's
codes, gives them a new authenticator key and makes them Invited, which ends every session of theirs on every device
on its next request; they then set up again from the link (§8.1 steps 3 to 5). For an Invited person, it gives them a
new key and link, as Resend invite does. Either way, every link made for them before stops working.

### When to run it

- **When no manager of the organisation can sign in to reset another's sign-in,** because the only Active manager, or
  every one, has lost their phone, has a new phone without their passkey, or has forgotten their password. The app
  never lets the last Active manager be reset, deactivated or made a worker (A26), and this command is the way out:
  it skips that rule. With two Active managers, which the go-live checklist asks for, one resets the other from the
  person's page in Users, and no operator is needed.
- **When an Invited manager's setup link is lost or past its 7 days, and no manager can resend it,** such as the
  first manager's link from `admin bootstrap` (*The setup link*, above).
- **Not for a Deactivated person,** whom it refuses: a manager reactivates them in the app first (D75). If no manager
  can sign in to do that, reset a manager's sign-in first.
- **In test,** only for made-up accounts (A35), as step 29 of [first-use-in-test.md](first-use-in-test.md) does.

### What to have ready

- The organisation's confirmation of who the person is (above), and a way to give the person the link directly, as
  for the first manager's link (*The setup link*, above).
- The person's email address, as their account has it, in any letter case: the app compares addresses ignoring case
  (A27), so `Sam.Taylor@Example.org` finds `sam.taylor@example.org`.
- The app answering `200` at `/healthz/ready`, as for `admin bootstrap` above.

### Running it

Open the SSH console and check its settings (above). Then paste this block in one go: it asks for the address and
runs the command from `/home/site/wwwroot`, as for `admin bootstrap`, so the address is not saved in the shell's
history.

```shell
cd /home/site/wwwroot && {
  printf 'Email address: '; read -r email
  dotnet Grow2Notes.Web.dll admin reset-signin "$email"
  echo "Exit code: $?"
  unset email
}
```

### What it writes and prints

It finds the account by its address, in whichever organisation it is, and in one transaction (design.md §5.9), so all
of it or none of it, writes:

- for an Active person: no password, no passkeys, two-factor sign-in off, a new authenticator key and so a new
  security stamp, and the status Invited; the dates of their invite and activation, their confirmed address and their
  lockout stay as they were, as a manager's reset leaves them;
- for an Invited person: a new authenticator key and so a new security stamp, and nothing else;
- the audit event `admin.signin_reset`, with no actor, for the person's organisation, about the person by their ID,
  and never their name or address (A28).

Once that has committed, it prints the new setup link on standard output, and nothing else, and the block then prints
`Exit code: 0`:

```text
https://app-grow2notes-test.azurewebsites.net/setup#u=<the person's ID>&t=<the setup token>
```

It sends no email, logs nothing and keeps no copy of the link (design.md §9.5). The link is handled as the first
manager's is (*The setup link*, above): it works once and for 7 days, it goes to the person directly and to no one
else, and it is kept nowhere else, so close the console's tab once it has gone to them. The person opens it, sets up
a passkey, or a password and an authenticator app, and is signed in.

### If it is refused or fails

- **Exit code 1: refused.** It writes one line on standard error, `Refused: <why> Nothing was written.`, followed by
  the usage line, `Usage: admin reset-signin <email>`, when the arguments were at fault. Before writing anything, it
  refuses no address (`No address was given.`), an address that is empty or spaces alone (`The address is empty.`),
  more than one argument, as when the command is typed by hand with an unquoted address that holds a space, rather
  than run from the block above (`More than one argument was given, but admin reset-signin takes one address.`), and
  an `App__Origin` that is missing or not an absolute address, which the refusal calls `App:Origin`. Inside the
  transaction, it refuses:
  - `No account has that address.`: check the address with the organisation, and type it again rather than pasting
    it, as a paste can bring an invisible character with it;
  - `The account is Deactivated, so a manager reactivates it in the app first.`: see *When to run it*;
  - `The account changed while the command ran, so run it again.`: a manager changed the account in the app at the
    same moment. Paste the block again, which reads the account as it is now.
- **Any other exit code: a failure the command did not expect,** such as a database that stayed unreachable through
  its retries; .NET writes the exception on standard error. No link was printed and the person's sign-in is as it
  was, so check `/healthz/ready` as above, then run it again with the same address.

## admin signout-all

Ends every session of every user, in every organisation in the environment, at once, to contain a breach (design.md
§7.4, §9.10 *Contain*). Each person's next request finds them signed out, and they sign in again as before: it
removes no passkey, password or authenticator app, and changes no one's status or role.

### When to run it

- **To contain a suspected breach, when a session may be in the wrong hands and it is not known whose,** such as when
  the key ring that protects the session cookies, or a copy of the database, may have been exposed. When the misused
  account is known, a manager deactivates it in the app instead, which ends that person's sessions on their next
  request (§9.10), or resets their sign-in.
- **Knowing that it signs out everyone in every organisation in the environment,** not one organisation alone, and
  that it ends every setup link not yet used. Tell the managers of each organisation, so that their people expect to
  sign in again, and the managers resend the invites not yet used.
- **In test,** only as step 30 of [first-use-in-test.md](first-use-in-test.md) does.

### What to have ready

- The time you run it, written down for the record of decisions and dates that a breach needs (design.md §9.10).
- The app answering `200` at `/healthz/ready`, as for `admin bootstrap` above.
- Nothing else: it takes no address and no option.

### Running it

Open the SSH console and check its settings (above). Then paste this block, which runs the command from
`/home/site/wwwroot`, as for `admin bootstrap`:

```shell
cd /home/site/wwwroot && {
  dotnet Grow2Notes.Web.dll admin signout-all
  echo "Exit code: $?"
}
```

### What it writes and prints

In one transaction (design.md §5.9), so all of it or none of it, it writes:

- a new security stamp for every user in every organisation, Invited and Deactivated ones included;
- one audit event `admin.signout_all` for each organisation, with no actor, about the organisation, so that each
  organisation's audit records show it (A28).

It prints nothing, and the block then prints `Exit code: 0`. It sends no email and logs nothing.

Then each session's next request gets `401`, so the person sees "You've been signed out", and signs in again with
their passkey, or their password and a code. The stamp is in every setup link too (design.md §5.3), so a link not yet
used says "This link has expired": a manager sends a new one with Resend invite on the person's page in Users. An
organisation whose only manager is still Invited, as straight after `admin bootstrap`, has no one to resend it, so
run [admin reset-signin](#admin-reset-signin) for that manager.

### If it is refused or fails

- **Exit code 1: refused.** Anything typed after `signout-all` is refused with one line on standard error, such as
  `Refused: "--all" is not an option of admin signout-all. Nothing was written.`, followed by the usage line,
  `Usage: admin signout-all`. Paste the block again as it is.
- **Any other exit code: a failure the command did not expect,** such as a database that stayed unreachable through
  its retries, or an account that changed while the command ran, such as by a failed sign-in counted at the same
  moment; .NET writes the exception on standard error. Nothing was written, so everyone is still signed in: check
  `/healthz/ready` as above, then run it again.
