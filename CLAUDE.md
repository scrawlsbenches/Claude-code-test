# CLAUDE.md - AI Assistant Guide for Claude-code-test

This document provides comprehensive guidance for AI assistants working with this repository.

## Repository Overview

**Name**: Claude-code-test
**Owner**: scrawlsbenches
**License**: MIT License (2025)
**Primary Language**: .NET/C#
**Purpose**: Testing repository for Claude Code functionality

## Current Repository State

**Status**: Production Ready (95% Specification Compliance)
**Build Status**: ✅ Passing (582 tests: 568 passing, 14 skipped)
**Test Coverage**: 85%+
**Last Updated**: November 20, 2025

### Project Structure

```
Claude-code-test/
├── src/
│   ├── HotSwap.Distributed.Domain/          # Domain models, enums, validation
│   ├── HotSwap.Distributed.Infrastructure/  # Telemetry, security, metrics
│   ├── HotSwap.Distributed.Orchestrator/    # Core orchestration, strategies
│   └── HotSwap.Distributed.Api/             # REST API controllers
├── examples/
│   └── ApiUsageExample/                     # Comprehensive API usage examples
├── tests/
│   └── HotSwap.Distributed.Tests/           # Unit tests (15+ tests)
├── .github/workflows/
│   └── build-and-test.yml                   # CI/CD pipeline
├── Dockerfile                                # Multi-stage Docker build
├── docker-compose.yml                        # Full stack deployment
├── DistributedKernel.sln                     # Solution file
├── test-critical-paths.sh                    # Critical path validation
├── validate-code.sh                          # Code validation script
├── .claude/skills/                            # Claude Skills (18 skills, ~12,000+ lines)
│   ├── thinking-framework.md                 # Meta-orchestrator (Think First, Code Later)
│   ├── project-intake.md                     # Business Analyst (extract requirements)
│   ├── scope-guard.md                        # Project Owner (prevent scope creep)
│   ├── architecture-review.md                # Technical Lead (right-sized architecture)
│   ├── reality-check.md                      # Project Manager (realistic estimates)
│   ├── sprint-planner.md                     # Sprint planning & task delegation
│   ├── dotnet-setup.md                       # .NET SDK setup automation
│   ├── tdd-helper.md                         # TDD workflow guidance
│   ├── precommit-check.md                    # Pre-commit validation
│   ├── api-endpoint-builder.md               # REST API scaffolding
│   ├── test-coverage-analyzer.md             # Coverage analysis
│   ├── race-condition-debugger.md            # Async debugging
│   ├── integration-test-debugger.md          # Integration test debugging
│   ├── performance-optimizer.md              # Load testing & optimization
│   ├── security-hardening.md                 # Secret rotation & OWASP compliance
│   ├── doc-sync-check.md                     # Documentation sync validation
│   ├── docker-helper.md                      # Docker configuration management
│   └── database-migration-helper.md          # EF Core migrations for PostgreSQL
├── CLAUDE.md                                 # This file (AI assistant guide)
├── SKILLS.md                                 # Claude Skills documentation
├── TASK_LIST.md                              # Comprehensive task roadmap (20+ tasks)
├── ENHANCEMENTS.md                           # Recent enhancements documentation
├── README.md                                 # Project overview
├── TESTING.md                                # Testing documentation
├── PROJECT_STATUS_REPORT.md                  # Production readiness status
├── SPEC_COMPLIANCE_REVIEW.md                 # Specification compliance
├── BUILD_STATUS.md                           # Build validation report
├── LICENSE                                   # MIT License
└── .gitignore                               # .NET gitignore
```

### Key Components

**Source Projects (src/):**
1. **HotSwap.Distributed.Domain** - Domain models, enums, value objects
2. **HotSwap.Distributed.Infrastructure** - Cross-cutting concerns (telemetry, security, metrics)
3. **HotSwap.Distributed.Orchestrator** - Core orchestration logic and deployment strategies
4. **HotSwap.Distributed.Api** - ASP.NET Core REST API

**Test Projects (tests/):**
- **HotSwap.Distributed.Tests** - Comprehensive unit tests with xUnit

**Examples (examples/):**
- **ApiUsageExample** - Complete API usage demonstration with 14 examples

## Technology Stack

### Primary Framework
- **.NET 8.0** - Latest LTS version with C# 12
- **ASP.NET Core 8.0** - Web API framework

### Core Infrastructure
- **OpenTelemetry 1.9.0** - Distributed tracing and observability
- **StackExchange.Redis 2.7.10** - Distributed locking and caching
- **Serilog.AspNetCore 8.0.0** - Structured logging
- **System.Security.Cryptography.Pkcs 8.0.0** - Module signature verification

### API & Documentation
- **Swashbuckle.AspNetCore 6.5.0** - OpenAPI/Swagger documentation
- **Microsoft.AspNetCore.OpenApi 8.0.0** - OpenAPI specification

### Testing Framework
- **xUnit 2.6.2** - Unit testing framework
- **Moq 4.20.70** - Mocking library
- **FluentAssertions 6.12.0** - Fluent assertion library

### Development Tools
- **MSBuild** - Build system
- **NuGet** - Package management
- **Docker** - Containerization
- **GitHub Actions** - CI/CD pipeline

## Quick Reference

**New to this project?** Start here for the most common tasks.

### Most Common Commands

