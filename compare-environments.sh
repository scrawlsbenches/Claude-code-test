#!/bin/bash
# compare-environments.sh - Diagnostic script to compare local vs CI/CD

echo "=== Local Environment Info ==="
echo "OS: $(uname -a)"
echo "CPU cores: $(nproc)"
echo "Memory: $(free -h 2>/dev/null | grep Mem | awk '{print $2}' || echo 'N/A')"
echo ".NET version: $(dotnet --version)"
echo "Date: $(date)"
echo ""

echo "=== Running Tests with Timing ==="
start_time=$(date +%s)

dotnet test /home/user/Claude-code-test/DistributedKernel.sln \
  --configuration Release \
  --verbosity minimal \
  --collect:"XPlat Code Coverage" \
  --results-directory ./TestResults

end_time=$(date +%s)
duration=$((end_time - start_time))

echo ""
echo "=== Test Execution Summary ==="
echo "Total duration: ${duration} seconds"
echo "Tests with coverage collection"
echo ""
echo "Compare this with CI/CD logs to identify differences"
