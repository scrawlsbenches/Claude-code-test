# Docker Configuration Helper Skill

**Description**: Assists with creating, updating, and validating Docker configuration files (Dockerfile and docker-compose.yml) following security and optimization best practices.

**When to use**:
- Updating base images or .NET SDK version
- Adding new services to docker-compose.yml
- Optimizing Docker build performance
- Conducting security reviews
- Monthly Docker maintenance tasks

## Instructions

This skill implements the comprehensive Docker maintenance guidelines from CLAUDE.md to ensure containerized deployments remain secure, optimized, and up-to-date.

---

## Phase 1: Initial Assessment

### Step 1.1: Check Current Configuration

```bash
echo "=== CURRENT DOCKER CONFIGURATION ==="
echo ""

# Check Dockerfile
if [ -f "Dockerfile" ]; then
    echo "📄 Dockerfile found"
    echo "Base images:"
    grep "^FROM" Dockerfile
else
    echo "❌ Dockerfile not found"
fi

echo ""

# Check docker-compose.yml
if [ -f "docker-compose.yml" ]; then
    echo "📄 docker-compose.yml found"
    echo "Services:"
    grep -E "^\s+[a-z-]+:" docker-compose.yml | grep -v "^#"
else
    echo "❌ docker-compose.yml not found"
fi

echo ""

# Check .dockerignore
if [ -f ".dockerignore" ]; then
    echo "✅ .dockerignore exists"
else
    echo "⚠️  .dockerignore missing - build context may include unnecessary files"
fi
```

### Step 1.2: Identify Update Triggers

```bash
echo ""
echo "=== UPDATE TRIGGERS CHECK ==="
echo ""

# Check for outdated base images
echo "Checking base image versions..."
CURRENT_SDK=$(grep "FROM.*dotnet/sdk" Dockerfile | grep -oP "sdk:\K[0-9.]+")
CURRENT_ASPNET=$(grep "FROM.*dotnet/aspnet" Dockerfile | grep -oP "aspnet:\K[0-9.]+")

echo "Current SDK version: $CURRENT_SDK"
echo "Current ASP.NET version: $CURRENT_ASPNET"
echo "Latest .NET version: 8.0.121 (verify at https://hub.docker.com/_/microsoft-dotnet-sdk)"

# Check when Dockerfile was last modified
LAST_MODIFIED=$(stat -c %y Dockerfile 2>/dev/null | cut -d' ' -f1)
DAYS_OLD=$(( ($(date +%s) - $(date -d "$LAST_MODIFIED" +%s)) / 86400 ))
echo ""
echo "Dockerfile last modified: $LAST_MODIFIED ($DAYS_OLD days ago)"

if [ $DAYS_OLD -gt 30 ]; then
    echo "⚠️  Dockerfile is >30 days old - consider reviewing for updates"
fi
```

---

## Phase 2: Dockerfile Optimization

### Step 2.1: Validate Multi-Stage Build

```bash
echo ""
echo "=== MULTI-STAGE BUILD VALIDATION ==="
echo ""

# Check if Dockerfile uses multi-stage builds
BUILD_STAGES=$(grep -c "^FROM" Dockerfile)
echo "Number of build stages: $BUILD_STAGES"

if [ $BUILD_STAGES -ge 2 ]; then
    echo "✅ Multi-stage build detected"

    # Show stages
    echo "Stages:"
    grep "^FROM" Dockerfile | grep -oP "AS \K\w+" || echo "  (unnamed stages)"
else
    echo "⚠️  Single-stage build - consider multi-stage for smaller images"
fi

# Check for proper stage separation
echo ""
echo "Recommended structure:"
echo "  1. Build stage (FROM dotnet/sdk) - compile application"
echo "  2. Runtime stage (FROM dotnet/aspnet) - run application"
```

### Step 2.2: Check Layer Caching Optimization