| Task | Command | Documentation |
|------|---------|---------------|
| First-time setup | `dotnet restore && dotnet build && dotnet test` | [Setup](#development-environment-setup) |
| Build project | `dotnet build` | [Building](#building-the-project) |
| Run all tests | `dotnet test` | [Running Tests](#running-tests) |
| Run API locally | `dotnet run --project src/HotSwap.Distributed.Api/` | [Running](#running-the-application) |
| Pre-commit check | `dotnet clean && dotnet restore && dotnet build --no-incremental && dotnet test` | [Pre-Commit](#️-critical-pre-commit-checklist) |
| **Use Claude Skills** | `/tdd-helper`, `/precommit-check`, `/doc-sync-check`, etc. | **[SKILLS.md](SKILLS.md)** ⭐ |
| Create feature branch | `git checkout -b claude/feature-name-sessionid` | [Git Workflow](#git-workflow) |
| Push changes | `git push -u origin claude/branch-name` | [Git Push](#git-push-requirements) |

### Project Metrics (Current)

> **Last Verified**: 2025-11-17 via `dotnet test --verbosity quiet`

| Metric | Value | Notes |
|--------|-------|-------|
| Total Tests | 582 | 568 passing, 14 skipped, 0 failed |
| Test Coverage | 85%+ | Measured via code coverage tools |
| .NET SDK Version | 8.0.121 | Minimum: 8.0+ |
| Build Warnings | 0 | Clean build |
| Projects in Solution | 4 | 4 source + 2 test + 1 example |

### AI Assistant Critical Rules

**ALWAYS:**
- ✅ Run pre-commit checklist before EVERY commit
- ✅ Follow TDD (tests before implementation)
- ✅ Update documentation when changing code
- ✅ Verify contracts (models, interfaces) before use

**NEVER:**
- ❌ Commit with failing tests
- ❌ Commit without verifying build succeeds
- ❌ Skip the pre-commit checklist
- ❌ Guess property/method names without reading definitions

## Table of Contents

### Getting Started (Essential Reading)
1. [Quick Reference](#quick-reference) ⭐⭐⭐ - Start here!
2. [Claude Skills (SKILLS.md)](SKILLS.md) ⭐⭐ - 7 automated workflows
3. [Repository Overview](#repository-overview)
4. [Technology Stack](#technology-stack)
5. [Development Environment Setup](#development-environment-setup) ⭐⭐
6. [First Time Build and Test](#first-time-build-and-test)

### Daily Development Workflows
7. [Pre-Commit Checklist](#️-critical-pre-commit-checklist) ⭐⭐⭐ - Run before EVERY commit
8. [Test-Driven Development (TDD)](#test-driven-development-tdd-workflow) ⭐⭐
9. [Git Workflow](#git-workflow) ⭐
10. [Building the Project](#building-the-project)
11. [Running Tests](#running-tests) ⭐
12. [Running the Application](#running-the-application)
13. [Docker Development and Maintenance](#docker-development-and-maintenance) ⭐ - Maintain Dockerfile and docker-compose.yml

### Standards and Best Practices
14. [.NET Development Conventions](#net-development-conventions)
15. [Code Generation Standards](#code-generation-standards)
16. [Testing Requirements](#testing-requirements) ⭐
17. [Security Best Practices](#security-best-practices)
18. [Documentation Standards](#documentation-standards)
19. [Task Management with TASK_LIST.md](#working-with-task_listmd)

### AI Assistant Guidelines
20. [AI Assistant Critical Rules](#ai-assistant-guidelines) ⭐⭐⭐
21. [Initial Analysis Checklist](#initial-analysis-checklist)
22. [Error Handling](#error-handling)

### Reference Materials
23. [Common .NET Commands](#common-net-commands-reference)
24. [Troubleshooting](#troubleshooting-common-issues)
25. [Resources](#resources)
26. [Quality Standards](#quality-standards)

### Edge Cases and Advanced Topics
27. [No .NET SDK Checklist](#-alternative-no-net-sdk-checklist) - For restricted environments
28. [Avoiding Stale Documentation](#avoiding-stale-documentation) - Maintenance guide
29. [Changelog](#changelog) - Document history

**Priority Legend:**
- ⭐⭐⭐ **CRITICAL** - Must read before making any changes
- ⭐⭐ **Important** - Read before daily development
- ⭐ **Helpful** - Reference as needed

## Development Environment Setup

**💡 TIP:** Use the `/dotnet-setup` skill to automate environment setup. See [SKILLS.md](SKILLS.md#dotnet-setup) for details.

This section provides comprehensive instructions for setting up your development environment to work with this project.

### Prerequisites Installation

#### Required Software

**1. .NET 8.0 SDK**

The project requires .NET 8.0 SDK or later.

**Windows:**
```powershell
# Download and install from:
# https://dotnet.microsoft.com/download/dotnet/8.0

# Or using winget:
winget install Microsoft.DotNet.SDK.8

# Verify installation
dotnet --version
# Expected output: 8.0.x or later
```

**Linux (Ubuntu/Debian):**
```bash
# Add Microsoft package repository
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Install .NET SDK
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0

# Verify installation
dotnet --version
```

**macOS:**
```bash
# Using Homebrew:
brew install dotnet@8

# Or download from:
# https://dotnet.microsoft.com/download/dotnet/8.0

# Verify installation
dotnet --version
```

**Claude Code Web Environment (Ubuntu 24.04):**

If you're using Claude Code in a web environment, you can install .NET SDK directly. The environment runs Ubuntu 24.04 LTS with root access.

```bash
# Step 1: Download Microsoft package repository for Ubuntu 24.04
wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb

# Step 2: Install the repository configuration (as root, no sudo needed)
dpkg -i packages-microsoft-prod.deb

# Step 3: Clean up the downloaded file
rm packages-microsoft-prod.deb

# Step 4: Fix /tmp permissions (prevents GPG errors during update)
chmod 1777 /tmp

# Step 5: Update package lists
apt-get update
# Note: You may see 403 Forbidden errors from PPA repositories - these are non-critical

# Step 6: Install .NET SDK 8.0
apt-get install -y dotnet-sdk-8.0

# Step 7: Verify installation
dotnet --version
# Expected output: 8.0.121 or later

dotnet --list-sdks
# Expected output: 8.0.121 [/usr/lib/dotnet/sdk]

dotnet --list-runtimes
# Expected output:
# Microsoft.AspNetCore.App 8.0.21 [/usr/lib/dotnet/shared/Microsoft.AspNetCore.App]
# Microsoft.NETCore.App 8.0.21 [/usr/lib/dotnet/shared/Microsoft.NETCore.App]
```

**Installation time**: ~30-60 seconds
**Disk space required**: ~500 MB

After installation, you can immediately use all dotnet commands for building, testing, and running the project.

**2. Docker (Optional - for containerized deployment)**

**Windows/macOS:**
- Download Docker Desktop from https://www.docker.com/products/docker-desktop

**Linux:**
```bash
# Install Docker
sudo apt-get update
sudo apt-get install -y docker.io docker-compose

# Add user to docker group
sudo usermod -aG docker $USER

# Verify installation
docker --version
docker-compose --version
```

**3. Git**

Ensure Git is installed for version control.

```bash
# Verify Git installation
git --version

# If not installed:
# Windows: https://git-scm.com/download/win
# Linux: sudo apt-get install git
# macOS: brew install git
```

#### Verify Prerequisites

Run the following commands to verify all prerequisites are installed:

```bash
# Check .NET SDK
dotnet --version
# Expected: 8.0.x or later

# List installed SDKs
dotnet --list-sdks
# Expected: 8.0.xxx [path]

# List installed runtimes
dotnet --list-runtimes
# Expected: Microsoft.AspNetCore.App 8.0.x, Microsoft.NETCore.App 8.0.x

# Check Docker (if using containerization)
docker --version
docker-compose --version

# Check Git
git --version
```

### Installing Dependencies

#### Clone the Repository

```bash
# Clone the repository
git clone https://github.com/scrawlsbenches/Claude-code-test.git
cd Claude-code-test

# Checkout your working branch
git checkout claude/your-branch-name
```

#### Restore NuGet Packages

The project uses several NuGet packages. Restore them before building:

```bash
# Restore all project dependencies
dotnet restore

# Expected output:
#   Restore completed in X.XX sec for HotSwap.Distributed.Domain.csproj
#   Restore completed in X.XX sec for HotSwap.Distributed.Infrastructure.csproj
#   Restore completed in X.XX sec for HotSwap.Distributed.Orchestrator.csproj
#   Restore completed in X.XX sec for HotSwap.Distributed.Api.csproj
#   Restore completed in X.XX sec for HotSwap.Distributed.Tests.csproj
#   Restore completed in X.XX sec for ApiUsageExample.csproj
```

#### List Installed Packages

To see all packages and their versions:

```bash
# List all packages in the solution
dotnet list package

# Check for outdated packages
dotnet list package --outdated

# Check for vulnerable packages
dotnet list package --vulnerable
```

### Building the Project

#### Build All Projects

```bash
# Build entire solution (Debug configuration)
dotnet build

# Build in Release mode (recommended for production)
dotnet build -c Release

# Build with detailed output
dotnet build -v detailed

# Expected output:
#   Build succeeded.
#       0 Warning(s)
#       0 Error(s)
```

#### First Time Build and Test

**After installing .NET SDK 8.0 for the first time**, run these commands in order:

```bash
# Step 1: Clean any existing build artifacts (if any)
dotnet clean

# Expected output:
#   Build succeeded.
#       0 Warning(s)
#       0 Error(s)
#   Time Elapsed 00:00:02.30

# Step 2: Restore all NuGet packages
dotnet restore

# Expected output:
#   Determining projects to restore...
#   Restored /home/user/Claude-code-test/src/HotSwap.Distributed.Domain/...
#   Restored /home/user/Claude-code-test/examples/ApiUsageExample/...
#   ... (6 projects restored)

# Step 3: Build the entire solution (non-incremental for first build)
dotnet build --no-incremental

# Expected output:
#   Build succeeded.
#       0 Warning(s)
#       0 Error(s)
#   Time Elapsed 00:00:18.04

# Step 4: Run all tests
dotnet test

# Expected output:
#   Passed!  - Failed:     0, Passed:   568, Skipped:    14, Total:   582, Duration: ~18 s
```

**Important Notes:**
- The build may show 1 warning (System.Text.Json version conflict - this is acceptable)
- Test count: 582 total (568 passing, 14 skipped) - may vary as tests are added
- Total setup time: approximately 20-30 seconds after .NET SDK installation

#### Build Specific Projects

```bash
# Build only the API project
dotnet build src/HotSwap.Distributed.Api/HotSwap.Distributed.Api.csproj

# Build only the orchestrator
dotnet build src/HotSwap.Distributed.Orchestrator/HotSwap.Distributed.Orchestrator.csproj

# Build only the examples
dotnet build examples/ApiUsageExample/ApiUsageExample.csproj
```

#### Clean and Rebuild

If you encounter build issues:

```bash
# Clean all build artifacts
dotnet clean

# Clean and rebuild
dotnet clean && dotnet build

# Force a complete rebuild
dotnet build --no-incremental
```

### Running Tests

This project includes comprehensive test coverage with xUnit.

#### Run All Tests

```bash
# Run all tests in the solution
dotnet test

# Expected output:
#   Passed!  - Failed:     0, Passed:   568, Skipped:    14, Total:   582

# Run tests with detailed output
dotnet test --verbosity normal

# Run tests with minimal output
dotnet test --verbosity quiet
```

#### Run Specific Test Projects

```bash
# Run only unit tests
dotnet test tests/HotSwap.Distributed.Tests/HotSwap.Distributed.Tests.csproj

# Run tests with filter
dotnet test --filter "FullyQualifiedName~DeploymentPipeline"
```

#### Test Coverage

```bash
# Run tests with code coverage
dotnet test --collect:"XPlat Code Coverage"

# Coverage reports will be in:
# tests/HotSwap.Distributed.Tests/TestResults/{guid}/coverage.cobertura.xml
```

#### Critical Path Tests

The project includes a shell script for critical path validation:

```bash
# Run critical path tests
./test-critical-paths.sh

# Expected output:
#   ✓ All 582 tests passed (568 passing, 14 skipped)
```

#### Code Validation

```bash
# Run code validation script
./validate-code.sh

# This checks:
# - Build succeeds
# - Tests pass
# - No obvious code issues
```

### Running the Application

#### Run the API Locally

```bash
# Run the API project
dotnet run --project src/HotSwap.Distributed.Api/HotSwap.Distributed.Api.csproj

# API will be available at:
# - http://localhost:5000
# - Swagger UI: http://localhost:5000/swagger
# - Health check: http://localhost:5000/health
```

#### Run with Docker Compose

```bash
# Build and start all services
docker-compose up -d

# View logs
docker-compose logs -f orchestrator-api

# Check service status
docker-compose ps

# Stop services
docker-compose down
```

**Available Services:**
- **API**: http://localhost:5000
- **Swagger UI**: http://localhost:5000/swagger
- **Jaeger UI**: http://localhost:16686 (tracing)
- **Health**: http://localhost:5000/health

#### Run the API Usage Examples

```bash
# Navigate to examples directory
cd examples/ApiUsageExample

# Run examples (ensure API is running first)
dotnet run

# Or use the convenience script
./run-example.sh

# Run with custom API URL
dotnet run -- http://your-api:5000
./run-example.sh http://your-api:5000
```

### Docker Development and Maintenance

**💡 TIP:** Use the `/docker-helper` skill to automate Docker validation and optimization. See [SKILLS.md](SKILLS.md#docker-helper) for details.

This section provides comprehensive guidance for maintaining Docker configuration files (Dockerfile and docker-compose.yml) to ensure containerized deployments remain secure, optimized, and up-to-date.

#### When to Update Docker Configuration

**Update Dockerfile when:**
- Base image updates are available (e.g., `mcr.microsoft.com/dotnet/sdk:8.0` → `9.0`)
- .NET SDK or runtime version changes
- New system dependencies are required (packages, libraries)
- Security patches are released for base images
- Build performance can be optimized
- Multi-stage build improvements are identified

**Update docker-compose.yml when:**
- New service dependencies are added (Redis, PostgreSQL, RabbitMQ, etc.)
- Port mappings change
- Environment variables are added or modified
- Volume mounts change (data persistence, configuration)
- Network configuration needs updating
- Service health checks are added or modified
- Resource limits (CPU, memory) need adjustment

#### Docker Maintenance Best Practices

**1. Base Image Management:**
```dockerfile
# ❌ AVOID: Using 'latest' tag (unpredictable)
FROM mcr.microsoft.com/dotnet/sdk:latest

# ✅ PREFER: Pinning specific versions
FROM mcr.microsoft.com/dotnet/sdk:8.0.121-alpine

# ✅ BEST: Use specific digest for reproducibility
FROM mcr.microsoft.com/dotnet/sdk:8.0@sha256:abc123...
```

**2. Multi-Stage Build Optimization:**
```dockerfile
# Keep build and runtime stages separate
# Build stage includes SDK (large)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore && dotnet build -c Release

# Runtime stage uses smaller runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /src/bin/Release/net8.0/publish .
ENTRYPOINT ["dotnet", "HotSwap.Distributed.Api.dll"]
```

**3. Layer Caching Strategies:**
```dockerfile
# ✅ CORRECT: Copy files in order of change frequency
# 1. Copy project files first (change rarely)
COPY *.csproj .
RUN dotnet restore

# 2. Copy source code last (changes frequently)
COPY . .
RUN dotnet build

# ❌ WRONG: Copying everything at once invalidates cache
COPY . .
RUN dotnet restore && dotnet build
```

**4. Security Scanning:**
```bash
# Scan Docker images for vulnerabilities before deployment

# Using Docker Scout (built-in)
docker scout cves hotswap-orchestrator:latest

# Using Trivy (recommended)
trivy image hotswap-orchestrator:latest

# Using Snyk (requires authentication)
snyk container test hotswap-orchestrator:latest
```

**5. Image Size Optimization:**
```dockerfile
# Use Alpine-based images (smaller footprint)
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine

# Remove unnecessary files
RUN rm -rf /tmp/* /var/cache/apk/*

# Use .dockerignore to exclude files
# Create .dockerignore with: bin/, obj/, .git/, *.md
```

**6. .dockerignore Maintenance:**
```
# .dockerignore - Exclude from build context
bin/
obj/
.git/
.github/
.vs/
.vscode/
*.md
*.log
**/node_modules
**/TestResults
Dockerfile*
docker-compose*
.gitignore
```

#### Testing Docker Builds

**Before committing Docker changes, run these tests:**

```bash
# 1. Build Docker image locally
docker build -t hotswap-test:local .

# Expected: Build succeeds without errors
# Check output for warnings or cache misses

# 2. Verify image size (should be <500MB for runtime image)
docker images hotswap-test:local
# Expected: SIZE column shows reasonable size

# 3. Run container locally
docker run -d -p 5001:5000 --name hotswap-test-container hotswap-test:local

# 4. Health check
curl http://localhost:5001/health
# Expected: HTTP 200 OK

# 5. Check container logs
docker logs hotswap-test-container
# Expected: No errors, application started successfully

# 6. Stop and remove test container
docker stop hotswap-test-container
docker rm hotswap-test-container
docker rmi hotswap-test:local
```

#### Testing docker-compose Stack

**Before committing docker-compose.yml changes:**

```bash
# 1. Start services in detached mode
docker-compose up -d

# Expected: All services start without errors

# 2. Check service status
docker-compose ps

# Expected output:
#   NAME                    STATUS
#   orchestrator-api        Up (healthy)
#   redis                   Up
#   jaeger                  Up

# 3. Verify service connectivity
docker-compose logs orchestrator-api | grep "Now listening on"
# Expected: API is listening on configured port

# 4. Test API endpoints
curl http://localhost:5000/health
curl http://localhost:5000/swagger/index.html

# Expected: Both respond successfully

# 5. Run tests in containerized environment
docker-compose run --rm orchestrator-api dotnet test

# Expected: All tests pass

# 6. Check for resource issues
docker stats --no-stream

# Expected: Memory and CPU usage within reasonable limits

# 7. Clean up
docker-compose down -v
# -v removes volumes to ensure clean state
```

#### Docker Security Best Practices

**1. Run as Non-Root User:**
```dockerfile
# Create non-root user
RUN addgroup -g 1000 appuser && \
    adduser -D -u 1000 -G appuser appuser

# Switch to non-root user
USER appuser

# Set working directory with proper permissions
WORKDIR /app
```

**2. Read-Only Filesystems:**
```yaml
# docker-compose.yml
services:
  orchestrator-api:
    image: hotswap-orchestrator
    read_only: true
    tmpfs:
      - /tmp
      - /app/logs
```

**3. Resource Limits:**
```yaml
# docker-compose.yml
services:
  orchestrator-api:
    image: hotswap-orchestrator
    deploy:
      resources:
        limits:
          cpus: '2.0'
          memory: 1G
        reservations:
          cpus: '0.5'
          memory: 512M
```

**4. Network Isolation:**
```yaml
# docker-compose.yml
networks:
  frontend:
    driver: bridge
  backend:
    driver: bridge
    internal: true  # No external access

services:
  orchestrator-api:
    networks:
      - frontend
      - backend
  redis:
    networks:
      - backend  # Only accessible internally
```

#### Monthly Docker Maintenance Tasks

**Perform these tasks monthly:**

```bash
# 1. Check for base image updates
docker pull mcr.microsoft.com/dotnet/sdk:8.0
docker pull mcr.microsoft.com/dotnet/aspnet:8.0

# Compare digest with current Dockerfile
# If different, test and update Dockerfile

# 2. Scan images for vulnerabilities
trivy image hotswap-orchestrator:latest --severity HIGH,CRITICAL

# Address any HIGH or CRITICAL vulnerabilities

# 3. Review Docker logs
docker-compose logs --tail=100 orchestrator-api | grep -i error

# Investigate any recurring errors

# 4. Clean up unused resources
docker system prune -a --volumes
# WARNING: This removes all unused images, containers, and volumes

# 5. Verify .dockerignore is current
# Ensure no large/unnecessary files are in build context
du -sh .  # Check directory size before build

# 6. Test build performance
time docker build --no-cache -t hotswap-test .
# Compare with previous build times
```

#### Integration with Pre-Commit Checklist

**If you modified Dockerfile or docker-compose.yml, add this step:**

See the [Pre-Commit Checklist Docker Verification Step](#step-7-verify-docker-changes-if-applicable) for detailed validation process before committing.

#### Common Docker Issues and Solutions

See the [Troubleshooting Docker Issues](#docker-related-issues) section for comprehensive troubleshooting guidance.

#### Docker Documentation Update Checklist

**When you modify Docker files, update these docs:**

1. **CLAUDE.md** (this file)
   - Update "Running with Docker Compose" section if ports/services change
   - Update "Docker Development and Maintenance" if best practices change
   - Update Changelog with Docker-related changes

2. **README.md**
   - Update Docker Quickstart section
   - Update environment variable documentation
   - Update service URLs and ports

3. **docker-compose.yml comments**
   - Add inline comments explaining configuration choices
   - Document environment variables and their purposes

4. **.env.example** (if exists)
   - Update with new environment variables
   - Document default values and valid ranges

#### Docker Version Pinning Strategy

**Current approach (as of 2025-11-17):**

- **Base images**: Pin to specific .NET SDK version (e.g., `8.0.121`)
- **Service images**: Pin to major.minor version (e.g., `redis:7.0`, `jaegertracing/all-in-one:1.52`)
- **Review schedule**: Monthly check for updates
- **Update process**: Test in development → staging → production

**Recommended digest pinning for production:**
```dockerfile
# After testing, pin to specific digest
FROM mcr.microsoft.com/dotnet/aspnet:8.0@sha256:[digest-here]
# Document digest and date in commit message
```

### Development Workflow

#### Typical Development Session

```bash
# 1. Pull latest changes from main
git pull origin main

# 2. Create feature branch
git checkout -b claude/your-feature-name-sessionid

# 3. Restore dependencies
dotnet restore

# 4. Build solution
dotnet build

# 5. Run tests to ensure everything works
dotnet test

# 6. Make your changes...

# 7. Build and test again
dotnet build
dotnet test

# 8. Run code validation
./validate-code.sh

# 9. Commit changes
git add .
git commit -m "feat: your feature description"

# 10. Pull latest changes and resolve conflicts (CRITICAL)
git fetch origin claude/your-feature-name-sessionid
git pull origin claude/your-feature-name-sessionid --no-rebase

# If merge conflicts occur:
# - Fix conflicts in affected files
# - git add <resolved-files>
# - git commit -m "merge: resolve conflicts with remote changes"
# - Rebuild and retest: dotnet build && dotnet test

# 11. Push to remote with retry logic
git push -u origin claude/your-feature-name-sessionid

# If push fails with network error, retry with exponential backoff:
# Wait 2s, retry
# Wait 4s, retry
# Wait 8s, retry
# Wait 16s, retry (final attempt)
```

### Troubleshooting Setup Issues

#### .NET SDK Not Found

```bash
# Error: "dotnet: command not found"
# Solution: Ensure .NET SDK is installed and in PATH

# Windows: Add to PATH
# C:\Program Files\dotnet

# Linux/macOS: Add to ~/.bashrc or ~/.zshrc
export PATH="$PATH:/usr/local/share/dotnet"
```

#### Installing .NET SDK on Linux (When Running as Root or sudo Issues)

If you encounter sudo permission errors or are running as root, follow these exact steps:

```bash
# Step 1: Download Microsoft package repository configuration
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb

# Step 2: Install the repository configuration
# If running as root (uid=0), omit sudo:
dpkg -i packages-microsoft-prod.deb

# If running as regular user with sudo access:
sudo dpkg -i packages-microsoft-prod.deb

# Step 3: Clean up the downloaded file
rm packages-microsoft-prod.deb

# Step 4: Fix /tmp permissions if you encounter GPG errors during apt-get update
# Error example: "Couldn't create temporary file /tmp/apt.conf.xxxxx"
# Solution:
chmod 1777 /tmp

# Step 5: Update package lists
# As root:
apt-get update

# As regular user:
sudo apt-get update

# Step 6: Install .NET SDK 8.0
# As root:
apt-get install -y dotnet-sdk-8.0

# As regular user:
sudo apt-get install -y dotnet-sdk-8.0

# Step 7: Verify installation
dotnet --version
# Expected output: 8.0.416 or later

dotnet --list-sdks
# Expected output: 8.0.xxx [/usr/share/dotnet/sdk]
```

**Common Installation Errors:**

1. **403 Forbidden errors from PPA repositories** - These are non-critical and won't prevent .NET SDK installation
2. **GPG/temporary file errors** - Fix with `chmod 1777 /tmp`
3. **sudo.conf ownership errors** - Run as root directly using `dpkg -i` instead of `sudo dpkg -i`

#### Package Restore Fails

```bash
# Error: "Unable to load the service index for source"
# Solution: Clear NuGet cache

dotnet nuget locals all --clear
dotnet restore
```

#### Build Errors After Pulling

```bash
# Error: Various build errors after git pull
# Solution: Clean and rebuild

dotnet clean
dotnet restore
dotnet build
```

#### Port Already in Use

```bash
# Error: "Address already in use: http://localhost:5000"
# Solution: Change port or kill existing process

# Find process using port 5000
lsof -i :5000          # Linux/macOS
netstat -ano | find "5000"  # Windows

# Kill the process or run on different port
dotnet run --urls "http://localhost:5001"
```

#### Docker Permission Denied

```bash
# Error: "Permission denied while trying to connect to Docker daemon"
# Solution: Add user to docker group (Linux)

sudo usermod -aG docker $USER
# Logout and login again
```

## .NET Development Conventions

### Code Style
- Follow [C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use PascalCase for public members, methods, and types
- Use camelCase for private fields and local variables
- Prefix private fields with underscore: `_privateField`
- Use meaningful, descriptive names

### Project Organization
- **One class per file** (unless nested/related classes)
- **Namespace matches folder structure**
- **Separate concerns**: Models, Services, Controllers, etc.
- **Use dependency injection** for loose coupling

### Testing Standards
- **Test project naming**: `[ProjectName].Tests`
- **Test method naming**: `MethodName_StateUnderTest_ExpectedBehavior`
- **Arrange-Act-Assert (AAA)** pattern
- Aim for high code coverage (>80%)
- Include both unit and integration tests

### NuGet Package Management
- Use central package management when possible
- Keep packages up to date
- Document why specific package versions are pinned
- Use `dotnet add package` for adding dependencies

## Git Workflow

### Branch Strategy
- **Main branch**: `main` (or `master`)
- **Feature branches**: `claude/[descriptive-name]-[session-id]`
- **Convention**: All Claude-generated branches MUST start with `claude/` and end with session ID

### Commit Guidelines
```bash
# Stage changes
git add .

# Commit with descriptive message
git commit -m "feat: add user authentication service"

# Push to feature branch
git push -u origin claude/[branch-name]
```

### Commit Message Format
Follow conventional commits:
- `feat:` - New features
- `fix:` - Bug fixes
- `docs:` - Documentation changes
- `refactor:` - Code refactoring
- `test:` - Test additions/modifications
- `chore:` - Maintenance tasks

### ⚠️ CRITICAL: Pre-Commit Checklist

**💡 TIP:** Use the `/precommit-check` skill to automate validation. See [SKILLS.md](SKILLS.md#precommit-check) for details.

**NEVER commit code without completing ALL steps below.** This prevents CI/CD failures and ensures code quality.

#### Prerequisites: Check .NET SDK Availability

Before starting the pre-commit checklist, verify if .NET SDK is available:

```bash
# Check if dotnet is available
dotnet --version
```

**If .NET SDK is available** (local development, CI/CD environments):
- Follow Steps 1-6 below EXACTLY
- Do NOT commit if ANY step fails

**If .NET SDK is NOT available** (e.g., Claude Code web environment):
- Skip to **"Alternative: No .NET SDK Checklist"** below
- You MUST follow the alternative checklist instead

---

#### Step 1: Clean Build (Requires .NET SDK)

```bash
# Clean all build artifacts
dotnet clean

# Restore all NuGet packages
dotnet restore

# Build entire solution (NOT incremental)
dotnet build --no-incremental

# Expected output: "Build succeeded. 0 Warning(s) 0 Error(s)"
# If you see ANY errors or warnings, FIX THEM before proceeding
```

**What to check:**
- ✅ Build completes with **zero errors**
- ✅ Build completes with **zero warnings** (warnings = future errors)
- ✅ All projects compile successfully
- ✅ No missing dependencies or package errors

**Common build errors:**
```bash
# Missing using statement
# Fix: Add required using directives at top of file

# Namespace mismatch
# Fix: Ensure namespace matches folder structure

# Missing project reference
# Fix: Add project reference with:
dotnet add <project> reference <referenced-project>

# Type not found
# Fix: Ensure the type exists and is public, check namespace
```

#### Step 2: Run ALL Tests

```bash
# Run all tests in solution
dotnet test

# Expected output: "Passed! - Failed: 0, Passed: X, Skipped: 0"
# If ANY tests fail, FIX THEM before proceeding
```

**What to check:**
- ✅ **ALL tests pass** (zero failures)
- ✅ No tests are skipped unexpectedly
- ✅ Tests run to completion without hanging
- ✅ No test warnings or errors in output

**Common test errors:**
```bash
# Test fails with NullReferenceException
# Fix: Check mock setup, ensure all required dependencies are mocked

# Test fails with timeout
# Fix: Increase timeout or optimize code being tested

# Test can't find dependencies
# Fix: Ensure test project references all required projects
```

#### Step 3: Run Specific Test Projects (if available)

```bash
# Run tests for each project individually to catch issues
dotnet test tests/HotSwap.Distributed.Tests/HotSwap.Distributed.Tests.csproj

# Check critical path tests
./test-critical-paths.sh

# Run code validation script
./validate-code.sh
```

#### Step 4: Verify New Files Compile

If you created new files, verify they're included:

```bash
# Check that new files are in git staging
git status

# Ensure new .cs files are in .csproj (auto-included in SDK-style projects)
# If NOT SDK-style, manually add to .csproj:
# <Compile Include="Path/To/NewFile.cs" />
```

#### Step 5: Check for Common Issues

```bash
# Check for compilation issues in specific configurations
dotnet build -c Debug
dotnet build -c Release

# Check for missing XML documentation (if enabled)
# Look for warnings like "Missing XML comment for publicly visible type"

# Verify no hardcoded paths or environment-specific code
grep -r "C:\\" src/  # Windows paths
grep -r "/Users/" src/  # macOS paths
grep -r "localhost:5000" src/  # Hardcoded URLs (use configuration instead)
```

#### Step 6: Final Verification

Before `git commit`:

```bash
# 1. Ensure clean build
dotnet clean && dotnet restore && dotnet build --no-incremental

# 2. Ensure all tests pass
dotnet test

# 3. Check git status
git status

# 4. Review changes
git diff --staged

# 5. Only THEN commit
git commit -m "feat: your message"
```

#### Step 7: Verify Docker Changes (if applicable)

**Only if you modified Dockerfile or docker-compose.yml:**

```bash
# 1. Check what Docker files changed
git diff --staged | grep -E "Dockerfile|docker-compose"

# If Docker files were modified, proceed with Docker validation:

# 2. Build Docker image
docker build -t hotswap-test:local .

# Expected: Build succeeds without errors
# Check for warnings or unusual cache behavior

# 3. Verify image size
docker images hotswap-test:local
# Expected: Reasonable size (<500MB for runtime image)

# 4. Test docker-compose stack
docker-compose up -d

# 5. Verify all services are running
docker-compose ps
# Expected: All services show "Up" status

# 6. Health check API
curl http://localhost:5000/health
# Expected: HTTP 200 OK

# 7. Check logs for errors
docker-compose logs orchestrator-api | grep -i error
# Expected: No critical errors

# 8. Run tests in Docker (optional but recommended)
docker-compose run --rm orchestrator-api dotnet test
# Expected: All tests pass

# 9. Clean up
docker-compose down -v

# 10. Remove test image
docker rmi hotswap-test:local
```

**What to check:**
- ✅ Docker build completes without errors
- ✅ Image size is reasonable (not excessively large)
- ✅ All services start successfully
- ✅ Health endpoints respond correctly
- ✅ No errors in container logs
- ✅ Tests pass in containerized environment (if applicable)
- ✅ Updated documentation reflects Docker changes

**Common Docker validation errors:**
```bash
# Build fails with "COPY failed"
# Fix: Check .dockerignore isn't excluding required files

# Service fails to start
# Fix: Check docker-compose.yml for correct image names, ports

# Health check fails
# Fix: Verify ports are correctly mapped, service is actually running

# Tests fail in Docker but pass locally
# Fix: Check for environment-specific dependencies, file paths
```

**Skip Docker validation if:**
- You only changed application code (no Docker config changes)
- You're working in an environment without Docker installed
- You already tested Docker changes in a previous commit

---

### 🔧 Alternative: No .NET SDK Checklist

**Use this checklist when .NET SDK is NOT available** (e.g., Claude Code web environment, restricted environments).

⚠️ **CRITICAL**: Since you cannot run build/test locally, you MUST be extra careful with code review and validation.

#### Step 1: Verify Contracts Before Use

**⚠️ CRITICAL**: Always read type definitions before using them - don't guess property/method names!

**Before using ANY type (class, interface, enum):**

1. **Read the definition file** - Don't assume property/method names
2. **Check required vs optional** - Note nullability and `required` keyword
3. **Verify parameter types** - Especially for methods and constructors
4. **Use exact names** - Property names are case-sensitive

**Quick Verification Process:**
```bash
# Find the type definition
grep -r "class YourType" src/

# Read the complete definition
cat path/to/file.cs

# Use EXACT names from the definition
```

**Common Contract Mistakes:**
```csharp
// ❌ WRONG: Guessing property names
var obj = new SomeModel { Message = "..." };  // Property might not exist!

// ✅ CORRECT: Read class definition first, use actual properties
// Check file first, then use exact property names from definition
var obj = new SomeModel { Error = "..." };    // Property verified to exist
```

**Key Rules:**
- Never guess property/method/parameter names
- Always verify nullability (`string?` vs `string`, `required` keyword)
- Check method signatures match when setting up mocks
- Verify enum values exist before using them

#### Step 2: Verify All Package References

Manually check that all project files have correct package references:

**For NEW test files:**
```bash
# Check if test project has all packages used in test code
# Example: If tests use BCrypt.Net.BCrypt, the test project MUST reference BCrypt.Net-Next

# Common packages needed in tests:
# - BCrypt.Net-Next (if tests create users with hashed passwords)
# - Microsoft.Extensions.Logging.Abstractions (if tests mock ILogger)
# - Any packages whose types are directly used in test code
```

**Verify:**
- ✅ Check `tests/HotSwap.Distributed.Tests/HotSwap.Distributed.Tests.csproj` for missing packages
- ✅ If test code uses `BCrypt.Net.BCrypt`, ensure `<PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />` exists
- ✅ If test code uses `ILogger<T>`, ensure `<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />` exists

#### Step 3: Review All Code Changes

Carefully review every code change:

```bash
# Show all changes
git diff

# Review staged changes
git diff --cached
```

**Check for:**
- ✅ All `using` statements are correct and namespaces exist
- ✅ All types referenced exist in the referenced projects/packages
- ✅ No syntax errors (missing semicolons, braces, etc.)
- ✅ Proper async/await usage (async methods return Task, await is used correctly)
- ✅ All interfaces have implementations registered in Program.cs
- ✅ Mock setups in tests match actual method signatures
- ✅ Test assertions use correct FluentAssertions syntax

#### Step 4: Verify Project References

Ensure all project references are correct:

**For NEW source files:**
- ✅ If code uses types from Domain, ensure project references Domain
- ✅ If code uses types from Infrastructure, ensure project references Infrastructure
- ✅ API layer should reference Domain, Infrastructure, and Orchestrator
- ✅ Infrastructure should reference Domain only
- ✅ Domain should have no project references (core layer)

**For NEW test files:**
- ✅ Test project should reference all projects whose types are used in tests
- ✅ Check `tests/HotSwap.Distributed.Tests/HotSwap.Distributed.Tests.csproj` has `<ProjectReference>` for all needed projects

#### Step 5: Check for Common Build Errors

Review code for patterns that commonly cause build failures:

**Namespace Issues:**
```csharp
// ❌ WRONG: Namespace doesn't match folder structure
namespace HotSwap.WrongNamespace;  // File is in HotSwap.Distributed.Domain/Models/

// ✅ CORRECT: Namespace matches folder
namespace HotSwap.Distributed.Domain.Models;
```

**Missing Using Statements:**
```csharp
// ❌ WRONG: Using List<T> without using System.Collections.Generic
public List<User> Users { get; set; }

// ✅ CORRECT: Add using statement
using System.Collections.Generic;
```

**Async/Await Issues:**
```csharp
// ❌ WRONG: Async method doesn't return Task
public async void DoSomethingAsync() { }

// ✅ CORRECT: Async methods return Task
public async Task DoSomethingAsync() { }
```

#### Step 6: Validate Test Code

Review test code for common issues:

**Mock Setup Issues:**
```csharp
// ❌ WRONG: Mock setup doesn't match actual method signature
_mockRepo.Setup(x => x.GetUserAsync(It.IsAny<string>()))  // Method has 2 params!
    .ReturnsAsync(user);

// ✅ CORRECT: Match actual signature
_mockRepo.Setup(x => x.GetUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(user);
```

**Test Dependency Issues:**
```csharp
// ❌ WRONG: Test uses type directly but package not referenced
var hash = BCrypt.Net.BCrypt.HashPassword("test");  // BCrypt.Net-Next not in test project!

// ✅ CORRECT: Add package to test project .csproj
```

#### Step 7: Document CI/CD Dependency

Add a note to your commit message:

```bash
git commit -m "feat: your feature description

Note: Build and tests will run in GitHub Actions CI/CD pipeline.
Package references verified manually for environments without .NET SDK.
"
```

#### Step 8: Monitor CI/CD Build

After pushing:

```bash
# Push to remote
git push -u origin claude/your-branch-name

# IMMEDIATELY check GitHub Actions build status
# Go to: https://github.com/scrawlsbenches/Claude-code-test/actions
```

**If build fails:**
1. Check the error message in GitHub Actions logs
2. Identify the missing package reference or code issue
3. Fix locally using this checklist
4. Add missing package references to appropriate .csproj files
5. Commit fix: `git commit -m "fix: add missing package reference for tests"`
6. Push fix: `git push -u origin claude/your-branch-name`
7. Monitor build again

#### Step 9: Pre-Commit Validation Summary

Before committing without .NET SDK, verify ALL of these:

- ✅ **Contracts verified** - Read all type definitions before use (Step 1)
- ✅ All package references correct in .csproj files
- ✅ Test project has packages for types used directly in test code
- ✅ All `using` statements present and namespaces match folder structure
- ✅ All project references correct
- ✅ No syntax errors visible in code review
- ✅ New interfaces have implementations registered in Program.cs
- ✅ Test mocks match actual method signatures
- ✅ Commit message notes CI/CD dependency

**Only commit if you answer YES to all checks above. Monitor GitHub Actions immediately after push.**

---

#### ❌ What NOT to Commit

**NEVER commit if:**
- ❌ **You didn't verify contracts** (property/method names in types you use)
- ❌ Build has errors or warnings (if SDK available)
- ❌ Any tests are failing (if SDK available)
- ❌ You haven't run `dotnet test` (if SDK available)
- ❌ You added new files without verifying package references (if SDK NOT available)
- ❌ Code contains `// TODO: Fix this before commit`
- ❌ Code contains hardcoded secrets or environment-specific values
- ❌ You made changes to interfaces without updating implementations
- ❌ You added dependencies without documenting why
- ❌ Test project is missing package references for types used in tests

#### 🚨 Emergency Fixes

If CI/CD fails after you push:

```bash
# 1. Pull the branch
git pull origin claude/your-branch

# 2. Clean and rebuild
dotnet clean
dotnet restore
dotnet build --no-incremental

# 3. Run all tests
dotnet test

# 4. Fix any errors

# 5. Commit fix
git add .
git commit -m "fix: resolve build/test failures"

# 6. Push
git push -u origin claude/your-branch
```

#### Summary: The Golden Rule

**BUILD + TEST = SUCCESS**

```bash
# ALWAYS run this before committing:
dotnet clean && \
dotnet restore && \
dotnet build --no-incremental && \
dotnet test

# If ALL steps succeed → Safe to commit
# If ANY step fails → DO NOT commit until fixed
```

### Git Push Requirements

**⚠️ CRITICAL: Always pull before push to avoid conflicts**

#### Pre-Push Procedure (MANDATORY)

**Before EVERY `git push`, follow these steps:**

```bash
# Step 1: Fetch latest changes from remote
git fetch origin <branch-name>

# Step 2: Pull and merge remote changes
git pull origin <branch-name> --no-rebase

# Step 3: If merge conflicts occur, resolve them (see below)
# Step 4: After resolving conflicts, rebuild and test
dotnet build && dotnet test

# Step 5: Only then push to remote
git push -u origin <branch-name>
```

#### Push Requirements Checklist

- ✅ **ALWAYS pull before push** - Prevents merge conflicts and rejected pushes
- ✅ **ALWAYS use** `git push -u origin <branch-name>` - Sets upstream tracking
- ✅ **Branch MUST start with** `claude/` and end with session ID - Required for permission
- ✅ **Rebuild and test after merges** - Ensures merged code still works
- ✅ **Retry on network errors** - Up to 4 times with exponential backoff (2s, 4s, 8s, 16s)
- ❌ **NEVER force push to main/master** - Destroys history and breaks collaboration
- ❌ **NEVER push without pulling first** - Causes conflicts and wasted time

#### Handling Merge Conflicts

**When `git pull` reports conflicts:**

```bash
# Step 1: Identify conflicted files
git status
# Look for "both modified:" entries

# Step 2: Open each conflicted file and look for conflict markers
# <<<<<<< HEAD
# Your changes
# =======
# Remote changes
# >>>>>>> branch-name

# Step 3: Resolve conflicts by:
# - Keeping your changes, OR
# - Keeping remote changes, OR
# - Combining both (most common)
# - Remove conflict markers (<<<<<<, =======, >>>>>>>)

# Step 4: Stage resolved files
git add <resolved-file-1> <resolved-file-2>

# Step 5: Complete the merge
git commit -m "merge: resolve conflicts with remote changes"

# Step 6: CRITICAL - Rebuild and test after merge
dotnet clean
dotnet restore
dotnet build --no-incremental
dotnet test

# Step 7: Only if build and tests pass, push
git push -u origin <branch-name>
```

#### Common Conflict Scenarios

**Scenario 1: Remote has new commits**
```bash
# Your push is rejected: "Updates were rejected because the remote contains work..."
# Solution: Pull, merge, test, push
git pull origin <branch-name> --no-rebase
dotnet build && dotnet test
git push -u origin <branch-name>
```

**Scenario 2: Conflicting changes in same file**
```bash
# git pull reports: "CONFLICT (content): Merge conflict in src/File.cs"
# Solution: Resolve conflicts manually
git status  # See conflicted files
# Edit files to resolve conflicts
git add src/File.cs
git commit -m "merge: resolve conflicts in File.cs"
dotnet build && dotnet test
git push -u origin <branch-name>
```

**Scenario 3: Network failure during push**
```bash
# Push fails with: "fatal: unable to access... Could not resolve host"
# Solution: Retry with exponential backoff

# Attempt 1
git push -u origin <branch-name>
# If fails, wait 2 seconds

# Attempt 2
sleep 2
git push -u origin <branch-name>
# If fails, wait 4 seconds

# Attempt 3
sleep 4
git push -u origin <branch-name>
# If fails, wait 8 seconds

# Attempt 4
sleep 8
git push -u origin <branch-name>
# If fails, wait 16 seconds

# Attempt 5 (final)
sleep 16
git push -u origin <branch-name>
# If still fails, report network issue to user
```

#### Emergency: When Push is Blocked

**If push fails with permission error (HTTP 403):**

```bash
# Error: "remote: Permission to repository denied"
# Check: Branch name MUST match pattern: claude/*-<session-id>

# ❌ WRONG: claude/my-feature (missing session ID)
# ✅ CORRECT: claude/my-feature-01HQxN4hS7696T2bepqRT3VW

# Solution: Create correctly named branch
git checkout -b claude/my-feature-<session-id>
git cherry-pick <commit-hash>  # Move commits to new branch
git push -u origin claude/my-feature-<session-id>
```

#### Best Practices

1. **Pull frequently** - Before starting work, after breaks, before pushing
2. **Commit small, push often** - Smaller commits = easier conflict resolution
3. **Test after every merge** - Merged code may break tests even if no conflicts
4. **Never ignore conflicts** - Always resolve properly, never force push
5. **Document complex merges** - Add details to merge commit message

## AI Assistant Guidelines

### ⚠️ MOST IMPORTANT RULE

**Before EVERY commit, you MUST:**

```bash
dotnet clean && dotnet restore && dotnet build --no-incremental && dotnet test
```

**If ANY command fails → DO NOT commit. Fix the errors first.**

This is not optional. CI/CD failures waste time and resources. See the detailed [Pre-Commit Checklist](#️-critical-pre-commit-checklist) below.

---

### Initial Analysis Checklist
When starting a new task:

**Step 0: Verify .NET SDK Installation (CRITICAL - DO THIS FIRST)**
```bash
# Check if .NET SDK is installed
dotnet --version

# If command fails or version < 8.0, install .NET SDK 8.0
# Follow installation instructions in "Development Environment Setup" section above
# Windows: winget install Microsoft.DotNet.SDK.8
# Linux: See "Installing .NET SDK on Linux" section
# macOS: brew install dotnet@8

# Verify installation succeeded
dotnet --version
# Expected: 8.0.x or later

# If in Claude Code web environment without .NET SDK:
# - Document this limitation in your work
# - Follow "Alternative: No .NET SDK Checklist" for pre-commit validation
# - Rely on CI/CD for build/test verification
```

**⚠️ CRITICAL**: Never proceed with coding tasks without first verifying .NET SDK availability. If unavailable, you MUST follow the alternative checklist to avoid build failures.

After verifying .NET SDK:

1. **Read relevant files** before making changes
2. **Check for existing patterns** in the codebase
3. **Verify .NET SDK version** compatibility (must be 8.0+)
4. **Review dependencies** and their versions
5. **Check for existing tests** to understand expected behavior and testing patterns

### Test-Driven Development (TDD) Workflow

**💡 TIP:** Use the `/tdd-helper` skill for guided Red-Green-Refactor workflow. See [SKILLS.md](SKILLS.md#tdd-helper) for details.

**⚠️ MANDATORY**: All coding tasks MUST follow Test-Driven Development (TDD) principles. This is not optional.

#### Why TDD is Mandatory

1. **Prevents regressions** - Tests catch breaking changes immediately
2. **Improves design** - Writing tests first leads to better API design
3. **Documents behavior** - Tests serve as living documentation
4. **Reduces debugging time** - Issues are caught during development, not in CI/CD
5. **Ensures testability** - Code is designed to be testable from the start

#### The Red-Green-Refactor Cycle

All code changes must follow this cycle:

```
🔴 RED → 🟢 GREEN → 🔵 REFACTOR
```

1. **🔴 RED** - Write a failing test that defines desired behavior
2. **🟢 GREEN** - Write minimal code to make the test pass
3. **🔵 REFACTOR** - Improve code quality while keeping tests green

#### TDD Workflow for This Project

**For NEW Features:**

```bash
# Step 1: 🔴 RED - Write failing test(s)
# Navigate to tests/HotSwap.Distributed.Tests/

# Create or edit test file (e.g., UserAuthenticationTests.cs)
# Write test(s) that define the expected behavior

# Example test structure:
```csharp
[Fact]
public async Task AuthenticateAsync_WithValidCredentials_ReturnsToken()
{
    // Arrange
    var mockRepo = new Mock<IUserRepository>();
    var service = new AuthenticationService(mockRepo.Object);

    mockRepo.Setup(x => x.GetUserAsync("testuser", It.IsAny<CancellationToken>()))
        .ReturnsAsync(new User { Username = "testuser", PasswordHash = "hash" });

    // Act
    var result = await service.AuthenticateAsync("testuser", "password");

    // Assert
    result.Should().NotBeNull();
    result.Token.Should().NotBeEmpty();
}
```

```bash
# Step 2: Run the test - it should FAIL (RED)
dotnet test --filter "FullyQualifiedName~AuthenticateAsync_WithValidCredentials"

# Expected output: Test Failed (because implementation doesn't exist yet)
# If test passes without implementation → test is wrong, fix the test

# Step 3: 🟢 GREEN - Implement minimal code to pass the test
# Create or edit source file (e.g., src/HotSwap.Distributed.Api/Services/AuthenticationService.cs)
# Write ONLY enough code to make the test pass

# Step 4: Run the test again - it should PASS (GREEN)
dotnet test --filter "FullyQualifiedName~AuthenticateAsync_WithValidCredentials"

# Expected output: Test Passed
# If test still fails → fix implementation, not the test

# Step 5: 🔵 REFACTOR - Improve code quality
# - Extract methods
# - Improve naming
# - Add error handling
# - Add XML documentation
# - Ensure SOLID principles

# Step 6: Run ALL tests to ensure refactoring didn't break anything
dotnet test

# Expected output: All tests pass
# If any test fails → fix the issue before proceeding

# Step 7: Repeat for next test case (edge cases, error cases, etc.)
```

**For BUG Fixes:**

```bash
# Step 1: 🔴 RED - Write a test that reproduces the bug
# The test should FAIL, demonstrating the bug exists

# Step 2: 🟢 GREEN - Fix the bug
# Modify source code to make the test pass

# Step 3: 🔵 REFACTOR - Clean up if needed
# Improve code quality while keeping all tests green

# Step 4: Run all tests
dotnet test

# Expected: All tests pass, including the new bug reproduction test
```

**For REFACTORING:**

```bash
# Step 1: Ensure existing tests exist and pass
dotnet test

# If no tests exist → STOP and write tests first (follow "New Feature" workflow)

# Step 2: 🔵 REFACTOR - Modify code structure
# Keep behavior identical, only change internal structure

# Step 3: Run tests continuously during refactoring
dotnet test

# Tests should ALWAYS pass - if they fail, revert and try smaller steps

# Step 4: Verify all tests still pass
dotnet test
```

#### TDD Best Practices for This Project

1. **Test Naming Convention**:
   - `MethodName_StateUnderTest_ExpectedBehavior`
   - Example: `CreateDeployment_WithInvalidRequest_ReturnsBadRequest`

2. **Test Organization (AAA Pattern)**:
   ```csharp
   [Fact]
   public async Task MethodName_StateUnderTest_ExpectedBehavior()
   {
       // Arrange - Set up test data and mocks
       var mockDependency = new Mock<IDependency>();
       var sut = new SystemUnderTest(mockDependency.Object);

       // Act - Execute the method being tested
       var result = await sut.MethodAsync(parameters);

       // Assert - Verify expected behavior using FluentAssertions
       result.Should().NotBeNull();
       result.Property.Should().Be(expectedValue);
   }
   ```

3. **Mock Setup Patterns**:
   ```csharp
   // ✅ CORRECT: Mock setup matches actual method signature
   mockRepo.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
       .ReturnsAsync(expectedResult);

   // ❌ WRONG: Mock setup doesn't match signature
   mockRepo.Setup(x => x.GetAsync(It.IsAny<string>()))  // Missing CancellationToken!
       .ReturnsAsync(expectedResult);
   ```

4. **Assertion Patterns** (using FluentAssertions):
   ```csharp
   // ✅ Use FluentAssertions for readable assertions
   result.Should().NotBeNull();
   result.Items.Should().HaveCount(5);
   result.Name.Should().Be("Expected Name");
   result.Status.Should().Be(DeploymentStatus.Running);

   // ❌ Avoid xUnit Assert (less readable)
   Assert.NotNull(result);
   Assert.Equal(5, result.Items.Count);
   ```

5. **Test Coverage Requirements**:
   - **Happy path** - Normal successful execution
   - **Edge cases** - Boundary conditions, empty inputs, null values
   - **Error cases** - Invalid input, exceptions, failure scenarios
   - **Async patterns** - Cancellation, timeouts, concurrent access

#### Example: Complete TDD Workflow

**Scenario**: Add rate limiting to API endpoint

```bash
# 1️⃣ 🔴 RED - Write failing test
# File: tests/HotSwap.Distributed.Tests/Middleware/RateLimitingMiddlewareTests.cs

[Fact]
public async Task InvokeAsync_WhenRateLimitExceeded_ReturnsStatus429()
{
    // Arrange
    var middleware = new RateLimitingMiddleware(next: null, options);
    var context = CreateHttpContext();

    // Act - Make 101 requests (limit is 100)
    for (int i = 0; i < 101; i++)
    {
        await middleware.InvokeAsync(context);
    }

    // Assert
    context.Response.StatusCode.Should().Be(429);
}

# Run test - it FAILS (good!)
dotnet test --filter "FullyQualifiedName~RateLimitingMiddlewareTests"
# Output: Test Failed - middleware doesn't exist yet

# 2️⃣ 🟢 GREEN - Implement minimal code
# File: src/HotSwap.Distributed.Api/Middleware/RateLimitingMiddleware.cs

public class RateLimitingMiddleware
{
    private static int _requestCount = 0;

    public async Task InvokeAsync(HttpContext context)
    {
        _requestCount++;
        if (_requestCount > 100)
        {
            context.Response.StatusCode = 429;
            return;
        }
        await _next(context);
    }
}

# Run test - it PASSES (good!)
dotnet test --filter "FullyQualifiedName~RateLimitingMiddlewareTests"
# Output: Test Passed

# 3️⃣ 🔵 REFACTOR - Improve implementation
# - Add proper sliding window algorithm
# - Add configuration
# - Add logging
# - Add XML documentation
# - Use dependency injection

# Run ALL tests after refactoring
dotnet test
# Output: All tests pass

# 4️⃣ Add more tests (repeat cycle)
# - Test rate limit reset
# - Test different endpoints
# - Test concurrent requests
# - Test configuration options
```

#### Integration with TodoWrite Tool

When using TDD, structure your todos as follows:

```bash
TodoWrite:
- 🔴 Write test for [feature] - Status: in_progress
- 🟢 Implement [feature] to pass test - Status: pending
- 🔵 Refactor [feature] implementation - Status: pending
- ✅ Verify all tests pass - Status: pending
```

Mark each step complete only when:
- 🔴 RED: Test written and FAILING
- 🟢 GREEN: Implementation complete and test PASSING
- 🔵 REFACTOR: Code improved and ALL tests PASSING
- ✅ VERIFY: `dotnet test` succeeds with zero failures

#### When to Skip TDD

**NEVER**. TDD is mandatory for all code changes in this project.

Even for:
- "Quick fixes" - Write test first to verify the fix
- "Simple changes" - Tests prevent future regressions
- "Documentation updates" - Docs changes don't need tests, but code does
- "Refactoring" - Existing tests must pass, add tests if missing

#### TDD Checklist

Before marking any coding task as complete:

- ✅ Tests were written BEFORE implementation
- ✅ Tests initially FAILED (RED)
- ✅ Implementation makes tests PASS (GREEN)
- ✅ Code was REFACTORED for quality (BLUE)
- ✅ ALL tests pass (`dotnet test` shows zero failures)
- ✅ Test coverage includes happy path, edge cases, and error cases
- ✅ Tests use AAA pattern (Arrange-Act-Assert)
- ✅ Tests use FluentAssertions for readable assertions
- ✅ Mock setups match actual method signatures
- ✅ Test naming follows `MethodName_StateUnderTest_ExpectedBehavior`

**If you cannot answer YES to all items above → Task is NOT complete.**

### Code Generation Standards
- **Always prefer editing** existing files over creating new ones
- **Maintain consistency** with existing code style
- **Include XML documentation** for public APIs
- **Add appropriate error handling** (try-catch, null checks)
- **Consider security implications** (avoid SQL injection, XSS, etc.)
- **Use async/await** for I/O operations
- **Dispose resources properly** (use `using` statements)

### Security Best Practices
- **Never commit secrets** (.env files, connection strings, API keys)
- **Validate all input** (especially user input)
- **Use parameterized queries** for database access
- **Implement proper authentication/authorization**
- **Follow OWASP Top 10** guidelines
- **Sanitize output** to prevent XSS

### Testing Requirements

**💡 TIP:** Use the `/test-coverage-analyzer` skill to maintain 85%+ coverage. See [SKILLS.md](SKILLS.md#test-coverage-analyzer) for details.

**⚠️ MANDATORY: Follow Test-Driven Development (TDD) for ALL code changes**

This project enforces Test-Driven Development. See the [Test-Driven Development (TDD) Workflow](#test-driven-development-tdd-workflow) section above for complete guidelines.

**Key Requirements:**

1. **Write tests BEFORE implementation** (not after)
   - Tests must be written FIRST, following Red-Green-Refactor cycle
   - Implementation comes AFTER tests are written
   - This is mandatory, not optional

2. **⚠️ CRITICAL: Run `dotnet test` before EVERY commit** (see Pre-Commit Checklist)
   - Zero test failures allowed
   - Zero skipped tests (unless explicitly documented why)
   - All new code must have corresponding tests

3. **Test Coverage Requirements**:
   - **Happy path** - Normal successful execution
   - **Edge cases** - Boundary conditions, empty inputs, null values
   - **Error cases** - Invalid input, exceptions, failure scenarios
   - Target >80% code coverage (current: 85%+)

4. **Testing Patterns**:
   - Use **xUnit** for test framework
   - Use **Moq** for mocking dependencies
   - Use **FluentAssertions** for readable assertions
   - Follow **AAA pattern** (Arrange-Act-Assert)
   - Follow **naming convention**: `MethodName_StateUnderTest_ExpectedBehavior`

5. **Mock external dependencies** in unit tests
   - Database access → Mock repository interfaces
   - HTTP calls → Mock HttpClient or service interfaces
   - File I/O → Mock file system abstractions
   - Time → Mock IClock or similar time abstraction

6. **Integration tests** for critical workflows
   - Deployment pipeline end-to-end tests
   - API endpoint integration tests
   - Authentication and authorization flows

7. **Never commit failing tests** - CI/CD will catch this and fail the build
   - Run `dotnet test` locally before every commit
   - Fix failing tests immediately, never "comment out" failing tests
   - If test is flaky, fix the flakiness, don't skip the test

**Example TDD Workflow:**
```bash
# ❌ WRONG: Implementation first, tests later
1. Write implementation code
2. Write tests (maybe)
3. Run tests
4. Commit

# ✅ CORRECT: Tests first (TDD)
1. 🔴 Write failing test
2. 🟢 Write minimal implementation to pass test
3. 🔵 Refactor code for quality
4. Run ALL tests (`dotnet test`)
5. Commit (only if all tests pass)
```

**See [Test-Driven Development (TDD) Workflow](#test-driven-development-tdd-workflow) for detailed guidance.**

### File Operations
- Use **Read** tool for viewing files
- Use **Edit** tool for modifying existing files
- Use **Write** tool only for new files
- Use **Glob** for finding files by pattern
- Use **Grep** for searching code content

### Communication Standards
- **Output text directly** - don't use echo or comments to communicate
- **Be concise** in explanations
- **Use markdown** for formatting
- **Include file references** in format `file:line`
- **No emojis** unless explicitly requested

### Task Management
- **Use TodoWrite** at the start of complex tasks
- **Mark todos in_progress** before starting work
- **⚠️ ONLY mark completed after:**
  1. Code builds successfully (`dotnet build --no-incremental`)
  2. ALL tests pass (`dotnet test`)
  3. Changes are committed and pushed
- **Only one in_progress** task at a time
- **Break down complex tasks** into smaller steps
- **Never mark a task complete if build or tests fail**

### Working with TASK_LIST.md

**Overview:**
TASK_LIST.md is the project's comprehensive task roadmap, containing 20+ prioritized tasks derived from analyzing all project documentation. It serves as the single source of truth for planned enhancements, known gaps, and future work.

**When to Consult TASK_LIST.md:**
1. **At the start of any session** - Review to understand project priorities
2. **When asked "what needs to be done"** - Reference the task list
3. **Before proposing new features** - Check if already documented
4. **When planning work** - Use priorities and effort estimates
5. **After completing tasks** - Update status and add new tasks discovered

**Task List Structure:**
```
TASK_LIST.md
├── Major Tasks (High Priority)
│   ├── Task #1-5: Critical items (Auth, Security, etc.)
│   └── Detailed requirements, acceptance criteria, effort estimates
├── Minor Tasks (Medium Priority)
│   ├── Task #6-10: Enhancements (WebSocket, Prometheus, etc.)
│   └── Implementation guidance and benefits
├── Low Priority Tasks
│   ├── Task #11-14: Nice-to-have features
│   └── Optional enhancements
└── Summary Statistics
    ├── Tasks by priority, status, effort
    └── Recommended sprint planning
```

**How to Use TASK_LIST.md:**

1. **Read Before Starting Work:**
   ```bash
   # Always review the task list first
   Read TASK_LIST.md
   # Look for relevant tasks in your area
   ```

2. **Reference When Planning:**
   - Check task priorities (🔴 Critical, 🟡 High, 🟢 Medium, ⚪ Low)
   - Review effort estimates (1-7 days)
   - Check dependencies between tasks
   - Follow recommended sprint order

3. **Update After Implementation:**
   When you complete a task from the list:
   - Update the task status from ⏳ to ✅
   - Add implementation notes if helpful
   - Document any discovered issues or dependencies
   - Add any new tasks that emerged during implementation

4. **Add New Tasks:**
   When you discover new work:
   - Add to appropriate priority section
   - Include: Priority, Status, Effort estimate
   - Add requirements and acceptance criteria
   - Link to relevant documentation or issues

**Task Status Indicators:**
- ⏳ **Pending** - Not yet started
- ✅ **Completed** - Fully implemented and tested
- 🔄 **In Progress** - Currently being worked on
- ⚠️ **Blocked** - Waiting on dependency or decision

**Task Priority Levels:**
- 🔴 **Critical** - Required for production (security, auth, HTTPS)
- 🟡 **High** - Important for enterprise use (approval workflow, audit logs)
- 🟢 **Medium** - Valuable enhancements (WebSocket, Prometheus, Helm)
- ⚪ **Low** - Nice-to-have features (GraphQL, ML, multi-tenancy)

**Best Practices:**

1. **Keep It Current:**
   - Update task status immediately after completing work
   - Add discovered tasks as soon as identified
   - Revise effort estimates if reality differs significantly
   - Remove tasks that become obsolete

2. **Maintain Quality:**
   - Each task should have clear requirements
   - Include acceptance criteria for verification
   - Document dependencies and prerequisites
   - Add references to related documentation

3. **Communicate Changes:**
   - Commit TASK_LIST.md updates separately or with related code
   - Use descriptive commit messages for task list changes
   - Example: `docs: update TASK_LIST.md - mark rate limiting as completed`

4. **Reference in Commits:**
   When implementing a task from the list, reference it in commit messages:
   ```bash
   git commit -m "feat: implement JWT authentication (Task #1 from TASK_LIST.md)"
   ```

**Example Workflow:**

```markdown
# At start of session
1. Read TASK_LIST.md to understand priorities
2. User asks: "Add rate limiting to the API"
3. Check TASK_LIST.md - find Task #5: API Rate Limiting
4. Review requirements, effort estimate (1 day), priority (Medium)
5. Implement the feature following the documented requirements
6. Update TASK_LIST.md:
   - Change status from ⏳ to ✅
   - Add completion notes: "Implemented in src/Middleware/RateLimitingMiddleware.cs"
7. Commit both code and TASK_LIST.md update
```

**When to Create a New TASK_LIST.md:**
- If the current one becomes too large (>50 tasks)
- Archive old completed tasks to TASK_HISTORY.md
- Start fresh TASK_LIST.md with only pending/in-progress tasks
- Keep the summary statistics section updated

**Integration with Other Documents:**
- **ENHANCEMENTS.md** - Documents completed enhancements in detail
- **PROJECT_STATUS_REPORT.md** - References task list for next steps
- **README.md** - May link to high-priority tasks
- **SPEC_COMPLIANCE_REVIEW.md** - Identifies gaps that become tasks

**Common Mistakes to Avoid:**
- ❌ Don't skip reading TASK_LIST.md when starting work
- ❌ Don't implement features without checking if they're documented
- ❌ Don't forget to update status after completing tasks
- ❌ Don't add duplicate tasks without checking existing entries
- ❌ Don't ignore task priorities (Critical tasks should be done first)

**Example Task Entry Format:**

```markdown
### N. Task Name
**Priority:** 🔴 Critical
**Status:** ⏳ Not Implemented
**Effort:** 2-3 days
**References:** README.md:238, PROJECT_STATUS_REPORT.md:514

**Requirements:**
- [ ] Requirement 1
- [ ] Requirement 2
- [ ] Requirement 3

**Acceptance Criteria:**
- Feature works as described
- Tests pass
- Documentation updated

**Impact:** High - Critical for production security
```

### Error Handling
- **Read error messages carefully** before attempting fixes
- **Check build output** for warnings and errors
- **Verify tests pass** after changes
- **Fix security vulnerabilities** immediately when discovered
- **Don't ignore warnings** - address them

## Common .NET Commands Reference

### Solution Management
```bash
# Create new solution
dotnet new sln -n [SolutionName]

# Add project to solution
dotnet sln add [path/to/project.csproj]

# List projects in solution
dotnet sln list
```

### Project Management
```bash
# Create new console app
dotnet new console -n [ProjectName]

# Create new class library
dotnet new classlib -n [ProjectName]

# Create new web API
dotnet new webapi -n [ProjectName]

# Create new test project
dotnet new xunit -n [ProjectName].Tests
```

### Package Management
```bash
# Add package
dotnet add package [PackageName]

# Remove package
dotnet remove package [PackageName]

# List packages
dotnet list package

# Update packages
dotnet list package --outdated
```

### Running and Publishing
```bash
# Run application
dotnet run

# Run specific project
dotnet run --project [path/to/project.csproj]

# Publish application
dotnet publish -c Release -o ./publish
```

## Documentation Standards

### Code Documentation
- **XML comments** for all public APIs
- **README.md** for project overview
- **Inline comments** for complex logic only
- **Architecture docs** for design decisions

### XML Documentation Example
```csharp
/// <summary>
/// Authenticates a user with the provided credentials.
/// </summary>
/// <param name="username">The username to authenticate.</param>
/// <param name="password">The user's password.</param>
/// <returns>An authentication token if successful, null otherwise.</returns>
/// <exception cref="ArgumentNullException">Thrown when username or password is null.</exception>
public async Task<string?> AuthenticateAsync(string username, string password)
{
    // Implementation
}
```

### Avoiding Stale Documentation

**💡 TIP:** Use the `/doc-sync-check` skill to validate documentation synchronization. See [SKILLS.md](SKILLS.md#doc-sync-check) for details.

**⚠️ CRITICAL**: Stale documentation is worse than no documentation. Outdated docs mislead developers, waste time, and cause bugs. Follow these practices to keep documentation current.

#### Mandatory Documentation Update Triggers

**ALWAYS update documentation when you:**

1. **Change public APIs** - Update XML comments, README, and API docs
   ```bash
   # Before committing API changes:
   # 1. Update XML documentation in code
   # 2. Update README.md if it mentions the API
   # 3. Update any architecture docs that reference it
   # 4. Update examples that use the API
   ```

2. **Add or remove NuGet packages** - Update CLAUDE.md Technology Stack section
   ```bash
   # After adding/removing packages:
   # 1. Update "Technology Stack" section in CLAUDE.md
   # 2. Include version numbers
   # 3. Update README.md if user-facing
   # 4. Update setup instructions if installation steps changed
   ```

3. **Change build/test processes** - Update CLAUDE.md and README.md
   ```bash
   # If build or test commands change:
   # 1. Update Pre-Commit Checklist in CLAUDE.md
   # 2. Update Development Environment Setup in CLAUDE.md
   # 3. Update CI/CD documentation if applicable
   # 4. Update validation scripts (validate-code.sh, test-critical-paths.sh)
   ```

4. **Update test counts** - Update multiple docs with current test counts
   ```bash
   # After adding/removing tests:
   # Files to update:
   # - CLAUDE.md (line 16: "Build Status: ✅ Passing (X/X tests)")
   # - CLAUDE.md (line 115: "Project Metrics" table in Quick Reference)
   # - CLAUDE.md (line 388: expected test count in "First Time Build")
   # - CLAUDE.md (line 435: expected test count in "Run All Tests")
   # - CLAUDE.md (line 473: expected test count in "Critical Path Tests")
   # - README.md (test count badge if present)
   # - PROJECT_STATUS_REPORT.md (test statistics)

   # Quick grep to find all occurrences:
   grep -n "Passed.*tests\|tests.*Passed\|Total.*tests" CLAUDE.md README.md
   ```

5. **Change project structure** - Update CLAUDE.md Project Structure section
   ```bash
   # If you add/remove/move projects or folders:
   # 1. Update "Project Structure" tree in CLAUDE.md (lines 20-50)
   # 2. Update "Key Components" section in CLAUDE.md
   # 3. Update README.md structure section
   # 4. Update file references in other docs
   ```

6. **Complete tasks from TASK_LIST.md** - Update multiple docs
   ```bash
   # When completing a task:
   # 1. Update TASK_LIST.md (status from ⏳ to ✅)
   # 2. Update PROJECT_STATUS_REPORT.md (if status changed)
   # 3. Update ENHANCEMENTS.md (add implementation details)
   # 4. Update README.md (if user-facing feature)
   # 5. Update CLAUDE.md (if it affects setup, testing, or workflows)
   ```

7. **Change configuration or environment** - Update setup docs
   ```bash
   # If prerequisites, ports, URLs, or env vars change:
   # 1. Update "Development Environment Setup" in CLAUDE.md
   # 2. Update docker-compose.yml documentation
   # 3. Update .env.example if exists
   # 4. Update troubleshooting section if new issues may arise
   ```

8. **Change Docker configuration** - Update CLAUDE.md, README.md, and docker-compose.yml
   ```bash
   # If Dockerfile or docker-compose.yml changes:
   # 1. Update "Docker Development and Maintenance" in CLAUDE.md
   # 2. Update "Running with Docker Compose" section in CLAUDE.md if applicable
   # 3. Update README.md Docker Quickstart section
   # 4. Update environment variable documentation
   # 5. Update port numbers and service URLs if changed
   # 6. Test full docker-compose stack before committing (see Pre-Commit Checklist Step 7)
   # 7. Update .dockerignore if build context changes
   # 8. Add inline comments to docker-compose.yml explaining configuration
   ```

#### Documentation Synchronization Checklist

**Before EVERY commit that includes code changes, verify:**

```bash
# 1. Check for API signature changes
git diff --staged | grep -E "public|internal|protected" | grep -E "class|interface|method|property"

# If API changes found → Update XML documentation, README, architecture docs

# 2. Check for package.json or .csproj changes
git diff --staged | grep -E "PackageReference|TargetFramework"

# If package changes found → Update CLAUDE.md Technology Stack section

# 3. Check for test file changes
git diff --staged tests/

# If test changes found → Run dotnet test, update test counts in docs

# 4. Check for documentation file changes
git diff --staged | grep -E "\.md$"

# If doc changes found → Verify dates are updated in Changelog

# 5. Verify all edited docs have current dates
grep -n "Last Updated:\|### 2025-" CLAUDE.md README.md
```

#### Version Tracking Requirements

**All documentation files MUST include:**

1. **Last Updated Date** at the top
   ```markdown
   **Last Updated**: 2025-11-15
   ```

2. **Changelog Section** at the bottom
   ```markdown
   ## Changelog

   ### 2025-11-15 (Description of Changes)
   - Specific change 1
   - Specific change 2
   ```

3. **Version-Specific Information** when applicable
   ```markdown
   **For .NET 8.0+**: Use this approach
   **For .NET 6.0-7.0**: Use legacy approach
   ```

#### Documentation Review Process

**Monthly Documentation Audit (AI Assistant Responsibility):**

When starting a new session at the beginning of a month, perform this audit:

```bash
# 1. Check "Last Updated" dates in all docs
grep -r "Last Updated:" *.md

# 2. Identify docs older than 90 days
# These need review even if code hasn't changed

# 3. Verify test counts match actual test count
dotnet test --verbosity quiet
# Compare output with documented test counts

# 4. Verify package versions match actual packages
dotnet list package
# Compare output with Technology Stack in CLAUDE.md

# 5. Check for broken file references
grep -r "src/.*\.cs\|tests/.*\.cs" *.md
# Verify these files still exist

# 6. Validate code examples compile
# Extract code blocks from README.md and verify they compile
```

#### Automated Documentation Validation

**Create validation script (docs-check.sh):**

```bash
#!/bin/bash
# docs-check.sh - Validates documentation freshness

echo "🔍 Checking for stale documentation..."

# Check 1: Verify test counts match
ACTUAL_TESTS=$(dotnet test --verbosity quiet 2>&1 | grep -oP "Passed:\s+\K\d+")
DOCUMENTED_TESTS=$(grep -oP "Passing \(\K\d+" CLAUDE.md | head -1)

if [ "$ACTUAL_TESTS" != "$DOCUMENTED_TESTS" ]; then
    echo "❌ Test count mismatch: Actual=$ACTUAL_TESTS, Documented=$DOCUMENTED_TESTS"
    echo "   Update CLAUDE.md line 16, 309, 351, 389"
    exit 1
fi

# Check 2: Verify package versions are documented
UNDOCUMENTED_PACKAGES=$(dotnet list package | grep ">" | awk '{print $2}' | while read pkg; do
    grep -q "$pkg" CLAUDE.md || echo "$pkg"
done)

if [ -n "$UNDOCUMENTED_PACKAGES" ]; then
    echo "❌ Undocumented packages found:"
    echo "$UNDOCUMENTED_PACKAGES"
    echo "   Update CLAUDE.md Technology Stack section"
    exit 1
fi

# Check 3: Verify "Last Updated" is recent (within 30 days)
LAST_UPDATED=$(grep "Last Updated:" CLAUDE.md | grep -oP "\d{4}-\d{2}-\d{2}")
DAYS_OLD=$(( ($(date +%s) - $(date -d "$LAST_UPDATED" +%s)) / 86400 ))

if [ $DAYS_OLD -gt 30 ]; then
    echo "⚠️  CLAUDE.md last updated $DAYS_OLD days ago (review recommended)"
fi

echo "✅ Documentation validation passed"
```

**Run before major releases:**
```bash
chmod +x docs-check.sh
./docs-check.sh
```

#### Documentation-in-Code Proximity

**Keep docs close to what they document:**

1. **XML documentation** - Directly above code elements
   ```csharp
   /// <summary>Documentation here</summary>
   public class MyClass { }
   ```

2. **Component README** - In same directory as component
   ```
   src/HotSwap.Distributed.Api/
   ├── README.md           # API-specific docs
   ├── Controllers/
   └── Models/
   ```

3. **Test documentation** - In test file comments
   ```csharp
   // Test covers: User authentication with valid credentials
   // Related docs: CLAUDE.md#authentication, README.md#security
   [Fact]
   public async Task AuthenticateAsync_WithValidCredentials_ReturnsToken() { }
   ```

#### Deprecation and Outdated Content

**When documenting deprecated features:**

```markdown
## ⚠️ DEPRECATED: Old Feature Name

**Deprecated**: 2025-11-15
**Removed In**: v2.0.0
**Replacement**: Use `NewFeatureName` instead
**Migration Guide**: See [migration.md](migration.md)

~~Old documentation content strikethrough~~
```

**When removing outdated sections:**

```bash
# Don't just delete - add changelog entry
git commit -m "docs: remove outdated XYZ section from CLAUDE.md

Section documented legacy behavior that was removed in commit abc123.
See CHANGELOG.md for details."
```

#### Documentation Testing

**Test all code examples in documentation:**

```bash
# Before committing docs with code examples:

# 1. Extract code examples to temp files
cat README.md | grep -A 10 '```csharp' > temp_examples.txt

# 2. Validate they compile (manual check for now)
# TODO: Automate this with a script

# 3. Run examples if they're runnable
cd examples/ApiUsageExample
dotnet run
# Verify output matches documented output
```

**Validate all command examples:**

```bash
# Test documented commands actually work:

# From CLAUDE.md Development Environment Setup:
dotnet --version          # Should succeed
dotnet restore            # Should succeed
dotnet build              # Should succeed
dotnet test               # Should succeed

# If any fail → Update documentation with correct commands
```

#### Common Stale Documentation Patterns to Avoid

**❌ DON'T:**

1. **Copy-paste from other projects** without verifying accuracy
   ```markdown
   ❌ "This project uses .NET 6.0" (when it uses .NET 8.0)
   ❌ "Run npm install" (when it's a .NET project)
   ```

2. **Document implementation details** that change frequently
   ```markdown
   ❌ "The UserService.cs file is located at line 42 of..."
   ✅ "The UserService implements IUserService interface"
   ```

3. **Hardcode version numbers** without a plan to update
   ```markdown
   ❌ "As of version 1.2.3, the API supports..."
   ✅ "The API supports feature X (added in v1.2.3)"
   ```

4. **Leave TODO comments** in documentation
   ```markdown
   ❌ "TODO: Update this section when feature is complete"
   ✅ Complete the section or remove it
   ```

5. **Document temporary workarounds** without expiration dates
   ```markdown
   ❌ "Use this workaround for now"
   ✅ "Temporary workaround (until issue #123 is fixed): ..."
   ```

**✅ DO:**

1. **Use relative references** that auto-update
   ```markdown
   ✅ "See the [Authentication section](#authentication)"
   ✅ "Refer to TASK_LIST.md for current priorities"
   ```

2. **Document behavior, not implementation**
   ```markdown
   ✅ "The service authenticates users via JWT tokens"
   ❌ "The service uses the BCrypt library at version 4.0.3"
   ```

3. **Link to authoritative sources**
   ```markdown
   ✅ "Follows [Microsoft .NET coding conventions](URL)"
   ❌ Copy-pasting conventions that may change
   ```

4. **Date all temporal statements**
   ```markdown
   ✅ "As of 2025-11-15, the project supports..."
   ❌ "Currently, the project supports..."
   ```

5. **Use CI/CD to validate docs**
   ```yaml
   # .github/workflows/docs-check.yml
   - name: Validate documentation
     run: ./docs-check.sh
   ```

#### AI Assistant Responsibilities

**When you (AI assistant) make changes:**

1. **Update all affected documentation** in the SAME commit
   ```bash
   # Good commit:
   git commit -m "feat: add rate limiting

   - Implements rate limiting middleware
   - Updates CLAUDE.md with rate limiting setup
   - Updates README.md with rate limiting configuration
   - Updates TASK_LIST.md (mark Task #5 as complete)
   - Adds rate limiting tests"

   # Bad commit:
   git commit -m "feat: add rate limiting"
   # (Forgot to update docs!)
   ```

2. **Update the Changelog** in CLAUDE.md
   ```markdown
   ### 2025-11-15 (Rate Limiting Implementation)
   - Added rate limiting middleware to API
   - Updated Technology Stack with AspNetCoreRateLimit package
   - Updated Development Environment Setup with rate limit config
   ```

3. **Verify documentation accuracy** before marking todos complete
   ```bash
   # Before marking "Implement feature X" as complete:
   # ✅ Feature implemented
   # ✅ Tests passing
   # ✅ CLAUDE.md updated (if setup/testing affected)
   # ✅ README.md updated (if user-facing)
   # ✅ TASK_LIST.md updated (status changed)
   # ✅ Changelog updated in relevant docs
   ```

4. **Flag documentation debt** if you can't update everything
   ```bash
   # If you don't have time to update all docs:
   git commit -m "feat: add feature X

   Note: Documentation updates pending:
   - TODO: Update CLAUDE.md with new setup steps
   - TODO: Add examples to README.md
   - TODO: Update API documentation"

   # Then create a TASK_LIST.md entry to track it
   ```

#### Documentation Staleness Detection

**Red flags that indicate stale documentation:**

1. **Mismatch between code and docs**
   ```bash
   # Example: Docs say "65 tests" but actual count is 80
   grep "tests" CLAUDE.md | grep -oP "\d+"
   dotnet test | grep -oP "Passed:\s+\K\d+"
   ```

2. **References to removed files**
   ```bash
   # Check for broken file references
   grep -oP "src/[^\s]+" CLAUDE.md | while read file; do
       [ -f "$file" ] || echo "Missing: $file"
   done
   ```

3. **Old dates in "Last Updated"**
   ```bash
   # Docs older than 90 days should be reviewed
   ```

4. **Package versions don't match**
   ```bash
   # Technology Stack lists wrong versions
   dotnet list package | grep "OpenTelemetry"
   grep "OpenTelemetry" CLAUDE.md
   ```

5. **Documented commands fail**
   ```bash
   # Setup instructions don't work
   # Test commands produce different output
   ```

#### Summary: Documentation Maintenance Workflow

**For EVERY code commit:**

```mermaid
Code Change → Check API Changes? → Update XML Docs
           → Check Package Changes? → Update CLAUDE.md
           → Check Test Changes? → Update Test Counts
           → Check Structure Changes? → Update Project Structure
           → Update Changelog
           → Commit Code + Docs Together
```

**Monthly (start of session):**

```bash
1. Run ./docs-check.sh (validation script)
2. Review "Last Updated" dates
3. Update stale sections
4. Verify all examples still work
5. Update Changelog with review date
```

**Before major releases:**

```bash
1. Full documentation audit
2. Test all examples and commands
3. Update all version numbers
4. Verify all links work
5. Update README.md badges/stats
6. Update PROJECT_STATUS_REPORT.md
```

---

**Remember**: Documentation is code. Treat it with the same care:
- Version it
- Test it
- Review it
- Keep it DRY (Don't Repeat Yourself)
- Refactor it when needed
- Delete it when obsolete

## Quality Standards

### Code Quality
- **No compiler warnings** in Release builds
- **Follow SOLID principles**
- **Keep methods focused** (single responsibility)
- **Avoid code duplication** (DRY principle)
- **Use meaningful names** for variables and methods

### Performance Considerations
- **Use async/await** for I/O-bound operations
- **Avoid blocking calls** on async code
- **Consider memory allocations** in hot paths
- **Use appropriate collections** (List vs Array vs HashSet)
- **Profile before optimizing**

## Troubleshooting Common Issues

### Build Failures
1. Check .NET SDK version compatibility
2. Restore NuGet packages: `dotnet restore`
3. Clean and rebuild: `dotnet clean && dotnet build`
4. Check for missing dependencies

### Test Failures
1. Run tests individually to isolate issues
2. Check test output for detailed error messages
3. Verify test data and mocks are properly set up
4. Ensure async tests use proper async patterns

### Git Issues
1. Verify branch name follows `claude/*` convention
2. Check network connectivity for push failures
3. Use retry logic for transient failures
4. Never force push without explicit permission

### Docker-Related Issues

#### Docker Build Failures

**Error: "COPY failed: stat ... no such file or directory"**
```bash
# Problem: File referenced in Dockerfile doesn't exist or is excluded by .dockerignore
# Solution 1: Check if file exists
ls -la path/to/file

# Solution 2: Check .dockerignore
cat .dockerignore | grep filename

# Solution 3: Update .dockerignore if file was incorrectly excluded
# Remove the exclusion pattern from .dockerignore
```

**Error: "failed to solve with frontend dockerfile.v0"**
```bash
# Problem: Syntax error in Dockerfile
# Solution: Validate Dockerfile syntax
docker build --check .

# Check for common issues:
# - Missing quotes in COPY/ADD commands
# - Incorrect line continuations (\)
# - Invalid instruction order
```

**Error: "image not found" or "manifest unknown"**
```bash
# Problem: Base image not available or wrong name/tag
# Solution: Verify base image exists
docker pull mcr.microsoft.com/dotnet/sdk:8.0

# If pull fails, check image name and tag at:
# https://mcr.microsoft.com/
```

#### Docker Compose Issues

**Error: "port is already allocated"**
```bash
# Problem: Port already in use by another container or process
# Solution 1: Find what's using the port
lsof -i :5000          # Linux/macOS
netstat -ano | find "5000"  # Windows

# Solution 2: Change port in docker-compose.yml
# Edit docker-compose.yml:
services:
  orchestrator-api:
    ports:
      - "5001:5000"  # Changed from 5000:5000

# Solution 3: Stop conflicting container
docker ps | grep 5000
docker stop <container_id>
```

**Error: "service 'X' failed to build"**
```bash
# Problem: Build context or Dockerfile issue
# Solution: Build service individually to see full error
docker-compose build orchestrator-api --no-cache

# Check logs for specific error
docker-compose logs orchestrator-api
```

**Error: "network not found"**
```bash
# Problem: Network referenced in docker-compose.yml doesn't exist
# Solution 1: Create network if it should exist
docker network create my-network

# Solution 2: Remove network reference if not needed
# Edit docker-compose.yml to remove networks: section

# Solution 3: Clean up and recreate
docker-compose down
docker-compose up -d
```

#### Container Runtime Issues

**Container exits immediately after starting**
```bash
# Problem: Application crashes or exits on startup
# Solution: Check logs for error
docker logs <container_id>
docker-compose logs orchestrator-api

# Common causes:
# - Missing environment variables
# - Configuration file not found
# - Port binding issue
# - Insufficient permissions
```

**Container is slow or unresponsive**
```bash
# Problem: Resource constraints or performance issue
# Solution: Check resource usage
docker stats

# If CPU/Memory at 100%:
# 1. Increase limits in docker-compose.yml
# 2. Optimize application code
# 3. Scale horizontally with multiple containers
```

**Health check failing**
```bash
# Problem: Container health check endpoint not responding
# Solution: Debug health check
docker inspect <container_id> | grep -A 10 Health

# Test health check manually
docker exec <container_id> curl http://localhost:5000/health

# Common issues:
# - Wrong port in health check
# - Application not fully started
# - Health endpoint not implemented
```

#### Docker Permissions Issues

**Error: "permission denied while trying to connect to Docker daemon"**
```bash
# Problem: User not in docker group (Linux)
# Solution: Add user to docker group
sudo usermod -aG docker $USER

# Log out and log back in for changes to take effect
# Or use: newgrp docker

# Verify:
docker ps  # Should work without sudo
```

**Error: "permission denied" when copying files in Dockerfile**
```bash
# Problem: File permissions issue in build context
# Solution: Fix file permissions before build
chmod +r file-to-copy

# Or run container as root temporarily:
USER root
COPY file /destination
RUN chown appuser:appuser /destination/file
USER appuser
```

#### Docker Networking Issues

**Containers can't communicate with each other**
```bash
# Problem: Containers on different networks or wrong network configuration
# Solution 1: Check networks
docker network ls
docker network inspect <network_name>

# Solution 2: Ensure containers are on same network
# In docker-compose.yml:
services:
  api:
    networks:
      - backend
  redis:
    networks:
      - backend  # Same network

# Solution 3: Use service names for DNS
# Replace: http://localhost:6379
# With: http://redis:6379  # Use service name
```

**Can't access container from host**
```bash
# Problem: Port not exposed or wrong port mapping
# Solution: Verify port mapping
docker ps  # Check PORTS column

# Update docker-compose.yml if needed:
ports:
  - "5000:5000"  # host:container

# Or expose port in Dockerfile:
EXPOSE 5000
```

#### Docker Volume Issues

**Error: "volume mount failed"**
```bash
# Problem: Volume path doesn't exist or permission issue
# Solution: Create volume directory
mkdir -p /path/to/volume

# Fix permissions:
chmod 755 /path/to/volume

# Or use named volumes instead of bind mounts:
volumes:
  data:  # Named volume (Docker manages)

services:
  api:
    volumes:
      - data:/app/data
```

**Data not persisting after container restart**
```bash
# Problem: Volume not configured for data persistence
# Solution: Add volume to docker-compose.yml
services:
  api:
    volumes:
      - ./data:/app/data  # Bind mount
      # Or
      - api-data:/app/data  # Named volume

volumes:
  api-data:  # Define named volume
```

#### Docker Image Issues

**Image size too large (>1GB)**
```bash
# Problem: Inefficient Dockerfile or unnecessary files included
# Solution 1: Use multi-stage builds
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
# ... build steps ...
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
COPY --from=build /app/publish .

# Solution 2: Update .dockerignore
# Add: bin/, obj/, .git/, *.md, tests/

# Solution 3: Use Alpine images
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine

# Verify size improvement:
docker images | grep hotswap
```

**Old image layers not being removed**
```bash
# Problem: Dangling images accumulating
# Solution: Prune old images
docker image prune -a

# Remove specific old images:
docker rmi <image_id>

# Clean up everything (careful!):
docker system prune -a --volumes
```

#### Docker Security Issues

**Security scan finds vulnerabilities**
```bash
# Problem: Base image or dependencies have known vulnerabilities
# Solution 1: Update base image
docker pull mcr.microsoft.com/dotnet/aspnet:8.0
# Check if newer tag available

# Solution 2: Run security scan
trivy image hotswap-orchestrator:latest --severity HIGH,CRITICAL

# Solution 3: Update vulnerable packages
# In .csproj: Update package versions
# Rebuild image: docker build -t hotswap-orchestrator:latest .
```

#### Quick Docker Troubleshooting Checklist

When Docker issues occur, run through this checklist:

```bash
# 1. Check Docker daemon is running
docker ps
# If error: Start Docker Desktop (Windows/Mac) or sudo systemctl start docker (Linux)

# 2. Check disk space
df -h
# Docker needs sufficient disk space for images/containers

# 3. Check logs
docker-compose logs
docker logs <container_id>

# 4. Check container status
docker-compose ps
docker ps -a

# 5. Restart containers
docker-compose restart

# 6. Full cleanup and rebuild (if other steps fail)
docker-compose down -v
docker-compose build --no-cache
docker-compose up -d

# 7. Check Docker version
docker --version
docker-compose --version
# Ensure versions are recent and compatible
```

## Resources

### .NET Documentation
- [.NET Documentation](https://docs.microsoft.com/en-us/dotnet/)
- [C# Programming Guide](https://docs.microsoft.com/en-us/dotnet/csharp/)
- [ASP.NET Core Documentation](https://docs.microsoft.com/en-us/aspnet/core/)

### Best Practices
- [.NET Design Guidelines](https://docs.microsoft.com/en-us/dotnet/standard/design-guidelines/)
- [Secure Coding Guidelines](https://docs.microsoft.com/en-us/dotnet/standard/security/)
- [Unit Testing Best Practices](https://docs.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)

## Changelog

### 2025-11-20 (Git Push Procedure Enhancement)
- **Enhanced Git Push Requirements section** (~150 lines)
  - Added mandatory pre-push procedure (fetch, pull, resolve conflicts, test, push)
  - Comprehensive push requirements checklist with do's and don'ts
  - Detailed merge conflict resolution guide (7-step process)
  - Common conflict scenarios with solutions (3 scenarios)
  - Network failure retry logic with exponential backoff
  - Emergency procedures for permission errors (HTTP 403)
  - Best practices for pulling, committing, and merging
- **Updated Typical Development Session workflow**
  - Added Step 10: Pull latest changes before push (CRITICAL)
  - Included merge conflict resolution steps
  - Added retry logic guidance for network failures
  - Step-by-step instructions for handling conflicts
- **Impact**:
  - Prevents rejected pushes due to remote changes
  - Reduces merge conflicts through proactive pulling
  - Ensures code is always tested after merges
  - Provides clear guidance for resolving conflicts
  - Establishes retry logic for transient network failures
  - Aligns with Git best practices and team collaboration workflows
- **Total additions**: ~165 lines of git push and merge conflict guidance

### 2025-11-20 (Sprint 2 Skills - 5 New Skills Added)
- **Created 5 critical Claude Skills** (~39K lines total)
  1. **integration-test-debugger.md** (13K) - Systematic debugging of hanging, timeout, and failing integration tests
  2. **database-migration-helper.md** (11K) - Entity Framework Core migrations for PostgreSQL with rollback procedures
  3. **security-hardening.md** (6K) - Secret rotation, OWASP Top 10 compliance, production security checklist
  4. **api-endpoint-builder.md** (4K) - REST API controller scaffolding with CRUD operations
  5. **performance-optimizer.md** (5K) - Load testing, profiling, and optimization patterns with k6 and dotnet-trace
- **Updated SKILLS.md** (+300 lines)
  - Added new category: Security & Compliance Skills
  - Updated skills table: 13 → 18 skills
  - Updated total size: ~10,100 → ~12,000+ lines
  - Comprehensive documentation for all 5 new skills
  - Updated statistics and changelog
- **Updated CLAUDE.md** (this file)
  - Updated project structure to list all 18 skills
  - Updated skill count: 8 → 18 skills
  - Updated total size: ~3,900 → ~12,000+ lines
- **Impact**:
  - Unblocks Task #23 (ApprovalWorkflow hanging tests), Task #24 (slow deployment tests), Task #22 (multi-tenant 404s)
  - Completes Task #3 (PostgreSQL audit log persistence to 100%)
  - Addresses Task #16 (Secret Rotation) and Task #17 (OWASP Review)
  - Enables Task #22 (Multi-tenant API endpoints)
  - Facilitates Task #8 (Load Testing and Performance Benchmarks)
  - Covers all critical Sprint 2 gaps identified in skills analysis
- **Total Skill Count:** 18 skills, ~12,000+ lines of comprehensive guidance

### 2025-11-19 (Sprint Planner Skill Addition)
- **Created sprint-planner skill** (~23K, 900+ lines)
  - Comprehensive 7-phase sprint planning process
  - Task discovery, effort estimation, dependency mapping
  - Workload balancing across N team members
  - Sprint goals with SMART criteria
  - Risk analysis and mitigation strategies
  - Real-world example: TASK_DELEGATION_ANALYSIS.md (17 tasks → 3 balanced workstreams)
- **Updated SKILLS.md** (+130 lines)
  - Added Project Management Skills section
  - Updated skills table: 12 → 13 skills (including 5 project discipline skills)
  - Updated total size: ~2,800 → ~3,900 lines
  - Added sprint-planner to decision tree
  - Comprehensive documentation with usage examples
- **Updated README.md**
  - Added sprint-planner to skills table
  - Updated skill count and total lines
- **Updated CLAUDE.md**
  - Updated project structure with sprint-planner.md
  - Updated skill count references (3 locations)
  - Updated statistics throughout
- **Impact**:
  - Enables systematic sprint planning with task delegation
  - Prevents over-commitment through capacity planning
  - Identifies bottlenecks via critical path analysis
  - Provides 45-90 minute ROI (saves days/weeks of poor planning)
  - Supports multi-team coordination and workload balancing
- **Total Skill Count:** 13 skills at this point (5 project discipline + 1 project mgmt + 7 development skills)

### 2025-11-19 (Claude Skills Documentation and Integration)
- **Created SKILLS.md** (~1,100 lines)
  - Comprehensive documentation for all 13 Claude Skills (including 5 project discipline skills added same day)
  - Quick reference table by category
  - Decision tree for skill selection
  - Detailed descriptions for each skill (sprint-planner, dotnet-setup, tdd-helper, precommit-check, test-coverage-analyzer, race-condition-debugger, doc-sync-check, docker-helper)
  - Typical workflows (daily development, feature completion, bug fix, Docker update, monthly maintenance, sprint planning)
  - How to use skills (slash commands, Claude Code tool, manual execution)
  - Creating new skills (template and best practices)
  - Statistics: 8 skills, ~3,900 lines total
- **Updated CLAUDE.md to reference skills** (~10 locations)
  - Added .claude/skills/ directory to Project Structure
  - Added SKILLS.md to file list
  - Added "Use Claude Skills" row to Quick Reference table
  - Added SKILLS.md to Table of Contents (Getting Started section)
  - Renumbered all subsequent ToC sections (7-29 instead of 6-28)
  - Added skill tips to 6 key sections:
    - Development Environment Setup: `/dotnet-setup` skill
    - TDD Workflow: `/tdd-helper` skill
    - Pre-Commit Checklist: `/precommit-check` skill
    - Testing Requirements: `/test-coverage-analyzer` skill
    - Avoiding Stale Documentation: `/doc-sync-check` skill
    - Docker Development and Maintenance: `/docker-helper` skill
- **Impact**:
  - Skills are now fully documented and discoverable
  - Clear guidance on which skill to use for each task
  - Integrated into existing workflows throughout CLAUDE.md
  - Provides ~3,900 lines of automated workflow guidance
  - Reduces cognitive load by automating complex tasks

### 2025-11-17 (Docker Documentation and Maintenance Guidelines)
- **Added comprehensive Docker Development and Maintenance section** (~335 lines)
  - When to update Docker configuration (Dockerfile and docker-compose.yml triggers)
  - Docker maintenance best practices (6 key practices)
    1. Base image management (version pinning, digest pinning)
    2. Multi-stage build optimization
    3. Layer caching strategies
    4. Security scanning (Docker Scout, Trivy, Snyk)
    5. Image size optimization (Alpine images, .dockerignore)
    6. .dockerignore maintenance
  - Testing Docker builds (6-step validation process)
  - Testing docker-compose stack (7-step validation process)
  - Docker security best practices (non-root user, read-only filesystems, resource limits, network isolation)
  - Monthly Docker maintenance tasks checklist
  - Integration with Pre-Commit Checklist
  - Docker documentation update checklist
  - Docker version pinning strategy
- **Added Docker trigger to Mandatory Documentation Update Triggers** (trigger #8)
  - When to update Docker documentation
  - What files to update (CLAUDE.md, README.md, docker-compose.yml)
  - Test requirements before committing Docker changes
- **Added Step 7 to Pre-Commit Checklist: Verify Docker Changes** (~75 lines)
  - 10-step Docker validation process
  - What to check before committing Docker changes
  - Common Docker validation errors and fixes
  - When to skip Docker validation
- **Added comprehensive Docker troubleshooting section** (~314 lines)
  - Docker Build Failures (3 common errors)
  - Docker Compose Issues (3 common errors)
  - Container Runtime Issues (3 common scenarios)
  - Docker Permissions Issues (2 common errors)
  - Docker Networking Issues (2 common problems)
  - Docker Volume Issues (2 common errors)
  - Docker Image Issues (2 optimization topics)
  - Docker Security Issues (vulnerability management)
  - Quick Docker Troubleshooting Checklist (7-step process)
- **Updated Table of Contents**
  - Added item 12: Docker Development and Maintenance (Daily Development Workflows)
  - Renumbered subsequent sections (13-28)
  - Total sections increased from 27 to 28
- **Documentation improvements**
  - Total additions: ~724 lines of Docker-specific guidance
  - Comprehensive coverage of Docker development workflow
  - Integration with existing pre-commit and documentation update processes
  - Practical troubleshooting for common Docker issues
- **Impact**:
  - Developers now have clear guidance on maintaining Docker files
  - Reduces Docker-related build failures and configuration drift
  - Establishes security scanning and update schedule
  - Comprehensive troubleshooting reduces debugging time
  - Documentation stays synchronized with Docker changes
- Based on: User request to document Docker maintenance requirements

### 2025-11-17 (Documentation Test Count Update - Messaging & Multi-Tenant Features)
- **Updated test count references across all documentation**
  - Build Status: 80 tests → 582 tests (568 passing, 14 skipped)
  - Project Metrics table updated in Quick Reference section
  - First Time Build and Test: Expected output updated to 582 tests
  - Run All Tests: Expected output updated to 582 tests
  - Critical Path Tests: Expected output updated to 582 tests
  - AUTONOMOUS_AGENT_ONBOARDING.md: All test references updated (5 locations)
- **Reflects massive feature expansion from recent merges**
  - Multi-tenant system implementation (~100+ new files)
  - Message broker with topics, schemas, routing strategies
  - Schema management with approval workflow and compatibility checking
  - Comprehensive test coverage expansion: 80 → 582 tests (628% increase)
- **Fixed AUTONOMOUS_AGENT_ONBOARDING.md line references**
  - Changed CLAUDE.md line number references to section names
  - Prevents documentation staleness as file evolves
  - Improves maintainability and accuracy
- **Verified build and test status**
  - Build: Clean with 1 acceptable System.Text.Json warning
  - Tests: 568 passing, 14 skipped (need investigation), 0 failed
  - Duration: ~18 seconds for full test suite
- **Impact**: Documentation now accurately reflects current project state

### 2025-11-16 (Generalized Documentation - CLAUDE.md.PROPOSAL Full Implementation)
- **Generalized contract verification guidance** (Phase 2.2)
  - Condensed model property section from ~55 to ~37 lines
  - Removed overly specific ErrorResponse examples
  - Made applicable to ALL contracts (classes, interfaces, enums, methods)
  - Focus on principle: "Read definitions, don't guess names"
  - Benefits: Applies universally, easier to maintain, teaches core principle
- **Condensed No .NET SDK checklist** (Phase 2.3)
  - Removed redundant Step 9 (duplicate of generalized Step 1)
  - Streamlined Step 10 (Pre-Commit Validation Summary)
  - Updated "What NOT to Commit" to use generalized terms
  - Reduced redundancy while maintaining all critical information
- **Added comprehensive Table of Contents** (Phase 3)
  - Organized into 5 logical sections: Getting Started, Daily Workflows, Standards, AI Guidelines, Reference
  - Priority indicators (⭐⭐⭐ Critical, ⭐⭐ Important, ⭐ Helpful)
  - 27 major sections with quick navigation links
  - Helps new users identify what to read first
  - Improves discoverability of advanced topics
- **Documentation improvements**
  - Reduced redundancy: 80 additions, 81 deletions (net -1 line with improved clarity)
  - Single source of truth for contract verification principles
  - Better signal-to-noise ratio
  - Maintained all critical safety checks
- **Verified quality**
  - Build: 0 warnings, 0 errors ✓
  - Tests: 80 passing, 0 failed, 0 skipped ✓
  - All links and references verified
- **Impact**:
  - More maintainable: Less duplication, single principles vs specific examples
  - More usable: Table of Contents, priority indicators, better navigation
  - More scalable: Generic principles apply to future code, not just current examples
  - Completes CLAUDE.md.PROPOSAL Phases 1-3 (Critical Fixes, Structural Improvements, Navigation)
- Based on: Full implementation of CLAUDE.md.PROPOSAL (all phases except Phase 4 automation)

### 2025-11-16 (Quick Reference and Build Warning Fix)
- **Added Quick Reference section** (48 lines)
  - New section after Technology Stack for fast navigation
  - Most Common Commands table with 7 essential tasks
  - Project Metrics table with verified current values
  - AI Assistant Critical Rules (ALWAYS/NEVER checklist)
  - Provides immediate productivity for new users and AI assistants
- **Fixed build warning count inconsistency** (Lines 336-340)
  - Corrected expected output: 1 Warning → 0 Warnings
  - Updated build time: 13.99s → 18.04s (actual verified time)
  - Removed obsolete CS1998 warning reference (no longer exists)
  - Documentation now matches actual clean build state
- **Updated documentation staleness line number references**
  - Added Quick Reference metrics table (line 115) to update checklist
  - Corrected First Time Build reference: line 309 → 388
  - Corrected Run All Tests reference: line 351 → 435
  - Corrected Critical Path Tests reference: line 389 → 473
  - Accounts for 42-line Quick Reference addition
- **Verified actual state**
  - Build: 0 warnings, 0 errors (clean build confirmed)
  - Tests: 80 passing, 0 failed, 0 skipped
  - Build time: ~18 seconds (verified with dotnet build --no-incremental)
  - Test time: ~10 seconds (verified with dotnet test)
- **Impact**:
  - Resolves critical inconsistency from CLAUDE.md.PROPOSAL
  - Improves discoverability and usability for new users
  - Single source of truth for project metrics
  - Documentation fully aligned with actual project state
- Total additions: ~48 lines (Quick Reference section)
- Based on: CLAUDE.md.PROPOSAL generalized improvements

### 2025-11-16 (Claude Code Web Environment Support and Test Count Fixes)
- **Added Claude Code Web Environment installation instructions**
  - New section: "Claude Code Web Environment (Ubuntu 24.04)" in Prerequisites
  - Step-by-step installation guide for Ubuntu 24.04 LTS with root access
  - Verified installation process in actual web environment
  - Installation time: ~30-60 seconds, disk space: ~500 MB
  - Installed .NET SDK 8.0.121 with ASP.NET Core 8.0.21
- **Fixed test count inconsistencies across documentation**
  - Updated "First Time Build and Test" expected output: 23 → 80 tests
  - Updated "Run All Tests" expected output: 38 → 80 tests
  - Updated "Critical Path Tests" expected output: 38 → 80 tests
  - Verified actual test count: 80 passing tests (0 failed, 0 skipped)
- **Verified build status**
  - Build succeeds with 0 warnings and 0 errors (improved from documented 1 warning)
  - All 80 tests passing in ~10 seconds
  - Full clean, restore, build, test cycle confirmed working in web environment
- **Impact**:
  - Claude Code web environment now fully documented and tested
  - Consistent test count references prevent confusion
  - Documentation matches actual project state (80 tests, clean build)
  - Web-based development workflow now supported without local .NET SDK
- Total additions: ~40 lines of web environment installation documentation

### 2025-11-15 (Avoiding Stale Documentation)
- **Added comprehensive "Avoiding Stale Documentation" section** (~500 lines)
  - Mandatory documentation update triggers (7 key scenarios)
  - Documentation synchronization checklist for every commit
  - Version tracking requirements (dates, changelog, version-specific info)
  - Monthly documentation audit process
  - Automated documentation validation script (docs-check.sh)
  - Documentation-in-code proximity guidelines
  - Deprecation and outdated content handling
  - Documentation testing procedures
  - Common stale documentation patterns to avoid (5 don'ts, 5 dos)
  - AI assistant responsibilities for documentation maintenance
  - Documentation staleness detection (5 red flags)
  - Summary workflow: per-commit, monthly, and pre-release processes
  - Emphasizes: "Documentation is code" - version, test, review, refactor
- **Impact**: Prevents documentation from becoming outdated, misleading, or inaccurate
- **Benefits**:
  - Ensures docs stay synchronized with code changes
  - Reduces onboarding time for new developers
  - Prevents bugs caused by following outdated documentation
  - Establishes clear ownership and review processes
- Total additions: ~500 lines of documentation maintenance best practices

### 2025-11-16 (Deployment Listing and Test Coverage)
- **Fixed ListDeployments endpoint** returning empty list
  - Extended IDeploymentTracker interface with GetAllResultsAsync() and GetAllInProgressAsync()
  - Added ConcurrentDictionary ID tracking in InMemoryDeploymentTracker
  - Implemented full deployment listing with completed and in-progress aggregation
  - Automatic cleanup of stale IDs when cache entries expire
  - Sorted results by start time descending (most recent first)
- **Added comprehensive unit tests for InMemoryDeploymentTracker** (15 new tests)
  - Tests for GetResultAsync, StoreResultAsync, GetInProgressAsync, TrackInProgressAsync
  - Tests for RemoveInProgressAsync, GetAllResultsAsync, GetAllInProgressAsync
  - Cache expiration and cleanup behavior tests
  - Full deployment workflow integration test
  - Constructor validation tests
  - Maintains 85%+ test coverage requirement
- **Updated test count documentation**
  - Build Status: 80/80 tests passing (previously 65)
  - Updated documentation staleness example
  - Last Updated date refreshed
- **Impact**:
  - Fixes smoke test failures for deployment listing
  - Enables deployment history viewing via API
  - Maintains horizontal scaling capability
  - Comprehensive test coverage for deployment tracking

### 2025-11-15 (TDD and .NET SDK Installation Requirements)
- **Added mandatory .NET SDK installation verification** to Initial Analysis Checklist
  - Step 0: Verify .NET SDK Installation (CRITICAL - DO THIS FIRST)
  - Instructions for all platforms (Windows, Linux, macOS)
  - Guidance for Claude Code web environment without .NET SDK
  - Clear directive: Never proceed without verifying SDK availability
- **Added comprehensive Test-Driven Development (TDD) Workflow section** (~300 lines)
  - Why TDD is mandatory (prevents regressions, improves design, documents behavior)
  - Red-Green-Refactor cycle explanation and workflow
  - TDD workflows for: New Features, Bug Fixes, Refactoring
  - Best practices: Test naming, AAA pattern, mock setup, FluentAssertions
  - Complete example: Rate limiting middleware with TDD
  - Integration with TodoWrite tool for tracking TDD steps
  - TDD Checklist for task completion verification
  - Explicit statement: "NEVER skip TDD" - mandatory for all code changes
- **Enhanced Testing Requirements section**
  - Emphasizes TDD is MANDATORY, not optional
  - Tests BEFORE implementation (Red-Green-Refactor)
  - Test coverage requirements: happy path, edge cases, error cases
  - Testing patterns: xUnit, Moq, FluentAssertions, AAA pattern
  - Example workflows comparing wrong (implementation first) vs correct (TDD)
  - Cross-references to TDD Workflow section
- Total additions: ~350 lines of TDD and .NET SDK installation guidance

### 2025-11-15 (Installation and Build Instructions)
- **Added comprehensive .NET SDK installation troubleshooting** for Linux environments
  - Step-by-step installation process for root and non-root users
  - Fixed /tmp permissions issue resolution
  - Common installation errors and solutions
  - Handling sudo.conf ownership errors
  - PPA repository 403 Forbidden error handling
- **Added "First Time Build and Test" section** with exact workflow
  - Complete 4-step process: clean, restore, build, test
  - Expected output for each command
  - Documented known warnings (CS1998 in DeploymentsController.cs)
  - Build and test timing expectations
  - Current test count documentation (23 passing tests)
- **Verified installation process** on Ubuntu 24.04 LTS
  - .NET SDK 8.0.416 installation confirmed
  - All 23 tests passing
  - Build succeeds with expected 1 warning
- Total additions: ~60 lines of installation and first-run documentation

### 2025-11-15 (TASK_LIST.md Integration)
- **Added comprehensive "Working with TASK_LIST.md" section** to AI Assistant Guidelines
  - Overview and purpose of TASK_LIST.md
  - When to consult and how to use the task list
  - Task status indicators and priority levels
  - Best practices for maintenance and updates
  - Example workflows and task entry formats
  - Integration with other project documents
  - Common mistakes to avoid
- **Updated Project Structure** to include TASK_LIST.md and ENHANCEMENTS.md
- Total additions: ~160 lines of task management guidance

### 2025-11-14 (Update)
- **Updated repository state** to reflect production-ready status
- **Enhanced Technology Stack section** with actual versions (all packages)
- **Significantly expanded Development Environment Setup** with:
  - Comprehensive prerequisites installation (Windows/Linux/macOS)
  - Detailed dependency installation instructions
  - Step-by-step building instructions for all configurations
  - Complete testing guide including coverage and validation scripts
  - Application running instructions (local, Docker, examples)
  - Full development workflow documentation
  - Extensive troubleshooting section for common setup issues
- **Updated project structure** with actual current state
- **Added Examples section** documenting ApiUsageExample project
- **Added Docker deployment instructions**
- **Added API running instructions** with all available endpoints
- Total additions: ~400 lines of comprehensive setup documentation

### 2025-11-14 (Initial)
- Initial CLAUDE.md creation
- Documented repository structure and conventions
- Added .NET development guidelines
- Established AI assistant workflows

---

**Note**: This document should be updated as the project evolves and new conventions are established.
