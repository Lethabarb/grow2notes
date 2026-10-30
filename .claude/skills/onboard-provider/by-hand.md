# By-hand steps for the onboard-provider skill

The skill reads this file when it says so and leads the owner through it. The skill runs none of these steps. The
operator pages it links are read from the freshly pulled main checkout at run time, and no block from them is copied
here: [break-glass.md](../../../ops/break-glass.md) and [go-live.md](../../../ops/go-live.md), section D.

## A. Preconditions

Ask each line on its own, before any provider data is typed. A no stops the run.

### A1. Environment

- Test: made-up data only (A35). No real staff address, participant or person. Test is for a rehearsal.
- Production: only once the Result of step D5 in [mcp-switch-on.md](../../../ops/mcp-switch-on.md) reads pass on the
  operator's own copy, which means the AI-assistant access is switched on there.

### A2. Production gate (production only)

Every line must hold:

- The Results of go-live.md D1 and D4 read pass.
- The provider's written approval names Claude Code as the Support person's assistant (MA1, D57), and is kept with the
  hosting agreement records.
- Part 1 of [mcp-compliance-pack.md](../../../design/mcp-compliance-pack.md) is answered for Anthropic, on the
  operator's own copy, outside the repository.
- This Claude Code is signed in with the operator's commercial account, under Anthropic's commercial terms and data
  processing agreement. Run /status and read the account it shows.

### A3. Permissions (test and production)

The access tools must always ask, and nothing else may be allowed beyond the nine set-up and look-up tools.

1. In the onboarding folder, create `.claude/settings.local.json` with exactly this content. It is the owner's own file,
   outside the repository, and is deleted with the folder:

```json
{
  "permissions": {
    "ask": [
      "mcp__grow2notes__invite_user",
      "mcp__grow2notes__reset_user_sign_in",
      "mcp__grow2notes__deactivate_user",
      "mcp__grow2notes__archive_participant"
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
    "deny": ["WebSearch", "WebFetch", "Bash", "PowerShell", "Write", "Edit"]
  }
}
```

2. Claude Code is in the default permission mode: never auto, bypassPermissions or dontAsk.
3. Run /permissions. It shows allow rules for only the nine tools in the block above, and none for the whole
   grow2notes server.
4. Run /hooks. It shows no Elicitation hook, because one could answer Grow2Notes' form for you.
5. Run /mcp. Every other MCP server is disabled.
6. Read every invite_user prompt before you choose Yes:
   - The first call for a person shows no confirm, or confirm false. That call only previews.
   - A prompt whose arguments hold confirm true is approved only if you have just said yes in the chat to that exact
     person, and only after you check the email, the name, the role worker and the reason in it. Otherwise choose No.
7. When Claude Code offers "don't ask again", take it for none of the four access tools and never for the whole server.

### A4. What the skill never asks for

Note content, health detail beyond a goal's wording, dates of birth, setup links, passwords, codes, tokens, phone
numbers, and the values typed at the console. If you paste one by mistake, the skill says so and does not use it.

## B. The organisation and its first manager

The skill runs nothing here. You run the commands, in the SSH console. Details for a real provider come in person or on
a call, never by chat. Never paste the setup link into the chat, a file or a transcript: the skill never asks for it.

### In test

1. Open the SSH console as [break-glass.md, The SSH console](../../../ops/break-glass.md#the-ssh-console) says.
2. The skill reads the block under break-glass.md, admin bootstrap, Running it, and shows it exactly as written. Run
   it in test's console. Type made-up values only, and only at its prompts: an organisation name, a made-up first
   manager and a made-up address, as break-glass.md's When to run it says.
3. The made-up first manager opens the link the console printed in a private window and finishes setup, with MFA.

### In production

Section B is [go-live.md section D](../../../ops/go-live.md#d-the-providers-organisation-first-manager-and-email),
done before the switch-on. Confirm that D4's Result reads pass, and skip B. Never make a second organisation in
production: row-level security is deferred (D94), so the skill stops if asked.

## C. Access for the assistant

### C1. A Support account for you

The first manager signs in, opens Users, and invites you with the role Support (IT operator). In test, use a second
testing-mailbox plus-tag address that differs from the made-up manager's, because one email is one account (A27).
Set up from the email, with MFA.

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

Start Claude Code again the same way, from the folder, and type /onboard-provider, then choose return.

### C4. Sign in

Run /mcp, choose grow2notes, and sign in in the browser. On the Connect an AI assistant to Grow2Notes screen check
three things before choosing Allow:

1. It names your Support account.
2. It names Claude Code, with the company and data location you typed in C2.
3. It shows the warning that this assistant runs on your computer and other programs could pretend to be it.

If you later wait more than 30 minutes without a call, sign in again from /mcp. Nothing is lost.

### C5. When it fails

- `Incompatible auth server` with dynamic client registration: update Claude Code. If it persists, stop and tell the
  developer. Do not improvise a client ID or a callback port.
- A Grow2Notes error page at sign-in: Claude Code is not registered there. Go back to C2.
- A 404: the AI-assistant access is off in that environment, or the address is wrong.
- Calls that fail with a rate limit: wait a minute (HTTP 429), or for the time the skill gives you.

## D. Clean-up

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
