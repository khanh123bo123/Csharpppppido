#!/bin/bash

# Configuration
DOTNET="/usr/local/share/dotnet/dotnet"
BASE_DIR="/Users/dangkhoa/vinhkhanhapp/du-an-moi"
APP_DIR="$BASE_DIR/TouristGuideApp"
API_DIR="$BASE_DIR/TourGuideApi"
WWWROOT="$API_DIR/wwwroot"
TIMESTAMP=$(date +%s)
EXPORT_NAME="downloads/app-v2-$TIMESTAMP.apk"

# Ensure SDK path is set for the build
export ANDROID_HOME="$HOME/Library/Android/sdk"
export PATH="$PATH:$ANDROID_HOME/platform-tools:$ANDROID_HOME/cmdline-tools/latest/bin"

echo "🚀 Starting Android APK Build Process..."

# 1. Clean previous build artifacts
echo "🧹 Cleaning up old build files..."
rm -rf "$APP_DIR/bin"
rm -rf "$APP_DIR/obj"
mkdir -p "$WWWROOT"

# 2. Build and Publish the App for Android
# Using Release configuration for stability, signed with default debug keystore for local testing
echo "🏗️  Building .NET MAUI Android App (Release)..."
cd "$APP_DIR"
$DOTNET publish -f net10.0-android -c Release \
  -p:AndroidKeyStore=true \
  -p:AndroidSigningStorePass=android \
  -p:AndroidSigningKeyPass=android \
  -p:AndroidSigningKeyAlias=androiddebugkey \
  -p:AndroidSigningKeyStore="$HOME/.android/debug.keystore"

# 3. Locate the generated APK
# APK is usually at bin/Release/net10.0-android/publish/*.apk or bin/Release/net10.0-android/*.apk
APK_PATH=$(find bin/Release/net10.0-android -name "*-Signed.apk" | head -n 1)

if [ -z "$APK_PATH" ]; then
    APK_PATH=$(find bin/Release/net10.0-android -name "*.apk" | head -n 1)
fi

if [ -f "$APK_PATH" ]; then
    echo "✅ Build Successful! Found APK at: $APK_PATH"
    
    # 4. Copy to API wwwroot
    echo "📦 Hosting APK to $WWWROOT/$EXPORT_NAME..."
    cp "$APK_PATH" "$WWWROOT/$EXPORT_NAME"
    cp "$APK_PATH" "$WWWROOT/downloads/app-latest.apk"
    cp "$APK_PATH" "$WWWROOT/app-latest.apk" # Fallback for root path
    
    echo ""
    echo "🎉 DONE!"
    echo "   Download Link: http://172.20.10.2:5214/$EXPORT_NAME"
    echo "   (Make sure your API server is running with ./run_project.sh)"
else
    echo "❌ ERROR: Could not find the generated APK file. Check the build output above."
    exit 1
fi
