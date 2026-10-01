#!/usr/bin/env bash
# format-stdin.sh <path> — `jj fix` tool: read C# source on stdin, write the
# whitespace-formatted result to stdout, using the repo .editorconfig.
#
# `dotnet format` works on files, so the input goes through a scratch folder.

set -euo pipefail

readonly EDITORCONFIG=".editorconfig"

path="${1:?usage: format-stdin.sh <path>}"

scratch="$(mktemp -d)"
trap 'rm -rf "${scratch}"' EXIT

[[ -f "${EDITORCONFIG}" ]] && cp "${EDITORCONFIG}" "${scratch}/"
target="${scratch}/$(basename "${path}")"
cat > "${target}"

dotnet format whitespace "${scratch}" --folder --include "$(basename "${path}")" > /dev/null 2>&1 || true
cat "${target}"
