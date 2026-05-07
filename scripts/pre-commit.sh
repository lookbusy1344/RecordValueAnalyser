#!/usr/bin/env bash
# pre-commit.sh — run all required checks before committing.
#
# Install (one-time setup):
#   ln -sf ../../scripts/pre-commit.sh .git/hooks/pre-commit
#
# Or from an existing hook:
#
# if [ ! -x "scripts/pre-commit.sh" ]; then
#     echo "Missing executable scripts/pre-commit.sh" >&2
#     exit 1
# fi
#
# ./scripts/pre-commit.sh

set -euo pipefail

# Resolve the real script path before computing directories. We can't rely on
# `readlink -f` because it's unavailable on macOS's BSD userland by default.
resolve_script_path() {
    local source_dir=""
    local source_path="$1"

    while [ -L "${source_path}" ]; do
        source_dir="$(cd "$(dirname "${source_path}")" && pwd)"
        source_path="$(readlink "${source_path}")"
        [[ "${source_path}" != /* ]] && source_path="${source_dir}/${source_path}"
    done

    source_dir="$(cd "$(dirname "${source_path}")" && pwd)"
    printf '%s\n' "${source_dir}/$(basename "${source_path}")"
}

REAL_SCRIPT="$(resolve_script_path "$0")"
SCRIPT_DIR="$(cd "$(dirname "${REAL_SCRIPT}")" && pwd)"
PROJECT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"

cd "${PROJECT_DIR}"

# Wrapper that prints each command before running it.
run() {
    echo "==> $*"
    "$@"
}

# Only trigger if .NET source or build files are modified — staged OR unstaged.
# `git diff HEAD` catches both, so a dirty working tree can't slip past the hook
# just because the user staged unrelated changes.
if ! git -C "${PROJECT_DIR}" diff HEAD --name-only -z | tr '\0' '\n' | grep -qE \
    '\.(cs|csproj|sln|editorconfig|props|targets)$'; then
    echo "==> No .NET source or build files modified, skipping."
    exit 0
fi

echo "==> Running RecordValueAnalyser pre-commit checks..."

run dotnet build -c Debug RecordValueAnalyser.Test
run dotnet format --verify-no-changes
run gtimeout 120 dotnet test

echo "==> All checks passed."
