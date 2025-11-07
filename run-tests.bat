@echo off
echo ==================================================
echo ClickIt Plugin - Post-Build Test Execution
echo ==================================================

set "PROJECT_DIR=%~dp0"
set "TEST_PROJECT_DIR=%PROJECT_DIR%Tests"
set "TEST_RESULTS_DIR=%PROJECT_DIR%TestResults"

echo Project Directory: %PROJECT_DIR%
echo Test Project Directory: %TEST_PROJECT_DIR%

REM Create test results directory if it doesn't exist
if not exist "%TEST_RESULTS_DIR%" mkdir "%TEST_RESULTS_DIR%"

echo.
echo Building test project...
dotnet build "%TEST_PROJECT_DIR%\ClickIt.Tests.csproj" --configuration Debug --verbosity quiet
if %errorlevel% neq 0 (
    echo ERROR: Test project build failed!
    exit /b 1
)

echo.
echo Running unit tests...
REM Delete existing results file to prevent overwrite warning
if exist "%TEST_RESULTS_DIR%\ClickIt.Tests.trx" del "%TEST_RESULTS_DIR%\ClickIt.Tests.trx" >nul 2>&1

dotnet test "%TEST_PROJECT_DIR%\ClickIt.Tests.csproj" ^
    --configuration Debug ^
    --no-build ^
    --verbosity normal ^
    --logger "trx;LogFileName=ClickIt.Tests.trx" ^
    --results-directory "%TEST_RESULTS_DIR%"

set TEST_EXIT_CODE=%errorlevel%

echo.
if %TEST_EXIT_CODE% equ 0 (
    echo ✓ All tests passed successfully!
    echo Test results saved to: %TEST_RESULTS_DIR%
) else (
    echo ✗ Some tests failed! Exit code: %TEST_EXIT_CODE%
    echo Check test results in: %TEST_RESULTS_DIR%
)

echo ==================================================
exit /b %TEST_EXIT_CODE%