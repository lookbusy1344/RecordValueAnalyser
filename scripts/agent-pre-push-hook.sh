#!/usr/bin/env bash
# Agent pre-tool hook: run scripts/pre-push.sh before any push.
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
readonly PUSH_PATTERN='\b(jj\s+git\s+push|jj\s+(pmain|fixpush)|git(\s+-C\s+\S+)?\s+push)\b'

# Any other exit code is a non-blocking error, which would let the push through.
trap 'echo "Push blocked: agent-pre-push-hook.sh failed at line ${LINENO}." >&2; exit "${BLOCK_EXIT_CODE}"' ERR

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
readonly PROJECT_DIR

command="$(jq -r '.tool_input.command // empty')"
rg -q "${PUSH_PATTERN}" <<< "${command}" || exit 0

log="$(mktemp)"
trap 'rm -f "${log}"' EXIT

if ! "${PROJECT_DIR}/scripts/pre-push.sh" > "${log}" 2>&1; then
    echo "Push blocked: scripts/pre-push.sh failed. Fix the failures and retry." >&2
    tail -n "${LOG_TAIL_LINES}" "${log}" >&2
    exit "${BLOCK_EXIT_CODE}"
fi

# rg exits 1 on no match; the no-op path runs no tests.
checked="$({ rg '^==> (Checking|No non-empty|Not a jj or Git)' "${log}" || true; } | sed 's/^==> //' | paste -sd ';' -)"
tests="$({ rg -o '(Passed!|Failed!).*Total: +[0-9]+.*' "${log}" || true; } | tail -n 1)"
message="Pre-push checks passed: ${checked}${tests:+ (${tests})}"

jq -n --arg message "${message}" '{
    systemMessage: $message,
    hookSpecificOutput: {hookEventName: "PreToolUse", additionalContext: $message}
}'
