# ClickIt Plugin Testing Framework

This document describes the comprehensive automated testing setup for the ClickIt plugin.

## 🧪 Test Suite Overview

The test suite provides comprehensive coverage of all critical functionality:

### 📊 Test Categories

1. **Weight Calculation Tests** (`WeightCalculationTests.cs`)
   - Tests `CalculateUpsideWeight` and `CalculateDownsideWeight` methods
   - Validates arithmetic correctness and edge cases
   - Ensures proper handling of null/empty values

2. **Settings Tests** (`SettingsTests.cs`)
   - Validates `ModTiers` dictionary functionality
   - Tests default weight initialization
   - Verifies `GetModTier` method behavior
   - Ensures settings persistence and customization

3. **Altar Decision Logic Tests** (`AltarDecisionLogicTests.cs`)
   - Tests `EvaluateAltarWeights` method with various scenarios
   - Validates override logic (90+ weights)
   - Ensures correct decision making under different conditions
   - Tests color coding for UI feedback

4. **Constants and Data Validation Tests** (`AltarModsConstantsTests.cs`)
   - Validates `AltarModsConstants` data integrity
   - Ensures all mods have valid weights (1-100 range)
   - Verifies unique mod IDs and proper categorization
   - Tests high-value and dangerous mod classifications

5. **Integration Tests** (`IntegrationTests.cs`)
   - End-to-end workflow testing
   - Real-world scenario simulations
   - Custom weight impact validation
   - Edge case handling verification

## 🚀 Running Tests

### Local Development

#### Windows (Batch Script)
```bash
# Run all tests with coverage
.\run-tests.bat
```

#### Cross-Platform (Shell Script)  
```bash
# Make executable and run
chmod +x run-tests.sh
./run-tests.sh
```

#### Manual .NET CLI
```bash
# Build and run tests
dotnet build Tests/ClickIt.Tests.csproj --configuration Debug
dotnet test Tests/ClickIt.Tests.csproj --verbosity normal --collect:"XPlat Code Coverage"
```

### Automated Execution

Tests automatically run:
- **Post-Build**: After successful Debug builds via MSBuild target
- **CI/CD Pipeline**: On GitHub Actions for all pushes/PRs
- **Pre-Deployment**: Before plugin packaging

## 📁 Test Project Structure

```
Tests/
├── ClickIt.Tests.csproj          # Test project configuration
├── WeightCalculationTests.cs     # Core calculation logic tests  
├── SettingsTests.cs              # Settings and configuration tests
├── AltarDecisionLogicTests.cs    # Decision making algorithm tests
├── AltarModsConstantsTests.cs    # Data validation tests
└── IntegrationTests.cs           # End-to-end workflow tests
```

## ⚙️ Test Configuration

### Test Frameworks Used
- **MSTest**: Primary test framework
- **FluentAssertions**: Readable assertions and better error messages
- **Moq**: Mocking framework for dependencies
- **Coverlet**: Code coverage collection

### Coverage Configuration (`runsettings.xml`)
- **Formats**: Cobertura and OpenCover
- **Exclusions**: Test assemblies, external dependencies, generated code
- **Parallel Execution**: Enabled for faster test runs

## 🔧 Continuous Integration

### GitHub Actions Workflow (`.github/workflows/ci-cd.yml`)

**Build & Test Job:**
- Builds solution on Windows runner
- Executes full test suite
- Collects code coverage
- Uploads test results and coverage reports

**Static Analysis Job:**
- Runs additional code analysis
- Publishes build artifacts
- Validates code quality metrics

### Test Results & Reporting

Test results are automatically:
- Published to GitHub Actions summary
- Uploaded as workflow artifacts
- Sent to Codecov for coverage tracking
- Available in TRX format for detailed analysis

## 📈 Quality Metrics

### Test Coverage Goals
- **Minimum Target**: 80% code coverage
- **Critical Path Coverage**: 95%+ for altar decision logic
- **Edge Case Coverage**: All error conditions and null/empty inputs

### Quality Gates
- All tests must pass before merge
- No decrease in code coverage
- Static analysis warnings addressed
- Performance regression detection

## 🛠️ Adding New Tests

### Test Naming Convention
```csharp
[TestMethod]
public void MethodName_WithSpecificCondition_ExpectedBehavior()
{
    // Arrange
    // Act  
    // Assert
}
```

### Example Test Structure
```csharp
[TestMethod]
public void CalculateUpsideWeight_WithValidMods_ReturnsCorrectSum()
{
    // Arrange
    var mods = new List<string> { "mod1", "mod2" };
    var expected = 150m;

    // Act
    var result = _testObject.CalculateUpsideWeight(mods);

    // Assert
    result.Should().Be(expected);
}
```

## 🐛 Troubleshooting

### Common Issues

**Test Project Build Errors:**
- Ensure ExileCore.dll path is correct in test project
- Verify all NuGet packages are restored
- Check .NET Framework version compatibility

**Test Execution Failures:**
- Confirm test dependencies are available
- Validate mock object setup
- Check for changed default mod weights

**Coverage Collection Issues:**
- Ensure Coverlet collector is installed
- Verify exclusion patterns in runsettings.xml
- Check file paths for cross-platform compatibility

## 📋 Test Checklist

Before adding new functionality:
- [ ] Write failing tests first (TDD approach)
- [ ] Implement functionality to pass tests
- [ ] Add edge case and error condition tests
- [ ] Verify integration test coverage
- [ ] Update test documentation if needed
- [ ] Ensure tests pass in CI/CD pipeline

## 🎯 Benefits

This comprehensive testing framework provides:

✅ **Early Bug Detection**: Issues caught during development  
✅ **Regression Prevention**: Changes don't break existing functionality  
✅ **Refactoring Confidence**: Safe code improvements  
✅ **Documentation**: Tests serve as living documentation  
✅ **Quality Assurance**: Consistent behavior across environments  
✅ **Automated Validation**: No manual testing overhead  

---

*For questions or issues with the testing framework, please create an issue on the GitHub repository.*