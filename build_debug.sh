#!/bin/bash

# Fog Mod Build Script
# This script builds the mod and copies all necessary files to the Mods directory

echo "🔨 Building Fog Mod..."

# Pick up a user-local .NET SDK install if dotnet isn't on PATH
if ! command -v dotnet >/dev/null 2>&1 && [ -x "$HOME/.dotnet/dotnet" ]; then
    export PATH="$HOME/.dotnet:$PATH"
fi

# Build the project
echo "📦 Building project..."
dotnet build FogMod.csproj -c Debug

if [ $? -eq 0 ]; then
    echo "✅ Build successful!"

    if [ "$(uname)" = "Darwin" ]; then
        MOD_DIR="$HOME/Library/Application Support/Steam/steamapps/common/Stardew Valley/Contents/MacOS/Mods/FoggyGrouse"
    else
        MOD_DIR="$HOME/.local/share/Steam/steamapps/common/Stardew Valley/Mods/FoggyGrouse"
    fi
    mkdir -p "$MOD_DIR"

    echo "📋 Copying mod files..."
    cp -r bin/Debug/net6.0/* "$MOD_DIR"
    echo "🎉 Build and copy completed successfully!"
    echo "📁 Your mod is ready in: $MOD_DIR"
else
    echo "❌ Build failed! Please check the error messages above."
    exit 1
fi
