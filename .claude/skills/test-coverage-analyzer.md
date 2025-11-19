# Test Coverage Analyzer Skill

**Description**: Analyzes test coverage to ensure the project maintains 85%+ coverage and identifies untested code paths.

**When to use**:
- After implementing new features
- During code reviews
- Monthly quality audits
- Before major releases
- When test coverage seems insufficient

## Instructions

This skill helps maintain the required 85%+ test coverage by analyzing coverage data and identifying gaps.

---

## Phase 1: Generate Coverage Report

### Step 1.1: Run Tests with Coverage Collection

```bash
# Clean previous coverage data
rm -rf tests/HotSwap.Distributed.Tests/TestResults/

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage" --verbosity normal
```

**Expected output**:
```
Test run for .../HotSwap.Distributed.Tests.dll (.NET 8.0)
Passed!  - Failed:     0, Passed:   568, Skipped:    14, Total:   582
```

**Coverage files location**:
```
tests/HotSwap.Distributed.Tests/TestResults/{guid}/coverage.cobertura.xml
```

### Step 1.2: Locate Coverage Report

```bash
# Find the latest coverage file
COVERAGE_FILE=$(find tests/HotSwap.Distributed.Tests/TestResults/ -name "coverage.cobertura.xml" -type f -printf '%T@ %p\n' | sort -rn | head -1 | cut -d' ' -f2)

echo "Latest coverage report: $COVERAGE_FILE"

# Verify file exists
if [ -f "$COVERAGE_FILE" ]; then
    echo "✅ Coverage report generated successfully"
else
    echo "❌ Coverage report not found"
    exit 1
fi
```

---

## Phase 2: Analyze Overall Coverage

### Step 2.1: Calculate Coverage Percentages

```bash
# Extract coverage metrics using xmllint or grep
echo "=== OVERALL COVERAGE SUMMARY ==="

# Line coverage
LINE_RATE=$(grep -oP 'line-rate="\K[0-9.]+' "$COVERAGE_FILE" | head -1)
LINE_PERCENT=$(echo "$LINE_RATE * 100" | bc)
echo "Line Coverage: ${LINE_PERCENT}%"

# Branch coverage
BRANCH_RATE=$(grep -oP 'branch-rate="\K[0-9.]+' "$COVERAGE_FILE" | head -1)
BRANCH_PERCENT=$(echo "$BRANCH_RATE * 100" | bc)
echo "Branch Coverage: ${BRANCH_PERCENT}%"

# Compare against target (85%)
TARGET=85
if (( $(echo "$LINE_PERCENT >= $TARGET" | bc -l) )); then
    echo "✅ Coverage meets target (>= ${TARGET}%)"
else
    echo "⚠️  Coverage below target (${LINE_PERCENT}% < ${TARGET}%)"
fi
```

### Step 2.2: Coverage by Project

```bash
echo ""
echo "=== COVERAGE BY PROJECT ==="

# Parse XML to show coverage by package (project)
# Note: This requires xml parsing tools like xmllint or python

python3 -c "
import xml.etree.ElementTree as ET
import sys

tree = ET.parse('$COVERAGE_FILE')
root = tree.getroot()

print(f'{'Project':<50} {'Line Coverage':<15} {'Branch Coverage':<15}')
print('-' * 80)

for package in root.findall('.//package'):
    name = package.get('name')
    line_rate = float(package.get('line-rate', 0))
    branch_rate = float(package.get('branch-rate', 0))

    print(f'{name:<50} {line_rate*100:>6.2f}% {branch_rate*100:>6.2f}%')
" 2>/dev/null || echo "⚠️  Python not available for detailed parsing"
```

---

## Phase 3: Identify Untested Code

### Step 3.1: Find Files with Low Coverage

