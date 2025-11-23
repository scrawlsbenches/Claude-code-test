#!/bin/bash
# Test script for HotSwap Helm Chart
# This script validates the Helm chart without deploying it

set -e

CHART_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TEMP_DIR=$(mktemp -d)
trap "rm -rf $TEMP_DIR" EXIT

echo "================================"
echo "HotSwap Helm Chart Test Suite"
echo "================================"
echo ""

# Check if helm is installed
if ! command -v helm &> /dev/null; then
    echo "❌ ERROR: Helm is not installed"
    echo "Install Helm from: https://helm.sh/docs/intro/install/"
    exit 1
fi

echo "✓ Helm version: $(helm version --short)"
echo ""

# Test 1: Lint the chart
echo "Test 1: Linting chart..."
if helm lint "$CHART_DIR"; then
    echo "✓ Chart linting passed"
else
    echo "❌ Chart linting failed"
    exit 1
fi
echo ""

# Test 2: Template rendering with default values
echo "Test 2: Rendering templates with default values..."
if helm template test-release "$CHART_DIR" > "$TEMP_DIR/default.yaml"; then
    echo "✓ Template rendering passed (default values)"
    RESOURCE_COUNT=$(grep -c "^kind:" "$TEMP_DIR/default.yaml" || true)
    echo "  Generated $RESOURCE_COUNT Kubernetes resources"
else
    echo "❌ Template rendering failed"
    exit 1
fi
echo ""

# Test 3: Template rendering with dev values
echo "Test 3: Rendering templates with dev values..."
if helm template test-release "$CHART_DIR" -f "$CHART_DIR/values-dev.yaml" > "$TEMP_DIR/dev.yaml"; then
    echo "✓ Template rendering passed (dev values)"
else
    echo "❌ Template rendering failed (dev values)"
    exit 1
fi
echo ""

# Test 4: Template rendering with staging values
echo "Test 4: Rendering templates with staging values..."
if helm template test-release "$CHART_DIR" -f "$CHART_DIR/values-staging.yaml" --set image.tag=v1.0.0 > "$TEMP_DIR/staging.yaml"; then
    echo "✓ Template rendering passed (staging values)"
else
    echo "❌ Template rendering failed (staging values)"
    exit 1
fi
echo ""

# Test 5: Template rendering with production values
echo "Test 5: Rendering templates with production values..."
if helm template test-release "$CHART_DIR" -f "$CHART_DIR/values-production.yaml" --set image.tag=v1.0.0 > "$TEMP_DIR/production.yaml"; then
    echo "✓ Template rendering passed (production values)"
else
    echo "❌ Template rendering failed (production values)"
    exit 1
fi
echo ""

# Test 6: Validate generated YAML
echo "Test 6: Validating generated YAML syntax..."
YAML_VALID=true
for env in default dev staging production; do
    if python3 -c "import yaml; yaml.safe_load_all(open('$TEMP_DIR/$env.yaml'))" 2>/dev/null; then
        echo "  ✓ $env.yaml is valid YAML"
    else
        echo "  ❌ $env.yaml has YAML syntax errors"
        YAML_VALID=false
    fi
done

if [ "$YAML_VALID" = false ]; then
    echo "❌ YAML validation failed"
    exit 1
fi
echo ""

# Test 7: Check required resources
echo "Test 7: Checking required Kubernetes resources..."
REQUIRED_RESOURCES=(
    "Deployment"
    "Service"
    "ServiceAccount"
    "ConfigMap"
    "Secret"
)

for resource in "${REQUIRED_RESOURCES[@]}"; do
    if grep -q "^kind: $resource$" "$TEMP_DIR/default.yaml"; then
        echo "  ✓ $resource defined"
    else
        echo "  ❌ $resource missing"
        exit 1
    fi
done
echo ""

# Test 8: Check optional resources based on values
echo "Test 8: Checking conditional resources..."

# HPA should be enabled in default values
if grep -q "^kind: HorizontalPodAutoscaler$" "$TEMP_DIR/default.yaml"; then
    echo "  ✓ HorizontalPodAutoscaler defined (autoscaling enabled)"
else
    echo "  ❌ HorizontalPodAutoscaler missing (should be enabled by default)"
    exit 1
fi

# HPA should be disabled in dev values
if ! grep -q "^kind: HorizontalPodAutoscaler$" "$TEMP_DIR/dev.yaml"; then
    echo "  ✓ HorizontalPodAutoscaler not defined in dev (autoscaling disabled)"
else
    echo "  ❌ HorizontalPodAutoscaler should be disabled in dev"
    exit 1
fi

# Ingress should be enabled in staging/production
if grep -q "^kind: Ingress$" "$TEMP_DIR/staging.yaml"; then
    echo "  ✓ Ingress defined in staging"
else
    echo "  ❌ Ingress missing in staging"
    exit 1
fi

echo ""

# Test 9: Validate image tag is set
echo "Test 9: Validating image configuration..."
if grep -q "image: hotswap/hotswap-api:v1.0.0" "$TEMP_DIR/staging.yaml"; then
    echo "  ✓ Image tag correctly set in staging"
else
    echo "  ❌ Image tag not correctly set in staging"
    exit 1
fi
echo ""

# Test 10: Check security context
echo "Test 10: Validating security contexts..."
if grep -q "runAsNonRoot: true" "$TEMP_DIR/default.yaml"; then
    echo "  ✓ runAsNonRoot configured"
else
    echo "  ❌ runAsNonRoot not configured"
    exit 1
fi

if grep -q "readOnlyRootFilesystem: true" "$TEMP_DIR/default.yaml"; then
    echo "  ✓ readOnlyRootFilesystem configured"
else
    echo "  ❌ readOnlyRootFilesystem not configured"
    exit 1
fi
echo ""

# Test 11: Package the chart
echo "Test 11: Packaging chart..."
if helm package "$CHART_DIR" -d "$TEMP_DIR" > /dev/null; then
    PACKAGE=$(ls "$TEMP_DIR"/*.tgz)
    echo "✓ Chart packaged successfully: $(basename "$PACKAGE")"

    # Get package size
    SIZE=$(du -h "$PACKAGE" | cut -f1)
    echo "  Package size: $SIZE"
else
    echo "❌ Chart packaging failed"
    exit 1
fi
echo ""

# Test 12: Verify Chart.yaml metadata
echo "Test 12: Verifying chart metadata..."
CHART_NAME=$(helm show chart "$CHART_DIR" | grep "^name:" | awk '{print $2}')
CHART_VERSION=$(helm show chart "$CHART_DIR" | grep "^version:" | awk '{print $2}')
APP_VERSION=$(helm show chart "$CHART_DIR" | grep "^appVersion:" | awk '{print $2}')

echo "  Chart Name: $CHART_NAME"
echo "  Chart Version: $CHART_VERSION"
echo "  App Version: $APP_VERSION"

if [ "$CHART_NAME" != "hotswap" ]; then
    echo "❌ Chart name should be 'hotswap'"
    exit 1
fi
echo "  ✓ Chart metadata valid"
echo ""

# Summary
echo "================================"
echo "✅ ALL TESTS PASSED"
echo "================================"
echo ""
echo "Chart is ready for deployment!"
echo ""
echo "To install the chart:"
echo "  helm install hotswap $CHART_DIR --set image.tag=v1.0.0"
echo ""
echo "To see rendered templates:"
echo "  helm template hotswap $CHART_DIR"
echo ""
