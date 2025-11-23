#!/bin/bash

# Run All Load Tests
# Executes all k6 load test scenarios sequentially

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
BASE_URL="${BASE_URL:-http://localhost:5000}"
RESULTS_DIR="./results"

echo -e "${BLUE}================================${NC}"
echo -e "${BLUE}Load Testing Suite${NC}"
echo -e "${BLUE}================================${NC}"
echo ""
echo -e "Target API: ${GREEN}$BASE_URL${NC}"
echo ""

# Create results directory
mkdir -p "$RESULTS_DIR"

# Check if k6 is installed
if ! command -v k6 &> /dev/null; then
    echo -e "${RED}Error: k6 is not installed${NC}"
    echo "Install k6: https://k6.io/docs/getting-started/installation/"
    exit 1
fi

# Check if API is reachable
echo -e "${YELLOW}Checking API health...${NC}"
if curl -f -s "${BASE_URL}/health" > /dev/null; then
    echo -e "${GREEN}✓ API is healthy${NC}"
else
    echo -e "${RED}✗ API is not reachable at $BASE_URL${NC}"
    echo "Start the API before running load tests:"
    echo "  cd src/HotSwap.Distributed.Api"
    echo "  dotnet run --configuration Release"
    exit 1
fi

echo ""

# Run tests sequentially
run_test() {
    local test_name=$1
    local test_file=$2
    local duration=$3

    echo -e "${BLUE}================================${NC}"
    echo -e "${YELLOW}Running: $test_name${NC}"
    echo -e "${BLUE}================================${NC}"
    echo "Expected duration: $duration"
    echo ""

    if k6 run "scenarios/$test_file"; then
        echo -e "${GREEN}✓ $test_name completed successfully${NC}"
        return 0
    else
        echo -e "${RED}✗ $test_name failed${NC}"
        return 1
    fi
}

# Test execution tracking
TESTS_RUN=0
TESTS_PASSED=0
TESTS_FAILED=0

# 1. Quick tests first (< 10 minutes)
echo -e "${BLUE}Phase 1: Quick Performance Tests${NC}"
echo ""

if run_test "Metrics Endpoint Load Test" "metrics-load-test.js" "5 minutes"; then
    ((TESTS_PASSED++))
else
    ((TESTS_FAILED++))
fi
((TESTS_RUN++))
echo ""

if run_test "Spike Test" "spike-test.js" "4 minutes"; then
    ((TESTS_PASSED++))
else
    ((TESTS_FAILED++))
fi
((TESTS_RUN++))
echo ""

if run_test "Concurrent Deployments Test" "concurrent-deployments-test.js" "6 minutes"; then
    ((TESTS_PASSED++))
else
    ((TESTS_FAILED++))
fi
((TESTS_RUN++))
echo ""

# 2. Sustained load test (10 minutes)
echo -e "${BLUE}Phase 2: Sustained Load Test${NC}"
echo ""

if run_test "Deployment Endpoint Load Test" "deployments-load-test.js" "10 minutes"; then
    ((TESTS_PASSED++))
else
    ((TESTS_FAILED++))
fi
((TESTS_RUN++))
echo ""

# 3. Long-running tests (optional - comment out if needed)
echo -e "${BLUE}Phase 3: Long-Running Tests${NC}"
echo -e "${YELLOW}Note: These tests take 15+ minutes. Press Ctrl+C to skip.${NC}"
sleep 5
echo ""

if run_test "Stress Test" "stress-test.js" "15 minutes"; then
    ((TESTS_PASSED++))
else
    ((TESTS_FAILED++))
fi
((TESTS_RUN++))
echo ""

# Soak test is VERY long - optional
read -p "Run soak test (1 hour)? [y/N] " -n 1 -r
echo ""
if [[ $REPLY =~ ^[Yy]$ ]]; then
    if run_test "Soak Test" "soak-test.js" "1 hour"; then
        ((TESTS_PASSED++))
    else
        ((TESTS_FAILED++))
    fi
    ((TESTS_RUN++))
    echo ""
fi

# Summary
echo -e "${BLUE}================================${NC}"
echo -e "${BLUE}Load Testing Summary${NC}"
echo -e "${BLUE}================================${NC}"
echo ""
echo -e "Tests Run:    $TESTS_RUN"
echo -e "Tests Passed: ${GREEN}$TESTS_PASSED${NC}"
echo -e "Tests Failed: ${RED}$TESTS_FAILED${NC}"
echo ""

if [ $TESTS_FAILED -eq 0 ]; then
    echo -e "${GREEN}✓ All load tests passed!${NC}"
    echo ""
    echo "Results saved to $RESULTS_DIR/"
    exit 0
else
    echo -e "${RED}✗ Some load tests failed${NC}"
    echo ""
    echo "Review the results and check:"
    echo "  1. API logs for errors"
    echo "  2. Server resource usage (CPU, memory, connections)"
    echo "  3. PostgreSQL slow query log"
    echo "  4. Network latency"
    exit 1
fi
