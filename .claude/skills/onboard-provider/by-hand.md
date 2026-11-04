# By-hand steps for the onboard-provider skill

The skill reads this file when it says so and leads the owner through it. The skill runs none of these steps,
except that in section B it runs `bootstrap.sh` once. The operator pages it links are read from the freshly pulled
main checkout at run time, and no block from them is copied here: [break-glass.md](../../../ops/break-glass.md) and
[go-live.md](../../../ops/go-live.md), section D.

## A. Preconditions

Ask each line on its own, before any provider data is typed. A no stops the run.

### A1. The gate for section B (the organisation and its first manager)

Every line must hold:

- Azure CLI 2.90.0 or later, signed in as Owner on the subscription that holds the app. You run `az version` and
  `az account show` and read the results. The skill runs neither.
- Test: made-up data only (A35), and the first manager's address is a testing-mailbox plus-tag. No real staff address,
  participant or person. Test is for a rehearsal.
- Production: the Results of [go-live.md](../../../ops/go-live.md) D1 and D2 read pass, which includes that no
  organisation exists in production yet.
- Production: the three Anthropic lines in A2, because the first manager's details reach Claude.

### A2. The gate for section C onward

- Test: made-up data only (A35). No real staff address, participant or person. Test is for a rehearsal.
- Production: only once the Result of step D5 in [mcp-switch-on.md](../../../ops/mcp-switch-on.md) reads pass on the
  operator's own copy, which means the AI-assistant access is switched on there.

In production, every line must also hold:

- The Results of go-live.md D1 and D4 read pass.
- The provider's written approval names Claude Code as the Support person's assistant (MA1, D57), and is kept with the
  hosting agreement records.
- Part 1 of [mcp-compliance-pack.md](../../../design/mcp-compliance-pack.md) is answered for Anthropic, on the
  operator's own copy, outside the repository.
- This Claude Code is signed in with the operator's commercial account, under Anthropic's commercial terms and data
  processing agreement. Run /status and read the account it shows.

### A3. Permissions (test and production)

The access tools and Bash must always ask, and nothing else may be allowed beyond the nine set-up and look-up tools.

1. In the onboarding folder, create `.claude/settings.local.json` with exactly this content. It is the owner's own file,
   outside the repository, and is deleted with the folder:

```json
{
  "permissions": {
    "ask": [
      "mcp__grow2notes__invite_user",
      "mcp__grow2notes__reset_user_sign_in",
      "mcp__grow2notes__deactivate_user",
      "mcp__grow2notes__archive_participant",
      "Bash"
    ],
    "allow": [
      "mcp__grow2notes__list_common_items",
      "mcp__grow2notes__add_common_item_group",
      "mcp__grow2notes__add_common_item",
      "mcp__grow2notes__get_guide_prompts",
      "mcp__grow2notes__set_guide_prompts",
      "mcp__grow2notes__find_participants",
      "mcp__grow2notes__get_participant",
      "mcp__grow2notes__add_goal",
      "mcp__grow2notes__list_users"
    ],
    "deny": ["WebSearch", "WebFetch", "PowerShell", "Write", "Edit"]
  }
}
```

2. Claude Code is in the default permission mode: never auto, bypassPermissions or dontAsk.
3. Run /permissions. The ask rules are the four access tools and Bash. The allow rules are only the nine tools in the
   block above, and none for the whole grow2notes server.
4. Run /hooks. It shows no Elicitation hook, because one could answer Grow2Notes' form for you.
5. Run /mcp. Every other MCP server is disabled.
6. Read every invite_user prompt before you choose Yes:
   - The first call for a person shows no confirm, or confirm false. That call only previews.
   - A prompt whose arguments hold confirm true is approved only if you have just said yes in the chat to that exact
     person, and only after you check the email, the name, the role worker and the reason in it. Otherwise choose No.
7. When Claude Code offers "don't ask again", take it for none of the four access tools and never for the whole server.
8. Approve a Bash prompt only for `bootstrap.sh`, with the values as you confirmed them in the chat. Never choose
   "don't ask again" for Bash, because it would save the values in a rule.

### A4. What the skill never asks for

Note content, health detail beyond a goal's wording, dates of birth, setup links, passwords, codes, tokens, phone
numbers, and the values typed at the console. If you paste one by mistake, the skill says so and does not use it. The
one exception is section B's three values, which stay in this session until clean-up (D) deletes its transcript.

## B. The organisation and its first manager

The agent runs this step, once, with `bootstrap.sh`. It signs in with your Azure CLI sign-in, so you type no password
and nothing in a console. The provider tells you on a call who its first manager is (A105). You then give the skill the
organisation's name, the first manager's name and their address in the chat, and it reads them back and asks you one
question. It never asks for a setup link: the first manager is emailed it. Never paste a link into the chat, a file
or a transcript.

1. Read the question and the Bash prompt. The values in the prompt must match what you confirmed. If they differ,
   choose No.
