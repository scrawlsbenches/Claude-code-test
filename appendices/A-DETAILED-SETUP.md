# Appendix A: Detailed Development Environment Setup

**Purpose**: Comprehensive guide for setting up the Claude-code-test development environment

**Last Updated**: 2025-11-16

---

## Overview

This appendix provides step-by-step instructions for setting up your development environment to work with the Claude-code-test project. Follow these instructions if you're setting up for the first time or troubleshooting environment issues.

**Quick Setup** (if you already have .NET 8.0 SDK):
```bash
git clone https://github.com/scrawlsbenches/Claude-code-test.git
cd Claude-code-test
dotnet restore && dotnet build && dotnet test
```

**Full Setup**: Continue reading for detailed instructions.

---

## Prerequisites Installation

### Required Software

#### 1. .NET 8.0 SDK (Required)

**The project requires .NET 8.0 SDK or later.**

##### Windows Installation

```powershell
# Option 1: Download from Microsoft
# https://dotnet.microsoft.com/download/dotnet/8.0

# Option 2: Using winget (recommended)
winget install Microsoft.DotNet.SDK.8

# Verify installation
dotnet --version
# Expected output: 8.0.x or later
```

##### Linux Installation (Ubuntu/Debian)

**Standard Installation (Ubuntu 22.04)**:
```bash
# Step 1: Download Microsoft package repository
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb

# Step 2: Install repository configuration
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Step 3: Update and install
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0

# Step 4: Verify installation
dotnet --version
# Expected: 8.0.x or later
```

**Claude Code Web Environment (Ubuntu 24.04)**:

If you're using Claude Code in a web environment with root access:

```bash
# Step 1: Download repository configuration for Ubuntu 24.04
wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb

# Step 2: Install (as root, no sudo needed)
dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Step 3: Fix /tmp permissions (prevents GPG errors)
chmod 1777 /tmp

# Step 4: Update package lists
apt-get update
# Note: You may see 403 Forbidden errors from PPA repos - these are non-critical

# Step 5: Install .NET SDK 8.0
apt-get install -y dotnet-sdk-8.0

# Step 6: Verify installation
dotnet --version          # Should show: 8.0.121 or later
dotnet --list-sdks        # Should show: 8.0.121 [/usr/lib/dotnet/sdk]
dotnet --list-runtimes    # Should show ASP.NET Core and .NET Core runtimes
```

**Installation time**: ~30-60 seconds
**Disk space required**: ~500 MB

##### macOS Installation

```bash
# Option 1: Using Homebrew (recommended)
brew install dotnet@8

# Option 2: Download from Microsoft
# https://dotnet.microsoft.com/download/dotnet/8.0

# Verify installation
dotnet --version
# Expected: 8.0.x or later
```

##### Verify .NET SDK Installation

After installing on any platform:

```bash
# Check version
dotnet --version
# Expected: 8.0.x or later

# List installed SDKs
dotnet --list-sdks
# Expected: 8.0.xxx [path]

# List installed runtimes
dotnet --list-runtimes
# Expected:
#   Microsoft.AspNetCore.App 8.0.x [path]
#   Microsoft.NETCore.App 8.0.x [path]
```

---

#### 2. Git (Required)

Ensure Git is installed for version control.

**Check if installed**:
```bash
git --version
# If shows version → Already installed
# If error → Follow installation below
```

**Installation**:
- **Windows**: Download from https://git-scm.com/download/win
- **Linux**: `sudo apt-get install git`
- **macOS**: `brew install git` or use Xcode Command Line Tools

---

#### 3. Docker (Optional - for containerized deployment)

**Windows/macOS**:
- Download Docker Desktop: https://www.docker.com/products/docker-desktop

**Linux (Ubuntu/Debian)**:
```bash
# Install Docker and Docker Compose
sudo apt-get update
sudo apt-get install -y docker.io docker-compose

# Add user to docker group (avoids sudo for docker commands)
sudo usermod -aG docker $USER

# Logout and login for group changes to take effect

# Verify installation
docker --version
docker-compose --version
```

---

### Verify All Prerequisites

Run these commands to verify everything is installed correctly:

```bash
# .NET SDK
dotnet --version                  # Should show 8.0.x or later
dotnet --list-sdks                # Should list installed SDKs
dotnet --list-runtimes            # Should list ASP.NET Core and .NET Core

# Git
git --version                     # Should show git version

# Docker (optional)
docker --version                  # If installed, shows version
docker-compose --version          # If installed, shows version
```

**If any command fails**, revisit the installation steps for that tool.

---

## Clone the Repository

