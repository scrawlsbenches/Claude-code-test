#!/bin/bash

#
# Smoke Test Script for Distributed Kernel Orchestration API
#
# This script runs smoke tests to verify the API is functioning correctly.
# Smoke tests are quick (<60s) and test critical paths only.
#
# Usage:
#   ./run-smoke-tests.sh [API_URL]
#
# Examples:
#   ./run-smoke-tests.sh                          # Test localhost:5000
#   ./run-smoke-tests.sh http://localhost:5001    # Test custom URL
#   ./run-smoke-tests.sh https://api.example.com  # Test remote API
#

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Parse arguments
API_URL="${1:-http://localhost:5000}"

echo "╔══════════════════════════════════════════════════════════════╗"
echo "║   Distributed Kernel Orchestration API - Smoke Tests        ║"
echo "╚══════════════════════════════════════════════════════════════╝"
echo ""
echo "API URL:    $API_URL"
echo "Started:    $(date '+%Y-%m-%d %H:%M:%S')"
echo ""

# Check if .NET SDK is available
if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}❌ Error: .NET 8 SDK not found${NC}"
    echo ""
    echo "Please install .NET 8 SDK:"
    echo "  - Windows: winget install Microsoft.DotNet.SDK.8"
    echo "  - Linux:   See CLAUDE.md for installation instructions"
    echo "  - macOS:   brew install dotnet@8"
    echo ""
    exit 1
fi

# Check .NET version
DOTNET_VERSION=$(dotnet --version | cut -d'.' -f1)
if [ "$DOTNET_VERSION" -lt 8 ]; then
    echo -e "${YELLOW}⚠️  Warning: .NET SDK version is < 8.0. Some features may not work.${NC}"
    echo ""
fi

# Build smoke tests
echo "Building smoke tests..."
cd tests/HotSwap.Distributed.SmokeTests
dotnet build --configuration Release --nologo --verbosity quiet

if [ $? -ne 0 ]; then
    echo -e "${RED}❌ Failed to build smoke tests${NC}"
    exit 1
fi

echo "Build succeeded!"
echo ""

# Run smoke tests
echo "Running smoke tests against: $API_URL"
echo ""

dotnet run --configuration Release --no-build -- "$API_URL"

EXIT_CODE=$?

echo ""
if [ $EXIT_CODE -eq 0 ]; then
    echo -e "${GREEN}✅ All smoke tests passed!${NC}"
else
    echo -e "${RED}❌ Some smoke tests failed${NC}"
fi

exit $EXIT_CODE
