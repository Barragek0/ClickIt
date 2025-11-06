#!/bin/bash
set -e

echo "=================================================="
echo "ClickIt Plugin - Post-Build Test Execution"
echo "=================================================="

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TEST_PROJECT_DIR="$PROJECT_DIR/Tests"
TEST_RESULTS_DIR="$PROJECT_DIR/TestResults"

echo "Project Directory: $PROJECT_DIR"
echo "Test Project Directory: $TEST_PROJECT_DIR"

# Create test results directory if it doesn't exist
mkdir -p "$TEST_RESULTS_DIR"

echo ""
echo "Building test project..."
dotnet build "$TEST_PROJECT_DIR/ClickIt.Tests.csproj" --configuration Debug --verbosity quiet

echo ""
echo "Running unit tests..."
dotnet test "$TEST_PROJECT_DIR/ClickIt.Tests.csproj" \
    --configuration Debug \
    --no-build \
    --verbosity normal \
    --logger "trx;LogFileName=ClickIt.Tests.trx" \
    --results-directory "$TEST_RESULTS_DIR" \
    --collect:"XPlat Code Coverage" \
    --settings "$PROJECT_DIR/runsettings.xml"

TEST_EXIT_CODE=$?

echo ""
if [ $TEST_EXIT_CODE -eq 0 ]; then
    echo "✓ All tests passed successfully!"
    echo "Test results saved to: $TEST_RESULTS_DIR"
else
    echo "✗ Some tests failed! Exit code: $TEST_EXIT_CODE"
    echo "Check test results in: $TEST_RESULTS_DIR"
fi

echo "=================================================="
exit $TEST_EXIT_CODE