#!/bin/bash

# Fog Mod Build Script
# This script builds the mod and copies all necessary files to the bin directory

echo "🔨 Building Fog Mod..."

# Build the project
echo "📦 Building project..."
dotnet build fog_mod.sln

CLOUD_DIR="/Users/jakeetaylor/Library/CloudStorage/GoogleDrive-jake.e.taylor1@gmail.com/My Drive/Mods/StardewValley/FogMod"

if [ $? -eq 0 ]; then
    echo "✅ Build successful!"
    
    # Copy the main DLL to bin directory
    echo "📋 Copying FogMod.dll..."
    cp -r bin/Debug/net6.0/* "$CLOUD_DIR"
    echo "🎉 Build and copy completed successfully!"
    echo "📁 Your mod is ready in: $CLOUD_DIR"
else
    echo "❌ Build failed! Please check the error messages above."
    exit 1
fi
