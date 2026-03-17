#!/usr/bin/env bash
# publish.sh — Build AOT single-file binaries for Windows-x64 and Linux-x64
# Requires: .NET 10 SDK  (https://dotnet.microsoft.com/download)
# Run from the solution root:  bash publish.sh

set -euo pipefail

PROJECT="IkeaDownloader.Console/IkeaDownloader.Console.csproj"
OUT_BASE="publish"

# Verify SDK version
SDK_VER=$(dotnet --version 2>/dev/null || echo "not found")
if [[ "$SDK_VER" != 10.* ]]; then
    echo "WARNING: Expected .NET 10 SDK, found: $SDK_VER"
fi

publish() {
    local rid="$1"
    local out="$OUT_BASE/$rid"

    echo ""
    echo "==> Publishing $rid …"

    dotnet publish "$PROJECT" \
        --configuration Release \
        --runtime "$rid" \
        --output "$out"

    echo "    ✔  Output: $out"
}

publish "win-x64"
publish "linux-x64"

echo ""
echo "All done. Binaries are in ./$OUT_BASE/"
