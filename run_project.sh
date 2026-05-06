#!/bin/bash

# Configuration
DOTNET="dotnet"
BASE_DIR="/Users/dangkhoa/vinhkhanhapp/du-an-moi"
LOG_DIR="$BASE_DIR/logs"

mkdir -p "$LOG_DIR"

echo "🚀 Starting TourGuide Project System..."

# Stop any existing dotnet processes first to avoid port conflicts
echo "🧹 Cleaning up old processes..."
pkill -f "dotnet run" || true
pkill -f "TourGuideApi.dll" || true
pkill -f "TouristGuideWeb.dll" || true
sleep 2

# Start Backend API
echo "🌐 Starting TourGuideApi on http://localhost:5214..."
cd "$BASE_DIR/TourGuideApi"
$DOTNET run --urls "http://0.0.0.0:5214;https://localhost:7099" > "$LOG_DIR/api.log" 2>&1 &
API_PID=$!

# Wait for API to warm up
echo "⏳ Waiting for API to initialize..."
sleep 8

# Check if API is still running
if ! ps -p $API_PID > /dev/null; then
    echo "❌ TourGuideApi failed to start. Check $LOG_DIR/api.log"
    exit 1
fi

# Start Web UI
echo "🖥️ Starting TouristGuideWeb on http://localhost:5091..."
cd "$BASE_DIR/TouristGuideWeb"
$DOTNET run --urls "http://0.0.0.0:5091;https://localhost:7164" > "$LOG_DIR/web.log" 2>&1 &
WEB_PID=$!

sleep 5
if ! ps -p $WEB_PID > /dev/null; then
    echo "❌ TouristGuideWeb failed to start. Check $LOG_DIR/web.log"
    exit 1
fi

echo "✅ All services are running in the background."
echo "   - Web UI: http://localhost:5091"
echo "   - Backend API: http://localhost:5214"
echo "   - Logs: $LOG_DIR"
echo ""
echo "Press Ctrl+C to stop both services."

# Keep script running to maintain child processes
wait
