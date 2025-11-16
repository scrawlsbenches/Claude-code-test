#!/bin/bash
# update-docs-metrics.sh - Automated documentation metric updates
#
# This script automatically updates dynamic metrics in CLAUDE.md:
# - Test count
# - .NET SDK version
# - Project count
# - Last verified date
#
# Usage:
#   ./update-docs-metrics.sh
#
# The script will:
# 1. Detect current metrics from the project
# 2. Update CLAUDE.md with new values
# 3. Create a backup before making changes
# 4. Verify changes were successful

set -e

echo "📊 Updating CLAUDE.md metrics..."
echo ""

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Check if CLAUDE.md exists
if [ ! -f "CLAUDE.md" ]; then
    echo -e "${RED}❌ Error: CLAUDE.md not found in current directory${NC}"
    echo "Please run this script from the repository root"
    exit 1
fi

# Function to get test count
get_test_count() {
    if command -v dotnet &> /dev/null; then
        local test_output=$(dotnet test --verbosity quiet 2>&1 || echo "")
        local passed_count=$(echo "$test_output" | grep -oP "Passed:\s+\K\d+" || echo "")
        if [ -n "$passed_count" ]; then
            echo "$passed_count"
        else
            echo "UNKNOWN"
        fi
    else
        echo "UNKNOWN"
    fi
}

# Function to get .NET SDK version
get_dotnet_version() {
    if command -v dotnet &> /dev/null; then
        dotnet --version 2>/dev/null | grep -oP "^\d+\.\d+\.\d+" || echo "UNKNOWN"
    else
        echo "UNKNOWN"
    fi
}

# Function to get project count
get_project_count() {
    if [ -d "src" ]; then
        find src/ -name "*.csproj" 2>/dev/null | wc -l || echo "UNKNOWN"
    else
        echo "UNKNOWN"
    fi
}

# Get current metrics
echo "Detecting current project metrics..."
TEST_COUNT=$(get_test_count)
DOTNET_VERSION=$(get_dotnet_version)
CURRENT_DATE=$(date +%Y-%m-%d)
PROJECT_COUNT=$(get_project_count)

echo ""
echo "Detected Metrics:"
echo "  Test Count:      $TEST_COUNT"
echo "  .NET Version:    $DOTNET_VERSION"
echo "  Project Count:   $PROJECT_COUNT"
echo "  Current Date:    $CURRENT_DATE"
echo ""

# Warning if metrics couldn't be detected
if [ "$TEST_COUNT" = "UNKNOWN" ]; then
    echo -e "${YELLOW}⚠️  Warning: Could not detect test count (.NET SDK not available or tests failed)${NC}"
    echo "   Test count will not be updated"
fi

if [ "$DOTNET_VERSION" = "UNKNOWN" ]; then
    echo -e "${YELLOW}⚠️  Warning: Could not detect .NET SDK version${NC}"
    echo "   .NET version will not be updated"
fi

# Prompt for confirmation
read -p "Update CLAUDE.md with these values? (y/N) " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "Update cancelled"
    exit 0
fi

# Create backup
echo "Creating backup..."
cp CLAUDE.md CLAUDE.md.backup
echo -e "${GREEN}✅ Backup created: CLAUDE.md.backup${NC}"

# Update metrics table (Project Metrics section)
echo "Updating Project Metrics table..."

if [ "$TEST_COUNT" != "UNKNOWN" ]; then
    # Update test count in metrics table
    sed -i "s/| Total Tests | [0-9]* | All passing/| Total Tests | $TEST_COUNT | All passing/" CLAUDE.md

    # Update test count in example outputs (lines with "Passed: X")
    sed -i "s/Passed:     0, Passed:    [0-9]*, Skipped:     0, Total:    [0-9]*/Passed:     0, Passed:    $TEST_COUNT, Skipped:     0, Total:    $TEST_COUNT/" CLAUDE.md

    # Update test count in build status (80/80 tests format)
    sed -i "s/([0-9]*\/[0-9]* tests)/(${TEST_COUNT}\/${TEST_COUNT} tests)/" CLAUDE.md
fi

if [ "$DOTNET_VERSION" != "UNKNOWN" ]; then
    # Update .NET SDK version in metrics table
    sed -i "s/| .NET SDK Version | [0-9.]*  | Minimum:/| .NET SDK Version | $DOTNET_VERSION | Minimum:/" CLAUDE.md
fi

if [ "$PROJECT_COUNT" != "UNKNOWN" ]; then
    # Update project count in metrics table
    sed -i "s/| Projects in Solution | [0-9]* | [0-9]* source/| Projects in Solution | $PROJECT_COUNT | 4 source/" CLAUDE.md
fi

# Update "Last Verified" date
echo "Updating Last Verified date..."
sed -i "s/> \*\*Last Verified\*\*: [0-9-]* via/> **Last Verified**: $CURRENT_DATE via/" CLAUDE.md

# Update "Last Updated" in Repository Overview
sed -i "s/\*\*Last Updated\*\*: November [0-9]*, 2025/**Last Updated**: $(date +%B" "%d,)" 2025/" CLAUDE.md

# Verify changes were made
echo ""
echo "Verifying changes..."

changes_made=false

if [ "$TEST_COUNT" != "UNKNOWN" ] && grep -q "$TEST_COUNT" CLAUDE.md; then
    echo -e "${GREEN}✅ Test count updated to: $TEST_COUNT${NC}"
    changes_made=true
fi

if [ "$DOTNET_VERSION" != "UNKNOWN" ] && grep -q "$DOTNET_VERSION" CLAUDE.md; then
    echo -e "${GREEN}✅ .NET version updated to: $DOTNET_VERSION${NC}"
    changes_made=true
fi

if grep -q "$CURRENT_DATE" CLAUDE.md; then
    echo -e "${GREEN}✅ Last Verified date updated to: $CURRENT_DATE${NC}"
    changes_made=true
fi

if [ "$changes_made" = true ]; then
    echo ""
    echo -e "${GREEN}✅ CLAUDE.md metrics updated successfully${NC}"
    rm CLAUDE.md.backup
    echo ""
    echo "Next steps:"
    echo "  1. Review changes: git diff CLAUDE.md"
    echo "  2. Stage changes:  git add CLAUDE.md"
    echo "  3. Commit:         git commit -m 'docs: update metrics to current values'"
    echo "  4. Push:           git push"
else
    echo ""
    echo -e "${YELLOW}⚠️  No changes were made (all metrics are UNKNOWN or unchanged)${NC}"
    echo "Restoring from backup..."
    mv CLAUDE.md.backup CLAUDE.md
    exit 1
fi
