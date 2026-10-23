#!/usr/bin/env bash
# Agent pre-tool hook: run scripts/pre-commit.sh before any commit.
#
# jj has no commit hooks, so this is the gate for `jj commit` and `jj squash`.
# `jj describe` is left to the agent: a describe may only be a working note.
# `git commit` is gated here only when no Git pre-commit hook is installed.
#
# Shared by Claude Code (.claude/settings.json) and Codex (.codex/hooks.json).
# Both send the PreToolUse call as JSON on stdin with the shell command at
# .tool_input.command, and block the call on exit 2 with stderr as the reason.
# On success it prints JSON: systemMessage for the user, additionalContext for
# the agent.

set -euo pipefail

# GUI and hook launches omit the user-local dotnet and Homebrew paths.
export PATH="$HOME/.dotnet:/opt/homebrew/bin:/usr/local/bin:$PATH"

readonly BLOCK_EXIT_CODE=2
readonly LOG_TAIL_LINES=60
readonly JJ_COMMIT_PATTERN='\bjj(\s+--?\S+)*\s+(commit|ci|squash)\b'
readonly GIT_COMMIT_PATTERN='\bgit(\s+-C\s+\S+)?\s+commit\b'

# Any other exit code is a non-blocking error, which would let the commit through.
trap 'echo "Commit blocked: agent-pre-commit-hook.sh failed at line ${LINENO}." >&2; exit "${BLOCK_EXIT_CODE}"' ERR

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
readonly PROJECT_DIR

git_hook_installed() {
    local hook
    hook="$(git -C "${PROJECT_DIR}" rev-parse --git-path hooks/pre-commit 2> /dev/null)" || return 1
    [[ "${hook}" == /* ]] || hook="${PROJECT_DIR}/${hook}"
    [[ -x "${hook}" ]]
}

command="$(jq -r '.tool_input.command // empty')"
if ! rg -q "${JJ_COMMIT_PATTERN}" <<< "${command}"; then
    rg -q "${GIT_COMMIT_PATTERN}" <<< "${command}" || exit 0
    git_hook_installed && exit 0
fi

log="$(mktemp)"
trap 'rm -f "${log}"' EXIT

if ! "${PROJECT_DIR}/scripts/pre-commit.sh" > "${log}" 2>&1; then
    echo "Commit blocked: scripts/pre-commit.sh failed. Fix the failures and retry." >&2
    tail -n "${LOG_TAIL_LINES}" "${log}" >&2
    exit "${BLOCK_EXIT_CODE}"
fi

# rg exits 1 on no match; the skip paths run no tests.
tests="$({ rg -o '(Passed!|Failed!).*Total: +[0-9]+.*' "${log}" || true; } | tail -n 1)"
message="Pre-commit checks passed${tests:+: ${tests}}"
[[ -n "${tests}" ]] || message="Pre-commit checks: $(tail -n 1 "${log}")"

jq -n --arg message "${message}" '{
    systemMessage: $message,
    hookSpecificOutput: {hookEventName: "PreToolUse", additionalContext: $message}
}'
