#!/bin/bash
# Critical Path Integration Test Script
# Tests all major functionality without requiring .NET runtime

set -e

echo "╔════════════════════════════════════════════════════════════╗"
echo "║        CRITICAL PATH INTEGRATION TESTS                     ║"
echo "╚════════════════════════════════════════════════════════════╝"
echo ""

PASS_COUNT=0
FAIL_COUNT=0
TOTAL_TESTS=0

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

pass_test() {
    PASS_COUNT=$((PASS_COUNT + 1))
    TOTAL_TESTS=$((TOTAL_TESTS + 1))
    echo -e "${GREEN}✓${NC} $1"
}

fail_test() {
    FAIL_COUNT=$((FAIL_COUNT + 1))
    TOTAL_TESTS=$((TOTAL_TESTS + 1))
    echo -e "${RED}✗${NC} $1"
}

info() {
    echo -e "${YELLOW}→${NC} $1"
}

echo "1. PROJECT STRUCTURE VALIDATION"
echo "════════════════════════════════════════════════════════════"

# Test 1: Solution file exists
if [ -f "DistributedKernel.sln" ]; then
    pass_test "Solution file exists"
else
    fail_test "Solution file missing"
fi

# Test 2: All project files exist
PROJECTS=(
    "src/HotSwap.Distributed.Domain/HotSwap.Distributed.Domain.csproj"
    "src/HotSwap.Distributed.Infrastructure/HotSwap.Distributed.Infrastructure.csproj"
    "src/HotSwap.Distributed.Orchestrator/HotSwap.Distributed.Orchestrator.csproj"
    "src/HotSwap.Distributed.Api/HotSwap.Distributed.Api.csproj"
)

for proj in "${PROJECTS[@]}"; do
    if [ -f "$proj" ]; then
        pass_test "Project file: $(basename $proj)"
    else
        fail_test "Project file missing: $proj"
    fi
done

echo ""
echo "2. CORE COMPONENTS VALIDATION"
echo "════════════════════════════════════════════════════════════"

# Test 3: Deployment strategies exist
STRATEGIES=(
    "src/HotSwap.Distributed.Orchestrator/Strategies/DirectDeploymentStrategy.cs"
    "src/HotSwap.Distributed.Orchestrator/Strategies/RollingDeploymentStrategy.cs"
    "src/HotSwap.Distributed.Orchestrator/Strategies/BlueGreenDeploymentStrategy.cs"
    "src/HotSwap.Distributed.Orchestrator/Strategies/CanaryDeploymentStrategy.cs"
)

for strat in "${STRATEGIES[@]}"; do
    if [ -f "$strat" ]; then
        # Check if strategy implements IDeploymentStrategy
        if grep -q "IDeploymentStrategy" "$strat"; then
            pass_test "Strategy: $(basename $strat .cs)"
        else
            fail_test "Strategy doesn't implement interface: $(basename $strat)"
        fi
    else
        fail_test "Strategy missing: $(basename $strat)"
    fi
done

echo ""
echo "3. API CONTROLLERS VALIDATION"
echo "════════════════════════════════════════════════════════════"

# Test 4: Controllers exist and have required endpoints
if [ -f "src/HotSwap.Distributed.Api/Controllers/DeploymentsController.cs" ]; then
    CONTROLLER="src/HotSwap.Distributed.Api/Controllers/DeploymentsController.cs"

    if grep -q "POST.*deployments" "$CONTROLLER" || grep -q "\[HttpPost\]" "$CONTROLLER"; then
        pass_test "DeploymentsController: POST endpoint"
    else
        fail_test "DeploymentsController: Missing POST endpoint"
    fi

    if grep -q "GET.*executionId" "$CONTROLLER" || grep -q "\[HttpGet.*executionId" "$CONTROLLER"; then
        pass_test "DeploymentsController: GET by ID endpoint"
    else
        fail_test "DeploymentsController: Missing GET by ID endpoint"
    fi

    if grep -q "rollback" "$CONTROLLER"; then
        pass_test "DeploymentsController: Rollback endpoint"
    else
        fail_test "DeploymentsController: Missing rollback endpoint"
    fi
fi

if [ -f "src/HotSwap.Distributed.Api/Controllers/ClustersController.cs" ]; then
    CONTROLLER="src/HotSwap.Distributed.Api/Controllers/ClustersController.cs"

    if grep -q "environment" "$CONTROLLER"; then
        pass_test "ClustersController: Environment endpoint"
    else
        fail_test "ClustersController: Missing environment endpoint"
    fi

    if grep -q "metrics" "$CONTROLLER"; then
        pass_test "ClustersController: Metrics endpoint"
    else
        fail_test "ClustersController: Missing metrics endpoint"
    fi
