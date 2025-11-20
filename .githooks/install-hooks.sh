#!/bin/bash
# Install Git hooks for HotSwap Distributed Kernel
#
# Usage: .githooks/install-hooks.sh

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
GIT_HOOKS_DIR="$REPO_ROOT/.git/hooks"

echo "📦 Installing Git hooks..."

# Check if .git directory exists
if [ ! -d "$REPO_ROOT/.git" ]; then
    echo "❌ Not a Git repository. Run this script from the repository root."
    exit 1
fi

# Install pre-commit hook
if [ -f "$SCRIPT_DIR/pre-commit" ]; then
    echo "  ✓ Installing pre-commit hook..."
    cp "$SCRIPT_DIR/pre-commit" "$GIT_HOOKS_DIR/pre-commit"
    chmod +x "$GIT_HOOKS_DIR/pre-commit"
else
    echo "  ⚠️  pre-commit hook not found"
fi

# Install pre-push hook
if [ -f "$SCRIPT_DIR/pre-push" ]; then
    echo "  ✓ Installing pre-push hook..."
    cp "$SCRIPT_DIR/pre-push" "$GIT_HOOKS_DIR/pre-push"
    chmod +x "$GIT_HOOKS_DIR/pre-push"
else
    echo "  ⚠️  pre-push hook not found"
fi

echo ""
echo "✅ Git hooks installed successfully!"
echo ""
echo "ℹ️  Hooks installed:"
echo "   - pre-commit: Runs build and tests before each commit"
echo "   - pre-push: Validates before pushing to remote"
echo ""
echo "💡 To bypass hooks temporarily (not recommended):"
echo "   git commit --no-verify"
echo "   git push --no-verify"
echo ""
echo "📚 See .githooks/README.md for more information."