```bash
# Clone from GitHub
git clone https://github.com/scrawlsbenches/Claude-code-test.git

# Navigate into directory
cd Claude-code-test

# Verify you're in the correct directory
ls -la
# Should see: src/, tests/, examples/, CLAUDE.md, README.md, etc.
```

---

## Install Project Dependencies

### Restore NuGet Packages

The project uses several NuGet packages that must be restored before building:

```bash
# Restore all project dependencies
dotnet restore

# Expected output (shortened):
#   Determining projects to restore...
#   Restored /path/to/HotSwap.Distributed.Domain/HotSwap.Distributed.Domain.csproj
#   Restored /path/to/HotSwap.Distributed.Infrastructure/HotSwap.Distributed.Infrastructure.csproj
#   Restored /path/to/HotSwap.Distributed.Orchestrator/HotSwap.Distributed.Orchestrator.csproj
#   Restored /path/to/HotSwap.Distributed.Api/HotSwap.Distributed.Api.csproj
#   Restored /path/to/HotSwap.Distributed.Tests/HotSwap.Distributed.Tests.csproj
#   Restored /path/to/ApiUsageExample/ApiUsageExample.csproj
```

### List Installed Packages

To see what packages are installed:

```bash
# List all packages in solution
dotnet list package

# Check for outdated packages
dotnet list package --outdated

# Check for vulnerable packages
dotnet list package --vulnerable
```

---

## First-Time Build and Test

**After installing .NET SDK for the first time**, run these commands in order:

### Step 1: Clean Build Artifacts

```bash
dotnet clean

# Expected output:
#   Build succeeded.
#       0 Warning(s)
#       0 Error(s)
#   Time Elapsed 00:00:02.30
```

### Step 2: Restore Packages

```bash
dotnet restore

# Expected output:
#   Determining projects to restore...
#   Restored /path/to/... (6 projects)
```

### Step 3: Build Solution

```bash
# Non-incremental build for first time
dotnet build --no-incremental

# Expected output:
#   Build succeeded.
#       0 Warning(s)
#       0 Error(s)
#   Time Elapsed 00:00:18.04
```