```bash
echo ""
echo "=== LAYER CACHING OPTIMIZATION ==="
echo ""

# Check file copy order
echo "Checking COPY instruction order..."
grep "COPY" Dockerfile | nl

echo ""
echo "✅ GOOD order:"
echo "  1. COPY *.csproj (changes rarely)"
echo "  2. RUN dotnet restore"
echo "  3. COPY . . (changes frequently)"
echo ""
echo "❌ BAD order:"
echo "  1. COPY . . (invalidates cache on every change)"
echo "  2. RUN dotnet restore"
```

### Step 2.3: Security Best Practices Check

```bash
echo ""
echo "=== SECURITY CHECKS ==="
echo ""

# Check for non-root user
if grep -q "USER" Dockerfile; then
    echo "✅ Non-root user configured"
    grep "USER" Dockerfile
else
    echo "⚠️  Running as root - consider adding non-root user"
    echo ""
    echo "Add to Dockerfile:"
    echo "  RUN addgroup -g 1000 appuser && \\"
    echo "      adduser -D -u 1000 -G appuser appuser"
    echo "  USER appuser"
fi

echo ""

# Check for Alpine base images (smaller attack surface)
if grep -q "alpine" Dockerfile; then
    echo "✅ Using Alpine-based images (smaller footprint)"
else
    echo "💡 Consider Alpine images for smaller size: mcr.microsoft.com/dotnet/aspnet:8.0-alpine"
fi

echo ""

# Check for secrets in Dockerfile
if grep -iE "password|secret|api[_-]?key|token" Dockerfile; then
    echo "⚠️  WARNING: Potential secrets found in Dockerfile!"
else
    echo "✅ No obvious secrets in Dockerfile"
fi
```

---

## Phase 3: docker-compose.yml Validation

### Step 3.1: Service Configuration Check

```bash
echo ""
echo "=== DOCKER-COMPOSE VALIDATION ==="
echo ""

# Validate syntax
echo "Validating docker-compose.yml syntax..."
docker-compose config > /dev/null 2>&1
if [ $? -eq 0 ]; then
    echo "✅ docker-compose.yml syntax valid"
else
    echo "❌ docker-compose.yml syntax invalid"
    docker-compose config
fi

echo ""

# List services
echo "Configured services:"
docker-compose config --services

echo ""

# Check port mappings
echo "Port mappings:"
docker-compose config | grep -A 2 "ports:"
```

### Step 3.2: Resource Limits Check

```bash
echo ""
echo "=== RESOURCE LIMITS CHECK ==="
echo ""

if grep -q "resources:" docker-compose.yml; then
    echo "✅ Resource limits configured"
    grep -A 6 "resources:" docker-compose.yml
else
    echo "⚠️  No resource limits configured"
    echo ""
    echo "Recommended configuration:"
    cat <<'EOF'
    deploy:
      resources:
        limits:
          cpus: '2.0'
          memory: 1G
        reservations:
          cpus: '0.5'
          memory: 512M
EOF
fi
```

### Step 3.3: Network Configuration Check

```bash
echo ""
echo "=== NETWORK CONFIGURATION ==="
echo ""

if grep -q "networks:" docker-compose.yml; then
    echo "✅ Networks configured"
    grep -A 5 "networks:" docker-compose.yml | head -20
else
    echo "💡 No custom networks - services use default network"
fi

echo ""
echo "Best practice: Isolate services with separate networks"
echo "  - Frontend network: Public-facing services"
echo "  - Backend network: Internal services (databases, caches)"
```

---

## Phase 4: Build and Test

### Step 4.1: Build Docker Image

```bash
echo ""
echo "=== BUILDING DOCKER IMAGE ==="
echo ""

# Build with test tag
echo "Building image (this may take 1-2 minutes)..."
docker build -t hotswap-test:local . 2>&1 | tee /tmp/docker-build.log

if [ ${PIPESTATUS[0]} -eq 0 ]; then
    echo "✅ Build succeeded"
else
    echo "❌ Build failed"
    echo ""
    echo "Last 20 lines of build output:"
    tail -20 /tmp/docker-build.log
    exit 1
fi
```

### Step 4.2: Verify Image Size