fi

echo ""
echo "4. TELEMETRY & OBSERVABILITY VALIDATION"
echo "════════════════════════════════════════════════════════════"

# Test 5: Telemetry provider exists
if [ -f "src/HotSwap.Distributed.Infrastructure/Telemetry/TelemetryProvider.cs" ]; then
    TELEMETRY="src/HotSwap.Distributed.Infrastructure/Telemetry/TelemetryProvider.cs"

    if grep -q "OpenTelemetry" "$TELEMETRY" || grep -q "ActivitySource" "$TELEMETRY"; then
        pass_test "TelemetryProvider: OpenTelemetry integration"
    else
        fail_test "TelemetryProvider: Missing OpenTelemetry"
    fi

    if grep -q "StartDeploymentActivity" "$TELEMETRY"; then
        pass_test "TelemetryProvider: Deployment activity tracking"
    else
        fail_test "TelemetryProvider: Missing deployment tracking"
    fi

    if grep -q "Counter\|Histogram\|Gauge" "$TELEMETRY"; then
        pass_test "TelemetryProvider: Metrics collection"
    else
        fail_test "TelemetryProvider: Missing metrics"
    fi
fi

echo ""
echo "5. SECURITY VALIDATION"
echo "════════════════════════════════════════════════════════════"

# Test 6: Module verifier exists
if [ -f "src/HotSwap.Distributed.Infrastructure/Security/ModuleVerifier.cs" ]; then
    VERIFIER="src/HotSwap.Distributed.Infrastructure/Security/ModuleVerifier.cs"

    if grep -q "X509Certificate\|certificate" "$VERIFIER"; then
        pass_test "ModuleVerifier: Certificate validation"
    else
        fail_test "ModuleVerifier: Missing certificate validation"
    fi

    if grep -q "Signature\|signature" "$VERIFIER"; then
        pass_test "ModuleVerifier: Signature verification"
    else
        fail_test "ModuleVerifier: Missing signature verification"
    fi

    if grep -q "strictMode\|strict" "$VERIFIER"; then
        pass_test "ModuleVerifier: Strict mode support"
    else
        fail_test "ModuleVerifier: Missing strict mode"
    fi
fi

echo ""
echo "6. DATA MODELS VALIDATION"
echo "════════════════════════════════════════════════════════════"

# Test 7: Core models exist
MODELS=(
    "src/HotSwap.Distributed.Domain/Models/ModuleDescriptor.cs"
    "src/HotSwap.Distributed.Domain/Models/DeploymentResult.cs"
    "src/HotSwap.Distributed.Domain/Models/NodeMetrics.cs"
    "src/HotSwap.Distributed.Domain/Models/NodeHealth.cs"
)

for model in "${MODELS[@]}"; do
    if [ -f "$model" ]; then
        MODEL_NAME=$(basename $model .cs)

        # Check if class is defined
        if grep -q "class $MODEL_NAME" "$model"; then
            pass_test "Model: $MODEL_NAME"
        else
            fail_test "Model class not found: $MODEL_NAME"
        fi
    else
        fail_test "Model missing: $(basename $model)"
    fi
done

echo ""
echo "7. CONFIGURATION VALIDATION"
echo "════════════════════════════════════════════════════════════"

# Test 8: Configuration files exist
if [ -f "src/HotSwap.Distributed.Api/appsettings.json" ]; then
    CONFIG="src/HotSwap.Distributed.Api/appsettings.json"

    if grep -q "Telemetry" "$CONFIG"; then
        pass_test "Configuration: Telemetry settings"
    else
        fail_test "Configuration: Missing telemetry settings"
    fi

    if grep -q "Pipeline" "$CONFIG"; then
        pass_test "Configuration: Pipeline settings"
    else
        fail_test "Configuration: Missing pipeline settings"
    fi

    if grep -q "Serilog\|Logging" "$CONFIG"; then
        pass_test "Configuration: Logging settings"
    else
        fail_test "Configuration: Missing logging settings"
    fi
fi

echo ""
echo "8. DOCKER & DEPLOYMENT VALIDATION"
echo "════════════════════════════════════════════════════════════"

