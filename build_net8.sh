#!/usr/bin/env bash
set -euo pipefail

SOLUTION=${1:-SpssNet.sln}
CONFIG=${2:-Debug}

if [ ! -f "$SOLUTION" ]; then
  echo "Solution file '$SOLUTION' not found. Run this script from the repository root where SpssNet.sln is located."
  exit 1
fi

echo "Restoring NuGet packages for $SOLUTION..."
dotnet restore "$SOLUTION"

echo "Building $SOLUTION ($CONFIG)..."
dotnet build "$SOLUTION" -c "$CONFIG"

echo "Build succeeded."