```bash
echo ""
echo "=== IMAGE SIZE ANALYSIS ==="
echo ""

IMAGE_SIZE=$(docker images hotswap-test:local --format "{{.Size}}")
echo "Image size: $IMAGE_SIZE"

# Check if size is reasonable (<500MB for runtime image)
SIZE_MB=$(docker images hotswap-test:local --format "{{.Size}}" | grep -oP "^[0-9.]+")
if (( $(echo "$SIZE_MB < 500" | bc -l) )); then
    echo "✅ Image size is reasonable (<500MB)"
else
    echo "⚠️  Image size is large (>500MB) - consider optimization"
    echo ""
    echo "Optimization tips:"
    echo "  1. Use multi-stage builds"
    echo "  2. Use Alpine base images"
    echo "  3. Update .dockerignore to exclude unnecessary files"
    echo "  4. Remove build artifacts in same RUN command"
fi

# Show layers
echo ""
echo "Image layers (top 10 largest):"
docker history hotswap-test:local --format "table {{.Size}}\t{{.CreatedBy}}" --no-trunc | head -11
```

### Step 4.3: Test Container Startup

```bash
echo ""
echo "=== TESTING CONTAINER STARTUP ==="
echo ""

# Run container
echo "Starting container..."
docker run -d -p 5001:5000 --name hotswap-test-container hotswap-test:local

# Wait for startup
sleep 5

# Check if container is running
if docker ps | grep -q hotswap-test-container; then
    echo "✅ Container started successfully"

    # Check logs
    echo ""
    echo "Container logs (last 10 lines):"
    docker logs hotswap-test-container 2>&1 | tail -10
else
    echo "❌ Container failed to start"
    echo ""
    echo "Container logs:"
    docker logs hotswap-test-container 2>&1
    docker rm hotswap-test-container
    exit 1
fi
```

### Step 4.4: Health Check

```bash
echo ""
echo "=== HEALTH CHECK ==="
echo ""

# Test API health endpoint
echo "Testing health endpoint..."
HEALTH_STATUS=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5001/health 2>/dev/null || echo "000")

if [ "$HEALTH_STATUS" = "200" ]; then
    echo "✅ Health check passed (HTTP $HEALTH_STATUS)"
else
    echo "⚠️  Health check failed (HTTP $HEALTH_STATUS)"
    echo "Container may not be fully started or health endpoint misconfigured"
fi

# Test Swagger UI
echo ""
echo "Testing Swagger UI..."
SWAGGER_STATUS=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5001/swagger/index.html 2>/dev/null || echo "000")

if [ "$SWAGGER_STATUS" = "200" ]; then
    echo "✅ Swagger UI accessible (HTTP $SWAGGER_STATUS)"
else
    echo "💡 Swagger UI not accessible (HTTP $SWAGGER_STATUS) - may be disabled in production"
fi
```

### Step 4.5: Clean Up Test Resources

```bash
echo ""
echo "=== CLEANING UP ==="
echo ""

# Stop and remove container
docker stop hotswap-test-container > /dev/null 2>&1
docker rm hotswap-test-container > /dev/null 2>&1
echo "✅ Test container removed"

# Remove test image
docker rmi hotswap-test:local > /dev/null 2>&1
echo "✅ Test image removed"
```

---

## Phase 5: docker-compose Stack Test

### Step 5.1: Start Full Stack

```bash
echo ""
echo "=== TESTING DOCKER-COMPOSE STACK ==="
echo ""

# Start services
echo "Starting services with docker-compose..."
docker-compose up -d

# Wait for services to start
echo "Waiting for services to start (10 seconds)..."
sleep 10
```

### Step 5.2: Verify All Services

```bash
echo ""
echo "Service status:"
docker-compose ps

echo ""

# Check each service is Up
FAILING_SERVICES=$(docker-compose ps | grep -v "Up" | grep -v "NAME" | wc -l)

if [ $FAILING_SERVICES -eq 0 ]; then
    echo "✅ All services are Up"
else
    echo "⚠️  $FAILING_SERVICES service(s) not running properly"
    echo ""
    echo "Check logs with: docker-compose logs [service-name]"
fi
```