2. Wait for the outcome, which comes within 3 minutes of the app answering. Done means the email was handed to the
   email service, not delivered.
3. If it says Refused, Failed or No result, look for the email first. Never run it a second time for another name or
   address.

### In test

Use made-up values and a testing-mailbox plus-tag address. The made-up first manager opens the emailed link in a
private window and finishes setup, choosing how to sign in. Then go on to C.

### In production

Section B is [go-live.md section D](../../../ops/go-live.md#d-the-providers-organisation-first-manager-and-email),
step D3, done before the switch-on. When it ends with Done, do the clean-up steps 1 to 3 in D at once, because the
values are in this session's transcript, then go on with go-live D4. If the organisation exists already (D4's Result
reads pass), skip B. Never make a second organisation in production: row-level security is deferred (D94), so the
skill stops if asked.

### If the script cannot run

Do the same by hand. Open the SSH console as [break-glass.md, The SSH
console](../../../ops/break-glass.md#the-ssh-console) says. The skill reads the block under break-glass.md, admin
bootstrap, Running it, and shows it exactly as written. Run it in the console, typing the values only at its prompts.
The command emails the first manager the setup link and prints none.

## C. Access for the assistant

### C1. A Support account for you

The first manager signs in, opens Users, and invites you with the role Support (IT operator). In test, use a second
testing-mailbox plus-tag address that differs from the made-up manager's, because one email is one account (A27).
Set up from the email, choosing how to sign in.

### C2. Register Claude Code as an approved assistant (by hand, at the operator console)

Open the environment's SSH console. Run `admin mcp-client list` as [break-glass.md, admin
mcp-client](../../../ops/break-glass.md#admin-mcp-client) says. If Claude Code is missing, the skill shows you the
metadata-document block from that section, read at run time, and you fill in the values:

- `--metadata-url "https://claude.ai/oauth/claude-code-client-metadata"`, as read on 30 October 2026. Re-check it
  against Claude Code's current documentation at code.claude.com/docs before use.
- `--name "Claude Code"`.
- `--company` and `--data-location`: ask the owner. They come from the provider's approval and Anthropic's terms,
  and the skill never invents them.

In production, do this only at the provider's written request (MA1), which is normally done at switch-on. The command
needs the organisation, so C2 comes after B. When it prints, compare the line that starts Document, and both
redirect addresses (http://localhost/callback and http://127.0.0.1/callback), with Claude Code's documentation.

### C3. Add the server to Claude Code

Quit Claude Code. In the onboarding folder, run this, with the environment's name in the address:

```shell
claude mcp add --transport http grow2notes https://app-grow2notes-<test|prod>.azurewebsites.net/mcp
```

- Local scope, which is the default. Never `--scope project`, which writes a file into a project, and never
  `--scope user`.
- The address is exact, with no trailing slash, because the server's resource must equal it.
- No `--client-id`: Claude Code finds the metadata document itself.
- The name must be exactly grow2notes, or the rules in A3 would not match.

Start Claude Code again the same way, from the folder.

### C4. Sign in

Run /mcp, choose grow2notes, and sign in in the browser. On the Connect an AI assistant to Grow2Notes screen check
three things before choosing Allow:

1. It names your Support account.
2. It names Claude Code, with the company and data location you typed in C2.
3. It shows the warning that this assistant runs on your computer and other programs could pretend to be it.

After you choose Allow, type /onboard-provider and choose return.

Waiting does not end the connection: it lasts until 7 days unused. If Claude Code asks you to sign in again, do
it from /mcp. Nothing is lost.

### C5. When it fails

- `Incompatible auth server` with dynamic client registration: update Claude Code. If it persists, stop and tell the
  developer. Do not improvise a client ID or a callback port.
- A Grow2Notes error page at sign-in: Claude Code is not registered there. Go back to C2.
- A 404: the AI-assistant access is off in that environment, or the address is wrong.
- Calls that fail with a rate limit: wait a minute (HTTP 429), or for the time the skill gives you.

## D. Clean-up

Run /permissions first, before the folder is deleted, and check that no Bash allow rule is there (the ask rule from A3
is expected). If there is one, remove it, because it holds the values. After section B
in production, no server has been added, so do steps 1 to 3 below and skip the two commands.

Do these in order when the run ends:

```shell
claude mcp logout grow2notes
claude mcp remove grow2notes
```

Remove alone revokes nothing, so logout comes first. Then:

1. Delete the input file and the onboarding folder. Its settings file goes with it.
2. Delete this run's transcript folder under `~/.claude/projects`. Its name is the onboarding folder's path with every
   character other than a letter or digit made a hyphen. Look at the folder names in the listing first, and delete only
   the one that matches. Deleting it is the "delete the chat" habit.
3. If you typed rows rather than reading a file, find this run's lines in Claude Code's prompt history,
   `~/.claude/history.jsonl`, and remove them.
4. Start a new session in a different folder and check that /mcp lists no grow2notes server.