```bash
echo ""
echo "=== FILES WITH LOW COVERAGE (<80%) ==="

python3 -c "
import xml.etree.ElementTree as ET

tree = ET.parse('$COVERAGE_FILE')
root = tree.getroot()

low_coverage_files = []

for pkg in root.findall('.//package'):
    for cls in pkg.findall('.//class'):
        filename = cls.get('filename')
        line_rate = float(cls.get('line-rate', 0))

        if line_rate < 0.80:  # Less than 80%
            low_coverage_files.append((filename, line_rate * 100))

# Sort by coverage (lowest first)
low_coverage_files.sort(key=lambda x: x[1])

if low_coverage_files:
    print(f'{'File':<60} {'Coverage':<10}')
    print('-' * 70)
    for filename, coverage in low_coverage_files[:10]:  # Top 10 worst
        # Shorten path for readability
        short_name = filename.split('/')[-1] if '/' in filename else filename
        print(f'{short_name:<60} {coverage:>6.2f}%')
else:
    print('✅ No files with coverage below 80%')
" 2>/dev/null || echo "⚠️  Python not available for detailed analysis"
```

### Step 3.2: Find Untested Methods

```bash
echo ""
echo "=== UNCOVERED METHODS ==="

# List methods with zero coverage
python3 -c "
import xml.etree.ElementTree as ET

tree = ET.parse('$COVERAGE_FILE')
root = tree.getroot()

uncovered = []

for method in root.findall('.//method'):
    name = method.get('name')
    line_rate = float(method.get('line-rate', 0))

    # Skip constructors and property getters/setters (often not worth testing directly)
    if line_rate == 0 and not any(x in name for x in ['.ctor', 'get_', 'set_']):
        # Get parent class name
        cls = method.find('../..')
        class_name = cls.get('name') if cls is not None else 'Unknown'
        uncovered.append((class_name, name))

if uncovered:
    print(f'{'Class':<40} {'Method':<40}')
    print('-' * 80)
    for class_name, method_name in uncovered[:15]:  # Top 15
        short_class = class_name.split('.')[-1]
        short_method = method_name.split('(')[0]  # Remove parameters
        print(f'{short_class:<40} {short_method:<40}')

    if len(uncovered) > 15:
        print(f'... and {len(uncovered) - 15} more')
else:
    print('✅ No uncovered methods found')
" 2>/dev/null || echo "⚠️  Python not available for method analysis"
```

---

## Phase 4: Analyze Critical Paths

### Step 4.1: Check Core Components Coverage

```bash
echo ""
echo "=== CORE COMPONENT COVERAGE ==="

# Check coverage for critical components
CRITICAL_COMPONENTS=(
    "HotSwap.Distributed.Orchestrator"
    "HotSwap.Distributed.Infrastructure"
    "HotSwap.Distributed.Api"
    "HotSwap.Distributed.Domain"
)

for component in "${CRITICAL_COMPONENTS[@]}"; do
    COVERAGE=$(python3 -c "
import xml.etree.ElementTree as ET
tree = ET.parse('$COVERAGE_FILE')
root = tree.getroot()

for pkg in root.findall('.//package'):
    if '$component' in pkg.get('name'):
        print(f\"{float(pkg.get('line-rate', 0)) * 100:.2f}\")
        break
" 2>/dev/null)

    if [ -n "$COVERAGE" ]; then
        if (( $(echo "$COVERAGE >= 85" | bc -l) )); then
            echo "✅ $component: ${COVERAGE}%"
        else
            echo "⚠️  $component: ${COVERAGE}% (below 85%)"
        fi
    fi
done
```

### Step 4.2: Identify Missing Test Scenarios

