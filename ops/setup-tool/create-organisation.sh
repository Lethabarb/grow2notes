#!/bin/bash
# What the setup window's Create button runs: makes sure the Azure sign-in can reach the app, refuses a second
# organisation in production (D94), makes the organisation and first manager with the onboarding skill's bootstrap.sh,
# and then approves Claude Code, which needs an organisation to exist.
#
# Usage: bash create-organisation.sh test|prod
# Standard input, three lines, none empty: the organisation's name, the first manager's name, their address.
# Output: outcome lines only, never a value. Exit 0 done, 1 refused, 2 anything else, 4 the outcome is not known.

export MSYS_NO_PATHCONV=1

case ${1:-} in
  test | prod) env=$1 ;;
  *) echo "Usage: bash create-organisation.sh test|prod, then the three values on standard input."; exit 2 ;;
esac

here=$(cd "$(dirname "$0")" && pwd)
bootstrap=$here/../../.claude/skills/onboard-provider/bootstrap.sh
register=$here/register-claude-code.sh

IFS= read -r organisation || exit 2
IFS= read -r manager_name || exit 2
IFS= read -r manager_email || exit 2

reaches_app() {
  [ "$(az webapp show -g "rg-grow2notes-$env-ause" -n "app-grow2notes-$env" --query state -o tsv \
    --only-show-errors </dev/null 2>/dev/null | tr -d '\r')" = Running ]
}

# An expired sign-in still passes az account show, so ask for the app itself, and sign in only when that fails.
if ! reaches_app; then
  az login --only-show-errors -o none </dev/null >/dev/null 2>&1
  if ! reaches_app; then
    echo "This Azure sign-in cannot reach the app, so nothing was created. Sign in as the subscription's Owner."
    exit 2
  fi
fi

# Nothing in the app stops a second organisation, and production must have only one until row-level security lands.
# Approving Claude Code is refused while no organisation exists, so it is also the check for one.
if [ "$env" = prod ]; then
  outcome=$(bash "$register" prod)
  case $? in
    3) ;;
    0)
      echo "Production already has an organisation, so nothing was created. $outcome"
      exit 1
      ;;
    *)
      echo "$outcome"
      echo "Nothing was created."
      exit 2
      ;;
  esac
fi

printf '%s\n%s\n%s\n' "$organisation" "$manager_name" "$manager_email" | bash "$bootstrap" "$env"
code=$?
# Exit 2 means the outcome is not known, and the first run may still be committing, so the check above could miss it.
# Exit 4 tells the window to lock Create. In the same address's case a retry is refused; with another it would not be.
[ $code -eq 2 ] && [ "$env" = prod ] && exit 4
[ $code -eq 0 ] || exit $code

bash "$register" "$env"
exit $?
