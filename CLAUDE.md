# CLAUDE.md - AI Assistant Guide for Claude-code-test

This document provides comprehensive guidance for AI assistants working with this repository.

## Repository Overview

**Name**: Claude-code-test
**Owner**: scrawlsbenches
**License**: MIT License (2025)
**Primary Language**: .NET/C#
**Purpose**: Testing repository for Claude Code functionality

## Current Repository State

**Status**: Initial setup phase
**Branch**: `claude/claude-md-mhzd42xtak2vwsbk-01J3MA8R7NxyMQHYDz4MU1vt`
**Last Commit**: db34c2d - Initial commit

### Existing Files
- `README.md` - Basic project description
- `LICENSE` - MIT License
- `.gitignore` - .NET-specific gitignore configuration

### Project Structure (To Be Established)
```
Claude-code-test/
├── src/                    # Source code
│   └── [Project folders]   # Individual .NET projects
├── tests/                  # Test projects
│   └── [Test projects]     # Unit and integration tests
├── docs/                   # Documentation
├── .gitignore             # .NET gitignore
├── LICENSE                # MIT License
├── README.md              # Project overview
├── CLAUDE.md              # This file
└── [Solution file]        # .NET solution file (*.sln)
```

## Technology Stack

### Primary Framework
- **.NET** - Modern .NET development (likely .NET 6+ or .NET 8+)

### Expected Technologies (based on .NET projects)
- **C#** - Primary programming language
- **MSBuild** - Build system
- **NuGet** - Package management
- **xUnit/NUnit/MSTest** - Testing frameworks

## Development Environment Setup

### Prerequisites
```bash
# Verify .NET SDK installation
dotnet --version

# List installed SDKs
dotnet --list-sdks

# List installed runtimes
dotnet --list-runtimes
```

### Building the Project
```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Build in Release mode
dotnet build -c Release
```

### Running Tests
```bash
# Run all tests
dotnet test

# Run tests with detailed output
dotnet test --verbosity normal

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"
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

### 2025-11-14
- Initial CLAUDE.md creation
- Documented repository structure and conventions
- Added .NET development guidelines
- Established AI assistant workflows

---

**Note**: This document should be updated as the project evolves and new conventions are established.
