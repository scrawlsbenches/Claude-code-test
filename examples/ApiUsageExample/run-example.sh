#!/bin/bash

# Distributed Kernel Orchestration API - Example Runner
# Runs the comprehensive API usage examples

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Default API URL
API_URL="${1:-http://localhost:5000}"

echo -e "${BLUE}╔═══════════════════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║   Distributed Kernel Orchestration API - Example Runner          ║${NC}"
echo -e "${BLUE}╚═══════════════════════════════════════════════════════════════════╝${NC}"
echo ""

# Check if .NET SDK is installed
if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}✗ .NET SDK not found!${NC}"
    echo ""
    echo "Please install .NET 8.0 SDK or later:"
    echo "  https://dotnet.microsoft.com/download"
    exit 1
fi

echo -e "${GREEN}✓ .NET SDK found:${NC} $(dotnet --version)"
echo ""

# Check if API is accessible
echo -e "${YELLOW}Checking API health...${NC}"
if curl -s -f "${API_URL}/health" > /dev/null 2>&1; then
    echo -e "${GREEN}✓ API is healthy at ${API_URL}${NC}"
else
    echo -e "${RED}✗ API is not accessible at ${API_URL}${NC}"
    echo ""
    echo "Please ensure the API is running:"
    echo "  cd ../.."
    echo "  docker-compose up -d"
    echo "  # or"
    echo "  dotnet run --project src/HotSwap.Distributed.Api"
    echo ""
    echo -e "${YELLOW}Continue anyway? (y/N)${NC}"
    read -r response
    if [[ ! "$response" =~ ^[Yy]$ ]]; then
        exit 1
    fi
fi
echo ""

# Build the project
echo -e "${YELLOW}Building example project...${NC}"
if dotnet build --configuration Release > /dev/null 2>&1; then
    echo -e "${GREEN}✓ Build successful${NC}"
else
    echo -e "${RED}✗ Build failed${NC}"
    echo ""
    echo "Please check the build errors:"
    echo "  dotnet build"
    exit 1
fi
echo ""

# Run the examples
echo -e "${YELLOW}Running examples...${NC}"
echo ""
echo -e "${BLUE}═══════════════════════════════════════════════════════════════════${NC}"
echo ""

dotnet run --configuration Release --no-build -- "${API_URL}"

exit_code=$?

echo ""
echo -e "${BLUE}═══════════════════════════════════════════════════════════════════${NC}"
echo ""

if [ $exit_code -eq 0 ]; then
    echo -e "${GREEN}✓ All examples completed successfully!${NC}"
else
    echo -e "${RED}✗ Examples failed with exit code ${exit_code}${NC}"
fi

exit $exit_code
