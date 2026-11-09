#!/bin/bash
# Approves Claude Code to connect to an environment's AI-assistant access, by running the app's own admin mcp-client
# command in the web app's container (ops/break-glass.md, admin mcp-client). Running it again updates the one
# registration. The only authentication is the operator's Azure CLI sign-in. It handles no personal detail.
#
# Usage: bash register-claude-code.sh test|prod
# Output: one outcome line. Exit 0 approved, 3 the environment has no organisation yet, 2 anything else.

export MSYS_NO_PATHCONV=1
export AZURE_LOGGING_ENABLE_LOG_FILE=no
export AZURE_CORE_COLLECT_TELEMETRY=no

case $# in 1) ;; *) echo "Usage: bash register-claude-code.sh test|prod"; exit 2 ;; esac
case $1 in
  test | prod) env=$1 ;;
  *) echo "Usage: bash register-claude-code.sh test|prod"; exit 2 ;;
esac

app=app-grow2notes-$env
group=rg-grow2notes-$env-ause
site=https://$app.azurewebsites.net
result_url=https://$app.scm.azurewebsites.net/api/vfs/LogFiles
run=$(od -An -N8 -tx1 /dev/urandom | tr -d ' \n')
result=grow2notes-admin-$run.result

# Anthropic's privacy centre, read on 9 November 2026: commercial data is stored in the US, and by default traffic may
# be routed to the US, Europe, Asia and Australia. The Connect screen shows this text beside the assistant's name.
where='stored in the United States, and processed there or in Europe, Asia or Australia'

remote="t=/tmp/grow2notes-admin-$run; trap 'rm -f \$t.err' EXIT; "
remote+="dotnet Grow2Notes.Web.dll admin mcp-client add "
remote+="--metadata-url https://claude.ai/oauth/claude-code-client-metadata "
remote+="--name 'Claude Code' --company Anthropic --data-location '$where' >/dev/null 2>\$t.err; s=\$?; "
remote+="f=/home/LogFiles/grow2notes-admin-$run; "
remote+="{ echo \$s; grep -x 'Refused: .*' \$t.err || true; } >\$f.part && mv \$f.part \$f.result"

deadline=$((SECONDS + 120))
until [ "$(curl -s -o /dev/null -w '%{http_code}' --max-time 20 "$site/healthz/ready" </dev/null 2>/dev/null)" = 200 ]; do
  if [ $SECONDS -ge $deadline ]; then
    echo "The app did not answer at /healthz/ready within 2 minutes, so Claude Code was not approved."
    exit 2
  fi
  sleep 5
done

az webapp exec -g "$group" -n "$app" --mode execute --shell /bin/sh --working-directory /home/site/wwwroot \
  --shell-command "$remote" --only-show-errors -o none </dev/null 2>/dev/null || {
  echo "Azure did not start the command, so Claude Code was not approved. Check the Azure sign-in."
  exit 2
}

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

  if [ "$status" -eq 0 ]; then
    echo "Claude Code is approved to connect."
    exit 0
  fi
  if printf '%s\n' "$body" | grep -q 'There is no organisation yet'; then
    echo "There is no organisation yet."
    exit 3
  fi
  printf '%s\n' "$body" | grep -x 'Refused: .*'
  echo "Claude Code was not approved (exit code $status)."
  exit 2
done

echo "No result from the command, so it is not known whether Claude Code was approved."
exit 2
