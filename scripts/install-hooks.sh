#!/bin/bash
# install-hooks.sh - Installs git hooks from scripts/hooks/ to .git/hooks/

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
HOOKS_SOURCE="$SCRIPT_DIR/hooks"
HOOKS_TARGET="$REPO_ROOT/.git/hooks"

echo "🔧 Installing git hooks..."
echo ""

# Check if .git/hooks directory exists
if [ ! -d "$HOOKS_TARGET" ]; then
    echo "❌ Error: .git/hooks directory not found"
    echo "   Are you running this from the repository root?"
    exit 1
fi

# Check if source hooks directory exists
if [ ! -d "$HOOKS_SOURCE" ]; then
    echo "❌ Error: scripts/hooks directory not found"
    exit 1
fi

# Install hooks
HOOKS_INSTALLED=0
for hook in "$HOOKS_SOURCE"/*; do
    # Skip README and non-executable files
    if [ -f "$hook" ] && [ "$(basename "$hook")" != "README.md" ]; then
        HOOK_NAME=$(basename "$hook")
        TARGET_PATH="$HOOKS_TARGET/$HOOK_NAME"

        # Copy hook
        cp "$hook" "$TARGET_PATH"
        chmod +x "$TARGET_PATH"

        echo "✅ Installed: $HOOK_NAME"
        HOOKS_INSTALLED=$((HOOKS_INSTALLED + 1))
    fi
done

echo ""
if [ $HOOKS_INSTALLED -eq 0 ]; then
    echo "⚠️  No hooks found to install"
else
    echo "✨ Successfully installed $HOOKS_INSTALLED hook(s)"
    echo ""
    echo "Installed hooks:"
    ls -1 "$HOOKS_TARGET" | grep -v "\.sample$" || echo "  (none)"
fi

echo ""
echo "To test the pre-commit hook, run:"
echo "  .git/hooks/pre-commit"