```bash
echo ""
echo "=== SUGGESTED TEST SCENARIOS ==="

# Analyze code structure to suggest missing tests
echo "Analyzing codebase for test gaps..."

# Check for controllers without integration tests
echo ""
echo "Controllers (should have integration tests):"
find src/HotSwap.Distributed.Api/Controllers -name "*.cs" 2>/dev/null | while read controller; do
    CONTROLLER_NAME=$(basename "$controller" .cs)

    # Check if integration tests exist
    if ! grep -rq "$CONTROLLER_NAME" tests/ 2>/dev/null; then
        echo "  ⚠️  $CONTROLLER_NAME - No integration tests found"
    else
        echo "  ✅ $CONTROLLER_NAME - Tests exist"
    fi
done

# Check for services without unit tests
echo ""
echo "Services (should have unit tests):"
find src/ -path "*/Services/*.cs" 2>/dev/null | while read service; do
    SERVICE_NAME=$(basename "$service" .cs)

    # Check if unit tests exist
    if ! grep -rq "$SERVICE_NAME" tests/ 2>/dev/null; then
        echo "  ⚠️  $SERVICE_NAME - No unit tests found"
    else
        echo "  ✅ $SERVICE_NAME - Tests exist"
    fi
done

# Check for public methods without tests
echo ""
echo "Checking for untested public methods..."
grep -r "public async Task" src/ --include="*.cs" | \
    grep -v "\.Tests\." | \
    head -10 | \
    while read line; do
        METHOD=$(echo "$line" | grep -oP "public async Task[^(]*\K[^(]+")
        FILE=$(echo "$line" | cut -d: -f1)

        if [ -n "$METHOD" ]; then
            if ! grep -rq "$METHOD" tests/ 2>/dev/null; then
                echo "  ⚠️  $METHOD (in $(basename $FILE))"
            fi
        fi
    done | head -5
```

---

## Phase 5: Generate Recommendations

### Step 5.1: Prioritize Testing Efforts

```bash
echo ""
echo "=== TESTING RECOMMENDATIONS ==="
echo ""

# Priority 1: Critical paths with low coverage
echo "🔴 PRIORITY 1 - Critical Components Below 85%:"
# (Already shown in Core Component Coverage above)

# Priority 2: Completely untested files
echo ""
echo "🟡 PRIORITY 2 - Untested Files:"
echo "Files with 0% coverage should be tested first"

# Priority 3: Complex methods without tests
echo ""
echo "🟢 PRIORITY 3 - Complex Methods:"
echo "Methods with high cyclomatic complexity should have thorough tests"

echo ""
echo "=== ACTION ITEMS ==="
echo "1. Add tests for uncovered methods (see list above)"
echo "2. Review low-coverage files (<80%) and add edge case tests"
echo "3. Ensure critical paths (Orchestrator, Infrastructure) have >90% coverage"
echo "4. Add integration tests for API controllers"
echo "5. Test error handling paths (try-catch blocks)"
```

---

## Phase 6: Human-Readable Report

### Step 6.1: Generate Summary Report

```bash
echo ""
echo "===================================="
echo "TEST COVERAGE ANALYSIS REPORT"
echo "===================================="
echo ""
echo "Date: $(date +%Y-%m-%d)"
echo "Target Coverage: 85%"
echo ""

# Overall metrics
LINE_RATE=$(grep -oP 'line-rate="\K[0-9.]+' "$COVERAGE_FILE" | head -1)
LINE_PERCENT=$(printf "%.2f" $(echo "$LINE_RATE * 100" | bc))
BRANCH_RATE=$(grep -oP 'branch-rate="\K[0-9.]+' "$COVERAGE_FILE" | head -1)
BRANCH_PERCENT=$(printf "%.2f" $(echo "$BRANCH_RATE * 100" | bc))

echo "📊 Coverage Metrics:"
echo "  Line Coverage:   ${LINE_PERCENT}%"
echo "  Branch Coverage: ${BRANCH_PERCENT}%"
echo ""

# Status
if (( $(echo "$LINE_PERCENT >= 85" | bc -l) )); then
    echo "✅ Status: PASS (Coverage meets 85% target)"
else
    DEFICIT=$(printf "%.2f" $(echo "85 - $LINE_PERCENT" | bc))
    echo "⚠️  Status: NEEDS IMPROVEMENT (${DEFICIT}% below target)"
fi

echo ""
echo "📝 Next Steps:"
echo "1. Review 'FILES WITH LOW COVERAGE' section above"
echo "2. Review 'UNCOVERED METHODS' section above"
echo "3. Follow TDD workflow for new tests (use /tdd-helper)"
echo "4. Re-run coverage analysis after adding tests"
echo ""
```