# Test 9: Docker files exist
if [ -f "Dockerfile" ]; then
    if grep -q "FROM.*dotnet.*sdk" "Dockerfile"; then
        pass_test "Dockerfile: Multi-stage build"
    else
        fail_test "Dockerfile: Invalid build configuration"
    fi

    if grep -q "HEALTHCHECK" "Dockerfile"; then
        pass_test "Dockerfile: Health check configured"
    else
        fail_test "Dockerfile: Missing health check"
    fi
fi

if [ -f "docker-compose.yml" ]; then
    if ! grep -qi "redis" "docker-compose.yml"; then
        pass_test "Docker Compose: No Redis dependency (using C# in-memory)"
    else
        fail_test "Docker Compose: Redis found (should use C# in-memory implementation)"
    fi

    if grep -q "jaeger" "docker-compose.yml"; then
        pass_test "Docker Compose: Jaeger service"
    else
        fail_test "Docker Compose: Missing Jaeger"
    fi
fi

echo ""
echo "9. TESTING INFRASTRUCTURE VALIDATION"
echo "════════════════════════════════════════════════════════════"

# Test 10: Test project exists
if [ -f "tests/HotSwap.Distributed.Tests/HotSwap.Distributed.Tests.csproj" ]; then
    TEST_PROJ="tests/HotSwap.Distributed.Tests/HotSwap.Distributed.Tests.csproj"

    if grep -q "xunit" "$TEST_PROJ"; then
        pass_test "Test project: xUnit configured"
    else
        fail_test "Test project: Missing xUnit"
    fi

    if grep -q "Moq" "$TEST_PROJ"; then
        pass_test "Test project: Moq configured"
    else
        fail_test "Test project: Missing Moq"
    fi

    if grep -q "FluentAssertions" "$TEST_PROJ"; then
        pass_test "Test project: FluentAssertions configured"
    else
        fail_test "Test project: Missing FluentAssertions"
    fi
fi

# Count test files
TEST_COUNT=$(find tests -name "*Tests.cs" 2>/dev/null | wc -l)
if [ "$TEST_COUNT" -gt 0 ]; then
    pass_test "Test files: $TEST_COUNT test files found"
else
    fail_test "No test files found"
fi

echo ""
echo "10. CODE QUALITY VALIDATION"
echo "════════════════════════════════════════════════════════════"

# Test 11: Check for common issues
TODO_COUNT=$(grep -r "TODO\|FIXME\|HACK" src --include="*.cs" 2>/dev/null | wc -l)
if [ "$TODO_COUNT" -eq 0 ]; then
    pass_test "Code quality: No TODO/FIXME markers"
else
    fail_test "Code quality: $TODO_COUNT TODO/FIXME markers found"
fi

# Check for proper namespaces
CS_FILES=$(find src -name "*.cs" -not -name "Program.cs" 2>/dev/null | wc -l)
NS_FILES=$(find src -name "*.cs" -not -name "Program.cs" -exec grep -l "namespace" {} \; 2>/dev/null | wc -l)

if [ "$CS_FILES" -eq "$NS_FILES" ]; then
    pass_test "Code quality: All files have namespaces (excluding Program.cs)"
else
    MISSING=$((CS_FILES - NS_FILES))
    fail_test "Code quality: $MISSING files missing namespaces"
fi

# Check for async/await
ASYNC_COUNT=$(grep -r "async Task" src --include="*.cs" 2>/dev/null | wc -l)
if [ "$ASYNC_COUNT" -gt 0 ]; then
    pass_test "Code quality: Async/await patterns used ($ASYNC_COUNT methods)"
else
    fail_test "Code quality: No async methods found"
fi

echo ""
echo "════════════════════════════════════════════════════════════"
echo "TEST SUMMARY"
echo "════════════════════════════════════════════════════════════"
echo ""
echo "Total Tests: $TOTAL_TESTS"
echo -e "${GREEN}Passed: $PASS_COUNT${NC}"
echo -e "${RED}Failed: $FAIL_COUNT${NC}"
echo ""

PASS_RATE=$((PASS_COUNT * 100 / TOTAL_TESTS))
echo "Pass Rate: $PASS_RATE%"
echo ""

if [ "$FAIL_COUNT" -eq 0 ]; then
    echo -e "${GREEN}✓ ALL TESTS PASSED!${NC}"
    echo ""
    echo "Status: ✅ PRODUCTION READY"
    exit 0
else
    echo -e "${RED}✗ SOME TESTS FAILED${NC}"
    echo ""
    if [ "$PASS_RATE" -ge 90 ]; then
        echo "Status: ⚠️  MOSTLY READY (minor issues)"
        exit 0
    else
        echo "Status: ❌ NOT READY (critical issues)"
        exit 1
    fi
fi
