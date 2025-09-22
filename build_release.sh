#!/bin/bash

# Fog Mod Release Build Script
# This script builds the mod in release configuration, renames the output folder, and creates a zip file
# Usage: ./build_release.sh [version]
# If no version is provided, defaults to 0.3

# Set default version or use provided version
VERSION=${1:-"0.3"}
echo "🚀 Building Fog Mod for Release (Version: $VERSION)..."

# Update manifest.json with the version
echo "📝 Updating manifest.json with version $VERSION..."
sed -i.bak "s/\"Version\": \"[^\"]*\"/\"Version\": \"$VERSION\"/" manifest.json
rm manifest.json.bak

# Clean previous release build
echo "🧹 Cleaning previous release build..."
rm -rf bin/Release/net6.0
rm -rf bin/Release/FoggyGrouse
rm -f bin/Release/FoggyGrouse.zip

# Build the project in Release configuration with version
echo "📦 Building project in Release configuration..."
dotnet build fog_mod.sln -c Release -p:Version=$VERSION

if [ $? -eq 0 ]; then
    echo "✅ Build successful!"
    
    # Check if the net6.0 directory exists
    if [ -d "bin/Release/net6.0" ]; then
        echo "📋 Renaming net6.0 folder to FoggyGrouse..."
        mv bin/Release/net6.0 bin/Release/FoggyGrouse
        
        # Create zip file
        echo "🗜️ Creating FoggyGrouse.zip..."
        cd bin/Release
        zip -r FoggyGrouse.zip FoggyGrouse/
        cd ../..
        
        echo "🎉 Release build completed successfully!"
        echo "📁 Release files are in: bin/Release/"
        echo "📦 Zip file: bin/Release/FoggyGrouse.zip"
        echo "📂 Mod folder: bin/Release/FoggyGrouse/"
    else
        echo "❌ Release build directory not found! Expected: bin/Release/net6.0"
        exit 1
    fi
else
    echo "❌ Build failed! Please check the error messages above."
    exit 1
fi