---

## Alternative: Manual Coverage Review (No Tools)

If automated tools aren't available:

```bash
# 1. List all source files
echo "=== SOURCE FILES ==="
find src/ -name "*.cs" -not -path "*/bin/*" -not -path "*/obj/*"

# 2. List all test files
echo ""
echo "=== TEST FILES ==="
find tests/ -name "*.cs" -not -path "*/bin/*" -not -path "*/obj/*"

# 3. Check which source files have corresponding tests
echo ""
echo "=== COVERAGE CHECK (Manual) ==="
find src/ -name "*.cs" -not -path "*/bin/*" -not -path "*/obj/*" | while read srcfile; do
    BASENAME=$(basename "$srcfile" .cs)

    # Check if test file exists
    if find tests/ -name "*${BASENAME}Tests.cs" | grep -q .; then
        echo "✅ $BASENAME has tests"
    else
        echo "❌ $BASENAME has NO tests"
    fi
done
```

---

## Integration with TDD Workflow

**Use this skill to guide TDD efforts**:

1. Run coverage analysis:
   ```
   /test-coverage-analyzer
   ```

2. Identify gaps from report

3. For each gap, use TDD workflow:
   ```
   /tdd-helper
   ```
   - 🔴 Write test for uncovered code
   - 🟢 Ensure test passes
   - 🔵 Refactor

4. Re-run coverage analysis to verify improvement

---

## Quick Coverage Check (Fast)

For a quick coverage check without detailed analysis:

```bash
# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage" --verbosity quiet

# Find coverage file
COVERAGE_FILE=$(find tests/ -name "coverage.cobertura.xml" | head -1)

# Show overall coverage
if [ -f "$COVERAGE_FILE" ]; then
    LINE_RATE=$(grep -oP 'line-rate="\K[0-9.]+' "$COVERAGE_FILE" | head -1)
    COVERAGE=$(printf "%.2f" $(echo "$LINE_RATE * 100" | bc))
    echo "Overall Coverage: ${COVERAGE}%"

    if (( $(echo "$COVERAGE >= 85" | bc -l) )); then
        echo "✅ Meets target (85%)"
    else
        echo "⚠️  Below target (85%)"
    fi
else
    echo "❌ Coverage report not found"
fi
```

---

## Success Criteria

Coverage analysis is complete when:

- ✅ Coverage report generated successfully
- ✅ Overall line coverage >= 85%
- ✅ Core components (Orchestrator, Infrastructure) >= 90%
- ✅ No critical paths with 0% coverage
- ✅ All public APIs have basic tests
- ✅ Recommendations documented for improvement

---

## Performance Notes

- Test execution with coverage: ~20-30 seconds
- Coverage report generation: ~2-5 seconds
- Analysis and reporting: ~10-15 seconds
- Total time: ~30-50 seconds

---

## Troubleshooting

### Issue: Coverage report not generated

```bash
# Install coverlet.collector package
dotnet add tests/HotSwap.Distributed.Tests/ package coverlet.collector
dotnet restore
dotnet test --collect:"XPlat Code Coverage"
```

### Issue: Python not available

- Use manual coverage review (see "Alternative" section above)
- Or install Python: `apt-get install python3` (if root access)

### Issue: bc command not found

```bash
# Install bc for calculations
apt-get install bc
```

---

## Automation

Run this skill:
- After implementing new features
- During monthly quality audits
- Before releases

```
/test-coverage-analyzer
```

---

## Reference

Based on:
- CLAUDE.md: Testing Requirements (line 1057)
- CLAUDE.md: "Target >80% code coverage (current: 85%+)"
- CLAUDE.md: TDD Workflow (mandatory for all code changes)

**Key Principle**:
> "Aim for high code coverage (>80%). Current project maintains 85%+ coverage."
