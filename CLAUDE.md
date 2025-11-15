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
**Build Status**: ✅ Passing (38/38 tests)
**Test Coverage**: 85%+
**Last Updated**: November 14, 2025

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
├── CLAUDE.md                                 # This file (AI assistant guide)
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

## Development Environment Setup

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
#   Passed!  - Failed:     0, Passed:    38, Skipped:     0, Total:    38

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
#   ✓ All 38 critical path tests passed
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

### Development Workflow

#### Typical Development Session

```bash
# 1. Pull latest changes
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

# 10. Push to remote
git push -u origin claude/your-feature-name-sessionid
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

### Git Push Requirements
- **ALWAYS** use `git push -u origin <branch-name>`
- Branch MUST start with `claude/` and end with session ID
- Retry up to 4 times with exponential backoff (2s, 4s, 8s, 16s) on network errors
- Never force push to main/master

## AI Assistant Guidelines

### Initial Analysis Checklist
When starting a new task:
1. **Read relevant files** before making changes
2. **Check for existing patterns** in the codebase
3. **Verify .NET SDK version** compatibility
4. **Review dependencies** and their versions
5. **Check for existing tests** to understand expected behavior

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
- **Write tests for new features** before marking task complete
- **Ensure tests pass** before committing
- **Include edge cases** in test coverage
- **Mock external dependencies** in unit tests
- **Use integration tests** for critical workflows

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
- **Mark completed immediately** after finishing
- **Only one in_progress** task at a time
- **Break down complex tasks** into smaller steps

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