**If build fails**, see [Troubleshooting](#troubleshooting-setup-issues) section below.

### Step 4: Run All Tests

```bash
dotnet test

# Expected output:
#   Passed!  - Failed:     0, Passed:    80, Skipped:     0, Total:    80, Duration: 10 s
```

**Important Notes**:
- Build should have **0 warnings and 0 errors** (clean build)
- Current test count: **80 passing tests** (may increase as tests are added)
- Total setup time: approximately **20-30 seconds** after SDK installation

---

## Building the Project

### Build Entire Solution

```bash
# Debug build (default)
dotnet build

# Release build (optimized for production)
dotnet build -c Release

# Verbose output (for troubleshooting)
dotnet build -v detailed

# Expected output:
#   Build succeeded.
#       0 Warning(s)
#       0 Error(s)
```

### Build Specific Projects

```bash
# Build only API project
dotnet build src/HotSwap.Distributed.Api/HotSwap.Distributed.Api.csproj

# Build only Orchestrator
dotnet build src/HotSwap.Distributed.Orchestrator/HotSwap.Distributed.Orchestrator.csproj

# Build only Domain
dotnet build src/HotSwap.Distributed.Domain/HotSwap.Distributed.Domain.csproj

# Build only Tests
dotnet build tests/HotSwap.Distributed.Tests/HotSwap.Distributed.Tests.csproj

# Build only Examples
dotnet build examples/ApiUsageExample/ApiUsageExample.csproj
```

### Clean and Rebuild

If you encounter build issues:

```bash
# Clean all build artifacts
dotnet clean

# Clean and restore
dotnet clean && dotnet restore

# Clean, restore, and rebuild
dotnet clean && dotnet restore && dotnet build

# Force complete rebuild (non-incremental)
dotnet build --no-incremental
```

---

## Running Tests

### Run All Tests

```bash
# Run all tests in solution
dotnet test

# Expected output:
#   Passed!  - Failed:     0, Passed:    80, Skipped:     0, Total:    80

# Run with detailed output
dotnet test --verbosity normal

# Run with minimal output
dotnet test --verbosity quiet
```

### Run Specific Tests

```bash
# Run only tests in specific project
dotnet test tests/HotSwap.Distributed.Tests/HotSwap.Distributed.Tests.csproj

# Run tests matching filter
dotnet test --filter "FullyQualifiedName~DeploymentPipeline"
dotnet test --filter "FullyQualifiedName~JwtToken"
dotnet test --filter "ClassName~UserAuth"
```

### Test Coverage

```bash
# Run tests with code coverage
dotnet test --collect:"XPlat Code Coverage"

# Coverage reports saved to:
# tests/HotSwap.Distributed.Tests/TestResults/{guid}/coverage.cobertura.xml

# Current coverage: 85%+
```

### Validation Scripts

```bash
# Run critical path tests
./test-critical-paths.sh

# Expected output:
#   ✓ All 80 critical path tests passed

# Run code validation
./validate-code.sh

# Checks:
# - Build succeeds
# - Tests pass
# - No obvious code issues
```

---

## Running the Application

### Run API Locally

```bash
# Run the API project
dotnet run --project src/HotSwap.Distributed.Api/HotSwap.Distributed.Api.csproj

# API will be available at:
#   - Base URL: http://localhost:5000
#   - Swagger UI: http://localhost:5000/swagger
#   - Health check: http://localhost:5000/health

# Press Ctrl+C to stop
```

### Run with Custom Port

```bash
# Run on different port
dotnet run --project src/HotSwap.Distributed.Api/HotSwap.Distributed.Api.csproj --urls "http://localhost:5001"

# Or set in environment variable
export ASPNETCORE_URLS="http://localhost:5001"
dotnet run --project src/HotSwap.Distributed.Api/HotSwap.Distributed.Api.csproj
```

### Run with Docker Compose

```bash
# Build and start all services
docker-compose up -d

# View logs
docker-compose logs -f orchestrator-api

# Check service status
docker-compose ps

# Stop all services
docker-compose down

# Stop and remove volumes
docker-compose down -v
```

**Available Services** (when using Docker Compose):
- **API**: http://localhost:5000
- **Swagger UI**: http://localhost:5000/swagger
- **Jaeger UI**: http://localhost:16686 (distributed tracing)
- **Health check**: http://localhost:5000/health

### Run API Usage Examples

```bash
# Navigate to examples
cd examples/ApiUsageExample

# Ensure API is running first (in another terminal)
# Then run examples:
dotnet run

# Or use convenience script
./run-example.sh

# Run with custom API URL
dotnet run -- http://localhost:5001
./run-example.sh http://localhost:5001
```

---

## Development Workflow

### Typical Development Session

```bash
# 1. Pull latest changes from main
git pull origin main

# 2. Create feature branch
git checkout -b claude/your-feature-name-sessionid

# 3. Restore dependencies (if first time or packages changed)
dotnet restore

# 4. Build solution
dotnet build

# 5. Run tests to ensure baseline works
dotnet test

# 6. Make your code changes...
#    - Follow TDD workflow (write tests first)
#    - Implement features
#    - Refactor for quality

# 7. Build and test after changes
dotnet build
dotnet test

# 8. Run validation scripts
./validate-code.sh
./test-critical-paths.sh

# 9. Commit changes (after pre-commit checklist)
git add .
git commit -m "feat: your feature description"

# 10. Push to remote
git push -u origin claude/your-feature-name-sessionid
```

---

## Troubleshooting Setup Issues

### .NET SDK Not Found

**Error**: `dotnet: command not found`

**Solution**: Ensure .NET SDK is in your PATH

**Windows**:
1. Add to PATH: `C:\Program Files\dotnet`
2. Restart terminal/IDE

**Linux/macOS**:
```bash
# Add to ~/.bashrc or ~/.zshrc
export PATH="$PATH:/usr/local/share/dotnet"

# Or for system-wide installation
export PATH="$PATH:/usr/share/dotnet"

# Reload shell
source ~/.bashrc  # or source ~/.zshrc
```

---

### Installing .NET SDK on Linux (Root/sudo Issues)

If you encounter permission errors:

```bash
# Step 1: Download repository configuration
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb

# Step 2: Install repository
# If running as root (uid=0):
dpkg -i packages-microsoft-prod.deb

# If running as regular user:
sudo dpkg -i packages-microsoft-prod.deb

# Step 3: Clean up
rm packages-microsoft-prod.deb

# Step 4: Fix /tmp permissions if GPG errors occur
chmod 1777 /tmp

# Step 5: Update packages
# As root:
apt-get update

# As regular user:
sudo apt-get update

# Step 6: Install SDK
# As root:
apt-get install -y dotnet-sdk-8.0

# As regular user:
sudo apt-get install -y dotnet-sdk-8.0

# Step 7: Verify
dotnet --version
dotnet --list-sdks
```

**Common Installation Errors**:
1. **403 Forbidden from PPA repos** - Non-critical, won't prevent SDK installation
2. **GPG/temporary file errors** - Fix with `chmod 1777 /tmp`
3. **sudo.conf ownership errors** - Run as root directly instead of using sudo

---

### Package Restore Fails

**Error**: `Unable to load the service index for source https://api.nuget.org/v3/index.json`

**Solution**: Clear NuGet cache and retry

```bash
# Clear all NuGet caches
dotnet nuget locals all --clear

# Retry restore
dotnet restore

# If still fails, check network connectivity
ping api.nuget.org
```

---

### Build Errors After Git Pull

**Error**: Various build errors after pulling latest changes

**Solution**: Clean and rebuild

```bash
# Step 1: Clean
dotnet clean

# Step 2: Restore (in case packages changed)
dotnet restore

# Step 3: Rebuild
dotnet build

# If still fails, try non-incremental build
dotnet build --no-incremental
```

---

### Port Already in Use

**Error**: `Address already in use: http://localhost:5000`

**Solution**: Kill process or use different port

```bash
# Find process using port 5000
# Linux/macOS:
lsof -i :5000

# Windows:
netstat -ano | findstr "5000"

# Kill the process (Linux/macOS)
kill -9 <PID>

# Or run on different port
dotnet run --project src/HotSwap.Distributed.Api/ --urls "http://localhost:5001"
```

---

### Docker Permission Denied

**Error**: `Permission denied while trying to connect to Docker daemon socket`

**Solution**: Add user to docker group (Linux)

```bash
# Add current user to docker group
sudo usermod -aG docker $USER

# Logout and login for changes to take effect
# Or run:
newgrp docker

# Verify
docker ps
# Should work without sudo
```

---

### Test Failures After Fresh Clone

**Error**: Tests fail immediately after cloning

**Possible causes and solutions**:

1. **Packages not restored**:
   ```bash
   dotnet restore
   dotnet build
   dotnet test
   ```

2. **Stale build artifacts**:
   ```bash
   dotnet clean
   dotnet build
   dotnet test
   ```

3. **Wrong .NET SDK version**:
   ```bash
   dotnet --version
   # Should be 8.0.x or later
   # If not, update SDK
   ```

---

### Build Warnings

**Warning**: "Missing XML comment for publicly visible type or member"

**Solution**: This is informational, not an error. To fix:

```bash
# Option 1: Add XML documentation comments to public members
/// <summary>
/// Description of the class
/// </summary>
public class MyClass { }

# Option 2: Disable warning in .csproj (not recommended)
<NoWarn>$(NoWarn);CS1591</NoWarn>
```

---

## IDE Setup (Optional)

### Visual Studio Code

**Recommended Extensions**:
- C# (Microsoft)
- C# Dev Kit (Microsoft)
- .NET Runtime Install Tool
- GitLens

**Install**:
1. Download VS Code: https://code.visualstudio.com/
2. Install C# extension
3. Open project folder
4. Trust workspace when prompted

**Run/Debug**:
- Press F5 to start debugging
- Or use terminal: `dotnet run --project src/HotSwap.Distributed.Api/`

### Visual Studio 2022

**Requirements**:
- Visual Studio 2022 (Community, Professional, or Enterprise)
- .NET Desktop Development workload

**Open Project**:
1. Open `DistributedKernel.sln`
2. Set `HotSwap.Distributed.Api` as startup project
3. Press F5 to run

### JetBrains Rider

**Requirements**:
- JetBrains Rider 2023.3 or later

**Open Project**:
1. Open `DistributedKernel.sln`
2. Configure run configuration for API project
3. Run/Debug from toolbar

---

## Next Steps

After successful setup:

1. **Read core documentation**:
   - [Main CLAUDE.md](../CLAUDE.md) - Full AI assistant guide
   - [README.md](../README.md) - Project overview
   - [TESTING.md](../TESTING.md) - Testing guide

2. **Review workflows**:
   - [Pre-Commit Checklist](../workflows/pre-commit-checklist.md)
   - [TDD Workflow](../workflows/tdd-workflow.md)
   - [Git Workflow](../workflows/git-workflow.md)

3. **Explore templates**:
   - [Test Template](../templates/test-template.cs)
   - [Service Template](../templates/service-template.cs)
   - [Controller Template](../templates/controller-template.cs)

4. **Start developing**:
   - Follow TDD workflow
   - Run pre-commit checklist before every commit
   - Push to feature branches (claude/name-sessionid)

---

## See Also

- [Pre-Commit Checklist](../workflows/pre-commit-checklist.md) - Run before every commit
- [Troubleshooting](E-TROUBLESHOOTING.md) - Comprehensive troubleshooting guide
- [No SDK Checklist](B-NO-SDK-CHECKLIST.md) - For environments without .NET SDK

**Back to**: [Main CLAUDE.md](../CLAUDE.md)
