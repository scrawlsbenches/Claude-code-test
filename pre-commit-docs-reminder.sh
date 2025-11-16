#!/bin/bash
# pre-commit-docs-reminder.sh - Pre-commit hook for documentation maintenance
#
# This script should be added to .git/hooks/pre-commit or used with git hooks managers.
# It reminds developers to update documentation when tests or packages change.
#
# Installation:
#   Option 1: Direct installation
#     cp pre-commit-docs-reminder.sh .git/hooks/pre-commit
#     chmod +x .git/hooks/pre-commit
#
#   Option 2: Append to existing pre-commit hook
#     cat pre-commit-docs-reminder.sh >> .git/hooks/pre-commit
#
#   Option 3: Use with Husky or other hook manager
#     Add this script to your hook configuration

# Colors for output
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

REMIND=false
MESSAGES=()

# ============================================
# Check 1: Test files added/removed
# ============================================
if git diff --cached tests/ | grep -qE "^\+.*\[Fact\]|^\+.*\[Theory\]|^-.*\[Fact\]|^-.*\[Theory\]"; then
    REMIND=true
    MESSAGES+=("Tests were added/removed")
fi

# ============================================
# Check 2: Package references changed
# ============================================
if git diff --cached | grep -qE "^\+.*PackageReference|^-.*PackageReference"; then
    REMIND=true
    MESSAGES+=("NuGet packages were added/removed")
fi

# ============================================
# Check 3: Project files added/removed
# ============================================
if git diff --cached --name-status | grep -qE "^A.*\.csproj$|^D.*\.csproj$"; then
    REMIND=true
    MESSAGES+=("Project files (.csproj) were added/removed")
fi

# ============================================
# Check 4: Technology stack changes
# ============================================
if git diff --cached | grep -qE "^\+.*TargetFramework|^-.*TargetFramework"; then
    REMIND=true
    MESSAGES+=(".NET target framework changed")
fi

# ============================================
# Display reminder if needed
# ============================================
if [ "$REMIND" = true ]; then
    echo ""
    echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${YELLOW}⚠️  Documentation Update Reminder${NC}"
    echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo ""
    echo "The following changes may require documentation updates:"
    for msg in "${MESSAGES[@]}"; do
        echo "  • $msg"
    done
    echo ""
    echo -e "${CYAN}Recommended actions:${NC}"
    echo "  1. Run: ./update-docs-metrics.sh"
    echo "  2. Update CLAUDE.md Technology Stack if packages changed"
    echo "  3. Update README.md if user-facing changes were made"
    echo "  4. Stage updated docs: git add CLAUDE.md README.md"
    echo ""
    echo -e "${CYAN}Quick verification:${NC}"
    echo "  • Check test count: dotnet test | grep 'Passed'"
    echo "  • Validate docs:    ./docs-validation.sh"
    echo ""
    echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo ""

    # Optional: Prompt user to continue
    # Uncomment the following lines to make the hook interactive
    # read -p "Continue with commit? (y/N) " -n 1 -r
    # echo
    # if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    #     echo "Commit cancelled. Please update documentation first."
    #     exit 1
    # fi

    # Non-blocking reminder (default behavior)
    # Allow commit to proceed, but remind developer
fi

# Exit with success (allow commit)
exit 0