### Step 5.3: Test Service Connectivity

```bash
echo ""
echo "=== TESTING SERVICE CONNECTIVITY ==="
echo ""

# Test API
API_STATUS=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/health 2>/dev/null || echo "000")
if [ "$API_STATUS" = "200" ]; then
    echo "✅ API is accessible (HTTP $API_STATUS)"
else
    echo "❌ API is not accessible (HTTP $API_STATUS)"
fi

# Test Jaeger (if configured)
if docker-compose ps | grep -q jaeger; then
    JAEGER_STATUS=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:16686 2>/dev/null || echo "000")
    if [ "$JAEGER_STATUS" = "200" ]; then
        echo "✅ Jaeger UI is accessible (HTTP $JAEGER_STATUS)"
    else
        echo "💡 Jaeger UI not accessible (HTTP $JAEGER_STATUS)"
    fi
fi

# Test Redis (if configured)
if docker-compose ps | grep -q redis; then
    if docker-compose exec -T redis redis-cli ping > /dev/null 2>&1; then
        echo "✅ Redis is responsive"
    else
        echo "⚠️  Redis is not responsive"
    fi
fi
```

### Step 5.4: Run Tests in Container (Optional)

```bash
echo ""
echo "=== RUNNING TESTS IN CONTAINER (OPTIONAL) ==="
echo ""

# Check if API service exists
if docker-compose ps | grep -q orchestrator-api; then
    echo "Running tests in containerized environment..."
    docker-compose run --rm orchestrator-api dotnet test

    if [ $? -eq 0 ]; then
        echo "✅ Tests passed in container"
    else
        echo "❌ Tests failed in container"
    fi
else
    echo "💡 API service not configured for testing"
fi
```

### Step 5.5: Clean Up Stack

```bash
echo ""
echo "=== CLEANING UP STACK ==="
echo ""

# Stop and remove containers, networks, volumes
docker-compose down -v

echo "✅ Docker-compose stack cleaned up"
```

---

## Phase 6: Security Scanning

### Step 6.1: Vulnerability Scan

```bash
echo ""
echo "=== SECURITY SCANNING ==="
echo ""

# Build image for scanning
docker build -t hotswap-scan:latest . > /dev/null 2>&1

echo "Scanning for vulnerabilities..."

# Try Docker Scout (built-in)
if docker scout --help > /dev/null 2>&1; then
    echo "Using Docker Scout..."
    docker scout cves hotswap-scan:latest --only-severity critical,high
else
    echo "💡 Docker Scout not available"
fi

echo ""

# Try Trivy (if installed)
if command -v trivy > /dev/null 2>&1; then
    echo "Using Trivy..."
    trivy image --severity HIGH,CRITICAL hotswap-scan:latest
else
    echo "💡 Trivy not installed (install: apt-get install trivy)"
fi

# Clean up
docker rmi hotswap-scan:latest > /dev/null 2>&1
```

---

## Phase 7: Generate Recommendations

### Step 7.1: Summary Report

```bash
echo ""
echo "===================================="
echo "DOCKER CONFIGURATION REVIEW REPORT"
echo "===================================="
echo ""
echo "Date: $(date +%Y-%m-%d)"
echo ""

# Configuration status
echo "📋 Configuration Status:"
[ -f "Dockerfile" ] && echo "  ✅ Dockerfile exists" || echo "  ❌ Dockerfile missing"
[ -f "docker-compose.yml" ] && echo "  ✅ docker-compose.yml exists" || echo "  ❌ docker-compose.yml missing"
[ -f ".dockerignore" ] && echo "  ✅ .dockerignore exists" || echo "  ⚠️  .dockerignore missing"

echo ""

# Build status
if docker images | grep -q hotswap; then
    echo "🔨 Build Status: ✅ Image builds successfully"
else
    echo "🔨 Build Status: ⚠️  Build validation needed"
fi

echo ""

# Best practices
echo "📚 Best Practices Checklist:"
echo "  [ ] Multi-stage build (smaller final image)"
echo "  [ ] Non-root user configured"
echo "  [ ] Alpine base images (optional, smaller attack surface)"
echo "  [ ] .dockerignore excludes unnecessary files"
echo "  [ ] Resource limits in docker-compose.yml"
echo "  [ ] Network isolation configured"
echo "  [ ] Health checks configured"
echo "  [ ] No secrets in Dockerfile"
echo "  [ ] Base images pinned to specific versions"
echo "  [ ] Security scan shows no HIGH/CRITICAL vulnerabilities"

echo ""

# Recommendations
echo "💡 Recommendations:"
echo "1. Review Dockerfile for optimization opportunities"
echo "2. Verify docker-compose.yml resource limits"
echo "3. Run security scan monthly"
echo "4. Update base images when security patches released"
echo "5. Test docker-compose stack after changes"
echo "6. Update documentation if ports/services change"

echo ""
```

