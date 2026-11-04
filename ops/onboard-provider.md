# Onboarding a new provider with Claude Code

## Purpose

The owner is led through onboarding one new provider, end to end, by a Claude Code skill (D95), kept in
`.claude/skills/onboard-provider/`. The skill explains each by-hand step and runs none, with one exception: once you
have confirmed the values, it makes the organisation and its first manager by running the app's own `admin
bootstrap` through its script `bootstrap.sh`, with `az webapp exec` on your Azure CLI sign-in, so you type nothing in
a console (D101). The first manager is emailed the setup link. The skill changes the provider's set-up only through
the Grow2Notes tools, as the operator's Support person, and calls ten of them. It adds no tool, no server feature and
no operator command over MCP (D48).

## When

- Rehearse in test first, with made-up data only: steps 204 to 207 of [first-use-in-test.md](first-use-in-test.md).
- Production only after step D5 of [mcp-switch-on.md](mcp-switch-on.md) reads pass. The go-live set-up may already
  exist there (go-live.md section E, line 7), so a run adds only what is missing.
- The organisation step (section B) needs Azure CLI 2.90.0 or later, where `az webapp exec` is a preview command,
  signed in as Owner on the subscription; `az upgrade` updates it. In production that step is go-live.md D3, run once
  D1's gate reads pass and before the switch-on.

## Starting

1. In the main checkout, run `git switch main` and `git pull --ff-only`. The skill reads these ops pages from that
   checkout, so a stale one gives stale steps.
2. Make one empty folder for each environment, for example one for test and one for production, directly under your user
   folder: not inside the repository, and not in Documents, Desktop, OneDrive or any synced folder. Local scope is kept
   for each folder's path, so test and production never share a server, and no session in the repository ever sees it.
3. In that folder create `.claude/settings.local.json` from the block in section A3 of
   [by-hand.md](../.claude/skills/onboard-provider/by-hand.md). If wanted, copy `template.csv` there and fill it in.
4. Start Claude Code from that folder and accept the folder trust prompt for it only.
   - Git Bash: `CLAUDE_CODE_DISABLE_AUTO_MEMORY=1 claude --add-dir "<main checkout>"`
   - PowerShell: `$env:CLAUDE_CODE_DISABLE_AUTO_MEMORY = '1'; claude --add-dir '<main checkout>'`
5. Type `/onboard-provider`. If it is not offered, copy the skill folder into `<onboarding folder>/.claude/skills/` and
   start again the same way. The `--add-dir` is still needed so the skill can read these pages.

## What the tools do and what stays by hand

| Through the tools, or the skill's script | By hand |
| --- | --- |
| The organisation and first manager, through `bootstrap.sh` on your yes (D101) | Hearing from the provider, on a call, who its first manager is (A105) |
| Common item groups and items | The Support account, granted by a manager in Users |
| Guide prompts | Registering Claude Code (`admin mcp-client add`) |
| Goals for participants a manager has added | Adding participants, with dates of birth, in the app |
| Worker invites, each with a reason | `claude mcp add`, signing in, and every permission prompt |

## Invites and pacing

The skill invites last, after the set-up. The hour's allowance and its arithmetic are in the pacing paragraph of
[mcp-switch-on.md](mcp-switch-on.md). The skill keeps no count: the server's `rate_limited` is the stop, and the owner
waits. The wait does not end the connection, which lasts until 7 days unused (D100). If Claude Code asks you to
sign in again, do it from `/mcp`, which loses nothing.

The invite email still says that a manager added the person, even when Support invites (A138). Whether that needs its
own wording is step 201 of first-use-in-test.md.

## Clean-up

Section D of by-hand.md: `claude mcp logout grow2notes`, then `claude mcp remove grow2notes`; delete the input file,
the onboarding folder and this run's transcript folder; and check `~/.claude/history.jsonl` for typed rows.

## Claude Code documentation, as read on 30 October 2026

Pages read: `code.claude.com/docs/en/` `mcp`, `skills`, `permissions`, `permission-modes` and `memory`; Claude's
connector authentication page at `claude.com/docs/connectors/building/authentication`; and the Claude Code client
metadata document at `claude.ai/oauth/claude-code-client-metadata`. No page carried a date. The newest versions named
were v2.1.309 (skills) and v2.1.304 (MCP).

Facts relied on:

- A remote HTTP server is added with `claude mcp add --transport http <name> <url>` and signed in from `/mcp`. The
  default scope is local, kept in `~/.claude.json` under the folder's path. `--scope project` writes a shared file.
- A server that uses a client ID metadata document is discovered automatically, with no dynamic client registration.
  Without either, Claude Code shows "Incompatible auth server: does not support dynamic client registration".
- The metadata document at the address above names Claude Code, lists both loopback redirects (`localhost` and
  `127.0.0.1`, path `/callback`) and uses the authentication method none. Loopback redirects match on any port, and the
  server's resource must equal the address as entered.
- `claude mcp logout <name>` revokes the tokens at the server. `claude mcp remove <name>` revokes nothing.
- Claude Code declares both form and URL elicitation, so Grow2Notes' confirmation form is expected to show. An
  `Elicitation` hook can answer a form without showing it, which is why `/hooks` is checked. The preview path covers a
  client that shows no form. How an HTTP 429 shows is not documented.
- Skills in an `--add-dir` folder load. `disable-model-invocation: true` stops Claude starting a skill, and
  `allowed-tools` pre-approves tools, so the skill has none. Compaction keeps the first 5,000 tokens of a skill.
- Rules are checked deny, then ask, then allow. An ask rule prompts in every permission mode, except that `dontAsk`
  denies. Rules are written `mcp__<server>__<tool>`. So Bash, which the skill runs once, is an ask rule: a deny rule
  would stop it, and an allow rule would skip the prompt (D101).
- `CLAUDE_CODE_DISABLE_AUTO_MEMORY=1` turns auto memory off; `/memory` shows it.

## Azure CLI documentation, as read on 3 November 2026

Page read: the `az webapp` reference on Microsoft Learn (`learn.microsoft.com/cli/azure/webapp`), section `az webapp
exec`. Facts relied on by `bootstrap.sh`:

- `az webapp exec` is in preview and works only for Linux App Service plans. `--mode execute` runs a command in the
  app's main container and returns at once with no output and no exit code; a success means only that the command
  was accepted.
- `--shell-command` runs as `<shell> -c <command>`, with `--shell` choosing the shell, and the page's example
  captures output by writing it to a file under `/home/LogFiles`. `--cwd` (or `--working-directory`) sets the
  folder. With no `--instance`, a random instance is used; Grow2Notes runs one.

A pre-registered client ID (`--client-id`, `--callback-port`) is the developer's fallback only, never improvised by the
owner: it would change what the operator registers (MA2).

Re-check before use: the metadata document and its redirects (compare the printed Document line), and elicitation
support. The rehearsal records: form path or preview path, how a 429 showed, the `/mcp` labels, whether skills from
`--add-dir` were offered, and whether typed rows reached the prompt history.
