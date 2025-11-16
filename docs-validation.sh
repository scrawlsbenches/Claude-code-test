#!/bin/bash
# docs-validation.sh - Validates documentation freshness and accuracy
#
# This script checks for common documentation issues:
# - Stale test counts
# - Undocumented packages
# - Broken file references
# - Outdated "Last Updated" dates
# - Broken internal links
#
# Usage:
#   ./docs-validation.sh
#
# Exit codes:
#   0 - All validations passed
#   1 - Validation failures detected

set -e

echo "🔍 Validating documentation..."
echo ""

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

ERRORS=0
WARNINGS=0

# Function to install .NET SDK if not present
install_dotnet_if_needed() {
    if command -v dotnet &> /dev/null; then
        echo "✅ .NET SDK already installed (version $(dotnet --version))"
        return 0
    fi

    echo "📦 .NET SDK not found, attempting to install..."

    # Check if running as root or with sudo
    if [ "$EUID" -ne 0 ]; then
        echo -e "${YELLOW}⚠️  This script needs root privileges to install .NET SDK${NC}"
        echo "Please run: sudo $0"
        exit 1
    fi

    # Install for Ubuntu 24.04
    if [ -f /etc/os-release ]; then
        . /etc/os-release
        if [ "$ID" = "ubuntu" ] && [ "$VERSION_ID" = "24.04" ]; then
            echo "Installing .NET SDK 8.0 for Ubuntu 24.04..."
            wget -q https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
            dpkg -i packages-microsoft-prod.deb
            rm packages-microsoft-prod.deb
            chmod 1777 /tmp
            apt-get update -qq
            apt-get install -y dotnet-sdk-8.0

            if command -v dotnet &> /dev/null; then
                echo -e "${GREEN}✅ .NET SDK 8.0 installed successfully (version $(dotnet --version))${NC}"
                return 0
            else
                echo -e "${RED}❌ Failed to install .NET SDK${NC}"
                exit 1
            fi
        fi
    fi

    echo -e "${YELLOW}⚠️  .NET SDK installation not supported for this OS${NC}"
    echo "Please install .NET SDK 8.0 manually: https://dotnet.microsoft.com/download"
    exit 1
}

# Install .NET SDK if needed
install_dotnet_if_needed

# Check if CLAUDE.md exists
if [ ! -f "CLAUDE.md" ]; then
    echo -e "${RED}❌ Error: CLAUDE.md not found${NC}"
    exit 1
fi

echo "📋 Running validation checks..."
echo ""

# ============================================
# Check 1: Verify test counts match actual
# ============================================
echo "1️⃣  Checking test count accuracy..."

if command -v dotnet &> /dev/null; then
    ACTUAL_TESTS=$(dotnet test --verbosity quiet 2>&1 | grep -oP "Passed:\s+\K\d+" || echo "UNKNOWN")

    if [ "$ACTUAL_TESTS" != "UNKNOWN" ]; then
        # Extract documented test count from metrics table
        DOCUMENTED_TESTS=$(grep "Total Tests" CLAUDE.md | grep -oP "\| \K\d+" | head -1 || echo "UNKNOWN")

        if [ "$DOCUMENTED_TESTS" != "UNKNOWN" ]; then
            if [ "$ACTUAL_TESTS" = "$DOCUMENTED_TESTS" ]; then
                echo -e "   ${GREEN}✅ Test count matches: $ACTUAL_TESTS tests${NC}"
            else
                echo -e "   ${RED}❌ Test count mismatch:${NC}"
                echo "      Actual:      $ACTUAL_TESTS tests"
                echo "      Documented:  $DOCUMENTED_TESTS tests"
                echo "      Fix: Run ./update-docs-metrics.sh"
                ((ERRORS++))
            fi
        else
            echo -e "   ${YELLOW}⚠️  Could not find documented test count${NC}"
            ((WARNINGS++))
        fi
    else
        echo -e "   ${YELLOW}⚠️  Could not determine actual test count (.NET SDK not available)${NC}"
        ((WARNINGS++))
    fi
else
    echo -e "   ${YELLOW}⚠️  .NET SDK not available, skipping test count check${NC}"
    ((WARNINGS++))
fi

# ============================================
# Check 2: Verify package versions documented
# ============================================
echo ""
echo "2️⃣  Checking package documentation..."

if command -v dotnet &> /dev/null; then
    UNDOCUMENTED_PACKAGES=$(dotnet list package 2>/dev/null | grep ">" | awk '{print $2}' | while read pkg; do
        if ! grep -q "$pkg" CLAUDE.md; then
            echo "$pkg"
        fi
    done || echo "")

    if [ -n "$UNDOCUMENTED_PACKAGES" ]; then
        echo -e "   ${YELLOW}⚠️  Undocumented packages found:${NC}"
        echo "$UNDOCUMENTED_PACKAGES" | while read pkg; do
            echo "      - $pkg"
        done
        echo "      Fix: Add to Technology Stack section in CLAUDE.md"
        ((WARNINGS++))
    else
        echo -e "   ${GREEN}✅ All packages documented${NC}"
    fi
else
    echo -e "   ${YELLOW}⚠️  .NET SDK not available, skipping package check${NC}"
    ((WARNINGS++))
fi

# ============================================
# Check 3: Verify "Last Updated" is recent
# ============================================
echo ""
echo "3️⃣  Checking Last Updated date..."

