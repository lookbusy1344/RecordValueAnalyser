#!/usr/bin/env bash
# pre-push.sh — format, build and test unpushed jj revisions.
#
# In a plain Git checkout there are no revisions to select or amend: it runs
# the same checks on the working tree without formatting it, and refuses
# uncommitted changes to tracked files so the checked tree is the pushed HEAD.
# --full makes no difference there.
#
# Default: the tip only (the newest non-empty mutable revision in ::@).
# --full: every non-empty mutable revision in ::@, each in its own checkout
# via `jj run`.
#
# Run before `jj git push` or moving a shared bookmark. Analyzer severities
# live in .editorconfig, so `dotnet format --verify-no-changes` enforces them.

set -euo pipefail

# GUI and hook launches omit the user-local dotnet and Homebrew paths.
export PATH="$HOME/.dotnet:/opt/homebrew/bin:/usr/local/bin:$PATH"

readonly TEST_TIMEOUT_SECONDS=120
readonly SOLUTION_PATH="RecordValueAnalyser.sln"
readonly TEST_PROJECT="RecordValueAnalyser.Test"
readonly STACK='mutable() & ::@ ~ empty()'
readonly TIP="heads(${STACK})"
readonly CHECKS="dotnet restore \
&& dotnet build --configuration Debug --no-restore ${TEST_PROJECT} \
&& dotnet format ${SOLUTION_PATH} --verify-no-changes \
&& gtimeout ${TEST_TIMEOUT_SECONDS} dotnet test --no-restore"

# Inline so formatting does not depend on unversioned .jj/repo/config.toml.
# Whitespace rules only; the full style check runs in CHECKS.
readonly FORMAT_CONFIG=(
    --config 'fix.tools.dotnet-format.command=["scripts/format-stdin.sh", "$path"]'
    --config 'fix.tools.dotnet-format.patterns=["glob:\"**/*.cs\""]'
    --config 'fix.tools.dotnet-format.enabled=true'
)

usage() {
    echo "Usage: $0 [--full|-f]"
    echo "  --full, -f  Format and check every mutable revision, not only the tip."
}

full=false
while [[ $# -gt 0 ]]; do
    case "$1" in
        --full | -f) full=true; shift ;;
        -h | --help) usage; exit 0 ;;
        *) echo "Error: unknown argument '$1'" >&2; usage >&2; exit 1 ;;
    esac
done

if root="$(jj --ignore-working-copy root 2> /dev/null)"; then
    use_jj=true
elif root="$(git rev-parse --show-toplevel 2> /dev/null)"; then
    use_jj=false
else
    echo "==> Not a jj or Git repository, skipping pre-push checks."
    exit 0
fi
cd "${root}"

if [[ "${use_jj}" == false ]]; then
    if [[ -n "$(git status --porcelain --untracked-files=no)" ]]; then
        echo "==> Uncommitted changes to tracked files. Commit or stash them first." >&2
        exit 1
    fi
    echo "==> Checking working tree at $(git log -1 --format='%h %s')"
    bash -c "${CHECKS}"
    echo "==> All checks passed."
    exit 0
fi

stack="$(jj log --no-graph -r "${STACK}" -T 'change_id ++ "\n"')"
if [[ -z "${stack}" ]]; then
    echo "==> No non-empty mutable revisions in ${STACK}, nothing to check."
    exit 0
fi

if [[ "${full}" == true ]]; then
    echo "==> Formatting ${STACK}"
    jj "${FORMAT_CONFIG[@]}" fix -s "roots(${STACK})"
    echo "==> Checking each revision in ${STACK}"
    jj run --root --ignore-changes -r "${STACK}" -- bash -c "${CHECKS}"
else
    # Checks run in the working copy; when @ is empty its tree is the tip's.
    echo "==> Formatting tip ${TIP}"
    jj "${FORMAT_CONFIG[@]}" fix -s "${TIP}"
    echo "==> Checking tip $(jj log --no-graph -r "${TIP}" -T 'change_id.short() ++ " " ++ coalesce(description.first_line(), "(no description)")')"
    bash -c "${CHECKS}"
fi

echo "==> All checks passed."
