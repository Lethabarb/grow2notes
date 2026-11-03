#!/bin/bash
# Makes a provider's organisation and first manager by running the app's own admin bootstrap command in the web
# app's container (design.md section 7.4, D101). Run by the onboard-provider skill, in section B, once for each
# organisation the owner has confirmed. The only authentication is the operator's Azure CLI sign-in.
#
# Usage: bash bootstrap.sh test|prod
# Standard input, three lines, none empty: the organisation's name, the first manager's name, their address.
# Output: outcome lines only, never a value. Exit 0 done, 1 refused or a setting missing, 2 anything else.

export MSYS_NO_PATHCONV=1
export AZURE_LOGGING_ENABLE_LOG_FILE=no
export AZURE_CORE_COLLECT_TELEMETRY=no

usage() {
  echo "Usage: bash bootstrap.sh test|prod, then the organisation name, the first manager's name and their address," \
    "each on its own line of standard input and none empty."
  exit 2
}

[ $# -eq 1 ] || usage
case $1 in
  test | prod) env=$1 ;;
  *) usage ;;
esac

IFS= read -r organisation || usage
IFS= read -r manager_name || usage
IFS= read -r manager_email || usage
for value in "$organisation" "$manager_name" "$manager_email"; do
  case $value in
    '' | *$'\r'*) usage ;;
  esac
done

app=app-grow2notes-$env
group=rg-grow2notes-$env-ause
site=https://$app.azurewebsites.net
result_url=https://$app.scm.azurewebsites.net/api/vfs/LogFiles
run=$(od -An -N8 -tx1 /dev/urandom | tr -d ' \n')
[ ${#run} -eq 16 ] && [ -z "${run//[0-9a-f]/}" ] || usage
result=grow2notes-admin-$run.result

encode() { printf %s "$1" | base64 -w0; }

# The settings the command reads: break-glass.md's six, and the two the setup-link email needs.
names='ConnectionStrings__Grow2Notes ManagedIdentity__ClientId DataProtection__KeyVaultKeyUri App__Origin '
names+='IDENTITY_ENDPOINT IDENTITY_HEADER Email__ApiKeySecretUri Email__SenderAddress'

# One line of POSIX sh for the container. The values travel as base64, so no quoting depends on them. Expanded with
# no field splitting and no globbing, each reaches the command as one word. Only the exit status and whole lines that
# start Refused: are kept, and the full error output is removed with the temporary files, so a stack trace never
# reaches /home. The result is written under another name and renamed, so a read never sees half of it.
remote="set -f; IFS=; t=/tmp/grow2notes-admin-$run; trap 'rm -f \$t.out \$t.err' EXIT; "
remote+="for n in $names; do printenv \$n >/dev/null || echo 'Missing setting:' \$n >>\$t.out; done; "
remote+="if [ -s \$t.out ]; then s=1; else "
remote+="o=\$(echo $(encode "$organisation") | base64 -d); m=\$(echo $(encode "$manager_name") | base64 -d); "
remote+="e=\$(echo $(encode "$manager_email") | base64 -d); "
remote+="Email__UseConsole=false dotnet Grow2Notes.Web.dll admin bootstrap --organisation \$o --manager-name \$m "
remote+="--manager-email \$e >/dev/null 2>\$t.err; s=\$?; grep -x 'Refused: .*' \$t.err >\$t.out; fi; "
remote+="f=/home/LogFiles/grow2notes-admin-$run; { echo \$s; cat \$t.out; } >\$f.part && mv \$f.part \$f.result"

# The characters that Azure CLI's Windows launcher can break on.
case $remote in
  *'"'* | *%* | *^* | *'!'* | *$'\n'*) usage ;;
esac

# The same pre-check as break-glass.md. In test, the first request also wakes the paused database.
deadline=$((SECONDS + 120))
answered() {
  [ "$(curl -s -o /dev/null -w '%{http_code}' --max-time 20 "$site/healthz/ready" </dev/null 2>/dev/null)" = 200 ]
}
until answered; do
  if [ $SECONDS -ge $deadline ]; then
    echo "The app did not answer 200 at /healthz/ready within 2 minutes, so nothing ran."
    exit 2
  fi
  sleep 5
done

no_result() {
  echo "No result (run $run). It may have run. Look for the email first, and check the Azure CLI version and sign-in" \
    "(by-hand.md A). A second run for the same address is refused and writes nothing."
  exit 2
}

# Fire and forget: the command returns no output and no exit code. Its stderr is dropped so that no argument is echoed.
az webapp exec -g "$group" -n "$app" --mode execute --shell /bin/sh --working-directory /home/site/wwwroot \
  --shell-command "$remote" --only-show-errors -o none </dev/null 2>/dev/null || no_result

# Any read failure waits and tries again: until the file exists, the answer is a not-found. A sign-in problem
# therefore ends as no result, which says to check it.
deadline=$((SECONDS + 180))
while [ $SECONDS -lt $deadline ]; do
  sleep 5
  body=$(az rest --method get --url "$result_url/$result" --resource https://management.core.windows.net/ \
    --only-show-errors </dev/null 2>/dev/null | tr -d '\r')
  status=$(printf '%s\n' "$body" | head -n 1)
  case $status in
    '' | *[!0-9]*) continue ;;
  esac
  az rest --method delete --url "$result_url/$result" --resource https://management.core.windows.net/ \
    --headers 'If-Match=*' --only-show-errors </dev/null >/dev/null 2>&1

  missing=$(printf '%s\n' "$body" | grep -x 'Missing setting: [A-Za-z0-9_]*')
  if [ -n "$missing" ]; then
    echo "$missing"
    echo "Stopped: the app's settings were not all there, so nothing ran."
    exit 1
  fi
  if [ "$status" -eq 0 ]; then
    echo "Done: the organisation and its first manager were created, and the setup-link email was handed to the" \
      "email service."
    exit 0
  fi
  refused=$(printf '%s\n' "$body" | grep -x 'Refused: .*')
  if [ "$status" -eq 1 ] && [ -n "$refused" ]; then
    echo "$refused"
    exit 1
  fi
  echo "Failed with exit code $status. Nothing more is shown because it can hold personal details. Look for the" \
    "email first, then follow break-glass.md, If it is refused or fails."
  exit 2
done

no_result
