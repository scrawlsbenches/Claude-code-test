# .NET Environment Setup Skill

**Description**: Automatically sets up .NET 8.0 SDK and verifies the development environment is ready for this project.

**When to use**: At the start of any new session when working with this .NET project, or when .NET SDK is not available.

## Instructions

When this skill is invoked, perform the following steps in order:

### Step 1: Check if .NET SDK is already installed

```bash
dotnet --version
```

**If successful** (shows version 8.0.x or later):
- Skip to Step 4 (Verify Project Setup)
- Inform user: ".NET SDK already installed, skipping installation."

**If fails** (command not found):
- Proceed to Step 2

### Step 2: Install .NET SDK 8.0 (Ubuntu 24.04)

```bash
# Download Microsoft repository configuration
wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb

# Install repository (as root, no sudo needed in web environment)
dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Fix /tmp permissions to prevent GPG errors
chmod 1777 /tmp

# Update package lists
apt-get update 2>&1 | grep -v "403\|Forbidden" | tail -10

# Install .NET SDK 8.0
apt-get install -y dotnet-sdk-8.0 2>&1 | tail -20
```

**Expected time**: 30-60 seconds
**Disk space**: ~500 MB

### Step 3: Verify Installation

```bash
dotnet --version
dotnet --list-sdks
dotnet --list-runtimes
```

**Expected output**:
- Version: 8.0.121 or later
- SDKs: 8.0.xxx [/usr/lib/dotnet/sdk]
- Runtimes: Microsoft.AspNetCore.App 8.0.x, Microsoft.NETCore.App 8.0.x

### Step 4: Verify Project Setup

```bash
# Restore NuGet packages
dotnet restore

# Build solution to verify everything works
dotnet build --no-incremental 2>&1 | tail -5
```

**Expected output**:
- Build succeeded
- 0 Warning(s)
- 0 Error(s)

### Step 5: Report Status

Inform the user:
- ✅ .NET SDK version installed
- ✅ Project dependencies restored
- ✅ Build successful
- ✅ Environment ready for development

**If any step fails**:
- Report the specific error
- Suggest troubleshooting steps
- Do NOT proceed to next steps

## Success Criteria

All of the following must be true:
- ✅ `dotnet --version` shows 8.0.x or later
- ✅ `dotnet restore` completes without errors
- ✅ `dotnet build` completes with 0 errors and 0 warnings
- ✅ All project dependencies are available

## Notes

- This skill is designed for Ubuntu 24.04 (Claude Code web environment)
- For other platforms, use appropriate package managers
- Installation requires root access (available in web environment)
- Some PPA repository 403 errors are non-critical and can be ignored
