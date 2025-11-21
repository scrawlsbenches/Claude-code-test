#!/bin/bash
# Code Coverage Collection Script
# Collects coverage for unit tests only (excludes integration and smoke tests)

echo "========================================" | tee coverage-collection.log
echo "Code Coverage Collection Started" | tee -a coverage-collection.log
echo "Date: $(date)" | tee -a coverage-collection.log
echo "========================================" | tee -a coverage-collection.log
echo "" | tee -a coverage-collection.log

# Clean previous results
echo "Cleaning previous test results..." | tee -a coverage-collection.log
rm -rf TestResults/UnitTests
mkdir -p TestResults/UnitTests

# Run coverage for HotSwap.Distributed.Tests
echo "" | tee -a coverage-collection.log
echo "========================================" | tee -a coverage-collection.log
echo "Running coverage for HotSwap.Distributed.Tests" | tee -a coverage-collection.log
echo "========================================" | tee -a coverage-collection.log
dotnet test tests/HotSwap.Distributed.Tests/HotSwap.Distributed.Tests.csproj \
  --collect:"XPlat Code Coverage" \
  --results-directory ./TestResults/UnitTests \
  --verbosity normal 2>&1 | tee -a coverage-distributed-tests.log

# Run coverage for HotSwap.KnowledgeGraph.Tests
echo "" | tee -a coverage-collection.log
echo "========================================" | tee -a coverage-collection.log
echo "Running coverage for HotSwap.KnowledgeGraph.Tests" | tee -a coverage-collection.log
echo "========================================" | tee -a coverage-collection.log
dotnet test tests/HotSwap.KnowledgeGraph.Tests/HotSwap.KnowledgeGraph.Tests.csproj \
  --collect:"XPlat Code Coverage" \
  --results-directory ./TestResults/UnitTests \
  --verbosity normal 2>&1 | tee -a coverage-knowledgegraph-tests.log

# Find and list coverage files
echo "" | tee -a coverage-collection.log
echo "========================================" | tee -a coverage-collection.log
echo "Coverage files generated:" | tee -a coverage-collection.log
echo "========================================" | tee -a coverage-collection.log
find TestResults/UnitTests -name "coverage.cobertura.xml" -type f | tee -a coverage-collection.log

echo "" | tee -a coverage-collection.log
echo "========================================" | tee -a coverage-collection.log
echo "Code Coverage Collection Completed" | tee -a coverage-collection.log
echo "Date: $(date)" | tee -a coverage-collection.log
echo "========================================" | tee -a coverage-collection.log
echo "" | tee -a coverage-collection.log
echo "Log files created:" | tee -a coverage-collection.log
echo "  - coverage-collection.log (summary)" | tee -a coverage-collection.log
echo "  - coverage-distributed-tests.log (detailed HotSwap.Distributed.Tests)" | tee -a coverage-collection.log
echo "  - coverage-knowledgegraph-tests.log (detailed HotSwap.KnowledgeGraph.Tests)" | tee -a coverage-collection.log