LAST_UPDATED=$(grep "Last Updated:" CLAUDE.md | head -1 | grep -oP "\d{4}-\d{2}-\d{2}" || echo "")

if [ -n "$LAST_UPDATED" ]; then
    CURRENT_EPOCH=$(date +%s)
    UPDATED_EPOCH=$(date -d "$LAST_UPDATED" +%s 2>/dev/null || echo "0")

    if [ "$UPDATED_EPOCH" != "0" ]; then
        DAYS_OLD=$(( ($CURRENT_EPOCH - $UPDATED_EPOCH) / 86400 ))

        if [ $DAYS_OLD -lt 30 ]; then
            echo -e "   ${GREEN}✅ Last Updated is recent: $LAST_UPDATED ($DAYS_OLD days ago)${NC}"
        elif [ $DAYS_OLD -lt 90 ]; then
            echo -e "   ${YELLOW}⚠️  Last Updated: $LAST_UPDATED ($DAYS_OLD days ago)${NC}"
            echo "      Consider reviewing documentation for accuracy"
            ((WARNINGS++))
        else
            echo -e "   ${RED}❌ Last Updated: $LAST_UPDATED ($DAYS_OLD days ago)${NC}"
            echo "      Documentation is stale, please review and update"
            ((ERRORS++))
        fi
    else
        echo -e "   ${YELLOW}⚠️  Could not parse Last Updated date${NC}"
        ((WARNINGS++))
    fi
else
    echo -e "   ${RED}❌ Last Updated date not found${NC}"
    ((ERRORS++))
fi

# ============================================
# Check 4: Verify file references exist
# ============================================
echo ""
echo "4️⃣  Checking file references..."

BROKEN_REFS=0
grep -oP "src/[^\s\)]*\.cs(?:proj)?" CLAUDE.md 2>/dev/null | sort -u | while read file_ref; do
    if [ ! -f "$file_ref" ]; then
        if [ $BROKEN_REFS -eq 0 ]; then
            echo -e "   ${RED}❌ Broken file references:${NC}"
        fi
        echo "      - $file_ref"
        ((BROKEN_REFS++))
    fi
done

if [ $BROKEN_REFS -eq 0 ]; then
    echo -e "   ${GREEN}✅ All file references valid${NC}"
else
    ((ERRORS++))
fi

# ============================================
# Check 5: Check for TODO/FIXME in docs
# ============================================
echo ""
echo "5️⃣  Checking for unresolved TODOs..."

TODOS=$(grep -n "TODO\|FIXME" CLAUDE.md || echo "")

if [ -n "$TODOS" ]; then
    echo -e "   ${YELLOW}⚠️  Unresolved TODOs found:${NC}"
    echo "$TODOS" | while read line; do
        echo "      $line"
    done
    ((WARNINGS++))
else
    echo -e "   ${GREEN}✅ No unresolved TODOs${NC}"
fi

# ============================================
# Check 6: Verify internal links (basic check)
# ============================================
echo ""
echo "6️⃣  Checking internal links..."

# Extract section headers (lines starting with ## or ###)
SECTIONS=$(grep -oP "^##+ \K.*" CLAUDE.md | while read section; do
    # Convert to anchor format (lowercase, replace spaces with -, remove special chars)
    echo "$section" | tr '[:upper:]' '[:lower:]' | sed 's/[^a-z0-9 -]//g' | sed 's/ /-/g' | sed 's/--*/-/g'
done)

# Extract internal links (format: [text](#anchor))
LINKS=$(grep -oP "\]\(#[^\)]+\)" CLAUDE.md | sed 's/](#//; s/)$//' || echo "")

BROKEN_LINKS=0
if [ -n "$LINKS" ]; then
    echo "$LINKS" | while read link; do
        if ! echo "$SECTIONS" | grep -qF "$link"; then
            if [ $BROKEN_LINKS -eq 0 ]; then
                echo -e "   ${YELLOW}⚠️  Potential broken internal links:${NC}"
            fi
            echo "      - #$link"
            ((BROKEN_LINKS++))
        fi
    done
fi

if [ $BROKEN_LINKS -eq 0 ]; then
    echo -e "   ${GREEN}✅ Internal links appear valid${NC}"
else
    ((WARNINGS++))
fi

# ============================================
# Summary
# ============================================
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "📊 Validation Summary"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

if [ $ERRORS -eq 0 ] && [ $WARNINGS -eq 0 ]; then
    echo -e "${GREEN}✅ All validations passed!${NC}"
    echo ""
    echo "Documentation is accurate and up-to-date."
    exit 0
elif [ $ERRORS -eq 0 ]; then
    echo -e "${YELLOW}⚠️  Warnings: $WARNINGS${NC}"
    echo -e "${GREEN}✅ Errors: 0${NC}"
    echo ""
    echo "Documentation is valid but has minor warnings."
    echo "Consider addressing warnings to improve documentation quality."
    exit 0
else
    echo -e "${RED}❌ Errors: $ERRORS${NC}"
    echo -e "${YELLOW}⚠️  Warnings: $WARNINGS${NC}"
    echo ""
    echo "Documentation has critical errors that should be fixed."
    echo ""
    echo "Suggested fixes:"
    if [ $ERRORS -gt 0 ]; then
        echo "  1. Run: ./update-docs-metrics.sh (to fix stale metrics)"
        echo "  2. Review and fix broken file references"
        echo "  3. Update Last Updated date to current date"
    fi
    exit 1
fi
