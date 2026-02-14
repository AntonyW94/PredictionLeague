@echo off
setlocal enabledelayedexpansion

:: Navigate to the repository root (two levels up from this script)
cd /d "%~dp0..\.."

echo.
echo ========================================
echo   Unit Test Coverage Report
echo ========================================
echo.

:: Clean previous coverage data
if exist coverage (
    echo Cleaning previous coverage data...
    rmdir /s /q coverage
)

:: Run all unit test projects with coverage collection
echo Running unit tests with coverage collection...
echo.

set TEST_FAILED=0
for /r tests\Unit %%f in (*.csproj) do (
    echo Running: %%~nf
    dotnet test "%%f" --collect:"XPlat Code Coverage" --results-directory .\coverage
    if !ERRORLEVEL! neq 0 set TEST_FAILED=1
)

if %TEST_FAILED%==1 (
    echo.
    echo Some tests failed. Coverage report will still be generated for executed tests.
    echo.
)

:: Check coverage data was produced
if not exist coverage (
    echo.
    echo No coverage data was produced. Ensure test projects reference coverlet.collector.
    exit /b 1
)

:: Generate HTML report
echo.
echo Generating coverage report...
reportgenerator "-reports:.\coverage\**\coverage.cobertura.xml" "-targetdir:.\coverage\report" "-reporttypes:Html"
if %ERRORLEVEL% neq 0 (
    echo.
    echo Failed to generate report. Is dotnet-reportgenerator-globaltool installed?
    echo Run: dotnet tool install --global dotnet-reportgenerator-globaltool
    exit /b 1
)

:: Open the report in the default browser
echo.
echo Opening coverage report...
start "" ".\coverage\report\index.html"

echo.
echo Report saved to: coverage\report\index.html
echo.
