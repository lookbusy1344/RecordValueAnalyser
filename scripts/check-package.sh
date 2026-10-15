#!/usr/bin/env bash
# Builds a consumer project against the packed analyzer with one .NET SDK, and checks that
# the SDK loads the expected analyzer build and that the analyzer reports JSV01.
#
# Usage: scripts/check-package.sh <package-dir> <sdk-major> <target-framework> <roslyn-folder>
#   e.g. scripts/check-package.sh ./nuget-output 8 net8.0 roslyn4.8

set -euo pipefail

if [[ $# -ne 4 ]]; then
  echo "Usage: $0 <package-dir> <sdk-major> <target-framework> <roslyn-folder>" >&2
  exit 2
fi

readonly PACKAGE_ID="lookbusy1344.RecordValueAnalyser"
readonly EXPECTED_DIAGNOSTIC="JSV01 R.cs(1,17): Member 'int[] Data' does not have value semantics"
readonly ANALYZER_LOAD_FAILURES='^(CS9057|CS8032|AD0001) '

package_dir=$(cd "$1" && pwd)
sdk_major=$2
tfm=$3
roslyn_folder=$4

package_file=$(find "$package_dir" -maxdepth 1 -name "${PACKAGE_ID}.*.nupkg" | head -n 1)
if [[ -z "$package_file" ]]; then
  echo "FAIL: no ${PACKAGE_ID} package in ${package_dir}" >&2
  exit 1
fi
package_name=$(basename "$package_file" .nupkg)
package_version=${package_name#"${PACKAGE_ID}."}

# the newest installed SDK with this major version
sdk_version=$(dotnet --list-sdks | awk '{print $1}' | grep -E "^${sdk_major}\." | sort -V | tail -n 1 || true)
if [[ -z "$sdk_version" ]]; then
  echo "FAIL: no .NET ${sdk_major} SDK installed" >&2
  exit 1
fi

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
export NUGET_PACKAGES="$work/packages"

cat > "$work/global.json" <<EOF
{ "sdk": { "version": "${sdk_version}", "rollForward": "disable" } }
EOF

cat > "$work/nuget.config" <<EOF
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="${package_dir}" />
  </packageSources>
</configuration>
EOF

cat > "$work/Consumer.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>${tfm}</TargetFramework>
    <Nullable>enable</Nullable>
    <ErrorLog>diagnostics.sarif,version=2.1</ErrorLog>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="${PACKAGE_ID}" Version="${package_version}" PrivateAssets="all" />
  </ItemGroup>
</Project>
EOF

# one record without value semantics, one with
cat > "$work/R.cs" <<'EOF'
public record R(int[] Data);
public record Ok(int A, string B);
EOF

echo "==> SDK ${sdk_version}, ${tfm}, package ${package_version}"
cd "$work"

dotnet restore --nologo > restore.log 2>&1 || { cat restore.log; exit 1; }

# the analyzer DLLs the SDK picked from the package
dotnet msbuild -nologo -t:ResolveLockFileAnalyzers -getItem:Analyzer > analyzers.json
loaded=$(grep -oiE "\"Identity\": \"[^\"]*${PACKAGE_ID}/[^\"]*\.dll" analyzers.json | sed -E 's|.*/analyzers/dotnet/||' | sort || true)
expected=$(printf '%s\n' "${roslyn_folder}/cs/RecordValueAnalyser.CodeFixes.dll" "${roslyn_folder}/cs/RecordValueAnalyser.dll")
if [[ "$loaded" != "$expected" ]]; then
  echo "FAIL: expected analyzers:" >&2
  echo "$expected" >&2
  echo "got:" >&2
  echo "${loaded:-<none>}" >&2
  exit 1
fi

dotnet build --nologo --no-restore > build.log 2>&1 || { cat build.log; exit 1; }

# the compiler's SARIF log holds each diagnostic once, unlike the console output
# one line per diagnostic: "<id> <file>(<line>,<column>): <message>", or "<id> -: <message>" without a source location
diagnostics=$(jq -r '.runs[].results[]
  | (.locations[0].physicalLocation // null) as $at
  | "\(.ruleId) "
    + (if $at then "\($at.artifactLocation.uri | split("/") | last)(\($at.region.startLine),\($at.region.startColumn))" else "-" end)
    + ": \(.message.text)"' diagnostics.sarif)

if grep -qE "$ANALYZER_LOAD_FAILURES" <<< "$diagnostics"; then
  echo "FAIL: the analyzer did not load or crashed:" >&2
  grep -E "$ANALYZER_LOAD_FAILURES" <<< "$diagnostics" >&2
  exit 1
fi

jsv=$(grep -E '^JSV01 ' <<< "$diagnostics" || true)
if [[ "$jsv" != "$EXPECTED_DIAGNOSTIC" ]]; then
  echo "FAIL: expected exactly:" >&2
  echo "$EXPECTED_DIAGNOSTIC" >&2
  echo "got:" >&2
  echo "${jsv:-<none>}" >&2
  exit 1
fi

echo "OK: ${roslyn_folder} loaded and JSV01 reported"
