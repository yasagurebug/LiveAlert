#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

export DOTNET_ROOT="$HOME/.dotnet"
export JAVA_HOME="$HOME/.java/jdk-17.0.11+9"
export ANDROID_SDK_ROOT="$HOME/android-sdk"
export NUGET_PACKAGES="${NUGET_PACKAGES:-/mnt/c/nuget}"
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$JAVA_HOME/bin:$ANDROID_SDK_ROOT/cmdline-tools/latest/bin:$ANDROID_SDK_ROOT/platform-tools:$PATH"

LOG_DIR="$ROOT/logs"
mkdir -p "$LOG_DIR"
LOG_FILE="$LOG_DIR/test.log"

dotnet test "$ROOT/tests/LiveAlert.Core.Tests/LiveAlert.Core.Tests.csproj" -c Debug -v:n | tee "$LOG_FILE"