---

## Quick Docker Maintenance Checklist

Monthly tasks (from CLAUDE.md):

```bash
# 1. Check for base image updates
docker pull mcr.microsoft.com/dotnet/sdk:8.0
docker pull mcr.microsoft.com/dotnet/aspnet:8.0

# 2. Scan for vulnerabilities
trivy image hotswap-orchestrator:latest --severity HIGH,CRITICAL

# 3. Review logs for errors
docker-compose logs --tail=100 orchestrator-api | grep -i error

# 4. Clean up unused resources
docker system prune -a --volumes
# WARNING: Removes all unused images, containers, volumes

# 5. Verify .dockerignore is current
du -sh .  # Check directory size before build

# 6. Test build performance
time docker build --no-cache -t hotswap-test .
```

---

## Common Docker Issues

### Issue: Build fails with "COPY failed"

**Solution**:
```bash
# Check .dockerignore isn't excluding required files
cat .dockerignore
# Verify file exists
ls path/to/file
```

### Issue: Service fails to start

**Solution**:
```bash
# Check logs
docker-compose logs service-name

# Common causes:
# - Missing environment variables
# - Port already in use
# - Configuration file not found
```

### Issue: Container exits immediately

**Solution**:
```bash
# Check container logs
docker logs container-name

# Common causes:
# - Application crash on startup
# - Missing dependencies
# - Incorrect ENTRYPOINT/CMD
```

---

## Integration with Pre-Commit Checklist

If you modified Dockerfile or docker-compose.yml:

```bash
# 1. Run Docker validation
/docker-helper

# 2. Verify documentation updated
/doc-sync-check

# 3. Run standard pre-commit
/precommit-check

# 4. Commit changes
git add Dockerfile docker-compose.yml CLAUDE.md README.md
git commit -m "feat: update Docker configuration

- Updated base image to .NET 8.0.121
- Added resource limits to docker-compose.yml
- Updated documentation"
```

---

## Success Criteria

Docker configuration is ready when:

- ✅ Dockerfile exists and builds successfully
- ✅ Multi-stage build implemented
- ✅ Image size is reasonable (<500MB for runtime)
- ✅ Non-root user configured
- ✅ docker-compose.yml is valid
- ✅ All services start successfully
- ✅ Health endpoints respond correctly
- ✅ No HIGH/CRITICAL vulnerabilities in security scan
- ✅ .dockerignore optimizes build context
- ✅ Documentation updated with any changes

---

## Performance Notes

- Dockerfile validation: ~1 minute
- Image build and test: ~2-3 minutes
- docker-compose stack test: ~1-2 minutes
- Security scan: ~1-2 minutes
- Total time: ~5-8 minutes

---

## Automation

Use this skill:
- When updating Docker configuration
- Monthly maintenance checks
- Before production deployments

```
/docker-helper
```

---

## Reference

Based on:
- CLAUDE.md: Docker Development and Maintenance section (724 lines)
- CLAUDE.md: Pre-Commit Checklist Step 7 (Docker validation)
- CLAUDE.md: Troubleshooting Docker Issues (314 lines)

**Key Principle**:
> "Keep Docker configuration secure, optimized, and up-to-date through regular maintenance and validation."
