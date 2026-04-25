# AgenticAI Automation Framework

An enterprise-grade, AI-powered test automation framework built with .NET and Playwright that provides zero-code test creation, intelligent test recording, and comprehensive UI/API testing capabilities.

## 🚀 Features

### Core Capabilities
- **Zero-Code Test Designer**: Visual test creation with drag-and-drop interface
- **Enhanced Test Recorder**: Automatically record user interactions and generate test scenarios
- **Data-Driven Testing**: Support for CSV, JSON, and Excel test data
- **UI & API Automation**: Unified framework for both UI and API testing
- **Multi-Environment Support**: Dev, QA, and Production configurations
- **Evidence Capture**: Automatic screenshots, videos, and network logs
- **IFrame Management**: Intelligent iframe context handling
- **Self-Healing Tests**: AI-driven test stability and recovery

### Advanced Features
- **Visual Validation**: Screenshot comparison and visual regression testing
- **Network Interception**: Monitor and validate API calls during UI tests
- **Parallel Execution**: Run tests concurrently for faster feedback
- **CI/CD Integration**: Ready for Azure DevOps, GitHub Actions, and Jenkins
- **Comprehensive Reporting**: Detailed test reports with evidence attachments
- **Scenario Reordering**: Visual drag-and-drop test step management

## 📋 Prerequisites

- **.NET 8.0 SDK** or later
- **Node.js 18+** (for Playwright)
- **PowerShell 5.1+** (Windows) or PowerShell Core 7+ (cross-platform)
- **Visual Studio 2022** or **VS Code** (recommended)

## ⚙️ Quick Start

### 1. Initial Setup

Run the complete environment setup script:

```powershell
.\Setup-Complete-Environment.ps1
```

This will:
- Install Playwright browsers
- Restore NuGet packages
- Build the solution
- Set up test data directories
- Configure environments

### 2. Launch the Web UI

Start the Zero-Code Test Designer interface:

```powershell
.\LaunchWebUI.ps1
```

The Web UI will be available at `http://localhost:5000`

### 3. Create Your First Test

**Option A: Using the Visual Designer**
1. Open the Web UI (http://localhost:5000)
2. Click "Create New Scenario"
3. Add test steps visually
4. Save your scenario

**Option B: Using the Test Recorder**
```powershell
.\LaunchZeroCodeTesting.ps1
```

Follow the prompts to record interactions automatically.

### 4. Run Tests

```powershell
# Run all tests
dotnet test

# Run specific test module
dotnet test --filter "Category=Authentication"

# Run with data-driven scenarios
.\test-datadriven.ps1
```

## 📁 Project Structure

```
AgenticAIAutomationFramework/
│
├── src/                              # Source code
│   ├── AgenticAI.Core/              # Core framework components
│   ├── AgenticAI.UIAutomation/      # UI testing with Playwright
│   └── AgenticAI.APIAutomation/     # API testing capabilities
│
├── tools/                            # Framework tools
│   ├── AgenticAI.WebUI/             # Zero-Code Test Designer (Web UI)
│   ├── TestRecorder/                # Enhanced test recorder
│   └── VisualDesigner/              # Visual test design tools
│
├── tests/                            # Test projects
│   └── AgenticAI.Tests/             # Unit and integration tests
│
├── TestScenarios/                    # Test scenario files (JSON)
│   ├── Authentication/
│   ├── Datadriven/
│   ├── Login/
│   └── [Other modules]/
│
├── TestData/                         # Test data files
│   ├── SampleLogin.csv
│   ├── SampleUsers.json
│   └── Test_Form_01_Data.json
│
├── TestDesigner/                     # Zero-Code Designer UI
│   ├── index.html
│   └── Templates/
│
├── Configuration/                    # Environment configurations
│   └── Environments/
│
└── docs/                            # Documentation
    ├── DOCUMENTATION_INDEX.md
    ├── Enhanced_Recorder_Architecture.md
    ├── Evidence_Capture_Enhancement.md
    ├── IFrame_Quick_Start.md
    └── [Other guides]/
```

## 🛠️ Key Components

### 1. AgenticAI.Core
Core framework functionality including:
- Browser management
- Test lifecycle management
- Configuration handling
- Logging and reporting
- Evidence capture

### 2. AgenticAI.UIAutomation
UI test automation powered by Playwright:
- Page object models
- Element interaction utilities
- Wait strategies
- IFrame context management
- Visual validation

### 3. AgenticAI.APIAutomation
REST API testing capabilities:
- HTTP client wrapper
- Request/response validation
- Authentication handling
- API test data management

### 4. AgenticAI.WebUI
Zero-Code Test Designer web interface:
- Visual test creation
- Scenario management
- Step reordering (drag & drop)
- Configuration management
- Test execution dashboard

### 5. TestRecorder
Enhanced recording tool:
- Automatic interaction capture
- Locator generation
- Iframe detection
- Evidence collection
- Scenario export

## 📖 Usage Examples

### Creating a Data-Driven Test

1. Create test data file (`TestData/users.json`):
```json
[
  {
    "username": "user1@example.com",
    "password": "Test@123",
    "expectedResult": "Success"
  },
  {
    "username": "user2@example.com",
    "password": "Test@456",
    "expectedResult": "Success"
  }
]
```

2. Create scenario using Web UI or manually
3. Run the data-driven test:
```powershell
.\test-datadriven.ps1 -ScenarioName "LoginTest" -DataFile "users.json"
```

### Recording a Test Scenario

```powershell
# Start recorder
.\LaunchZeroCodeTesting.ps1

# Follow prompts:
# 1. Enter scenario name
# 2. Enter starting URL
# 3. Perform actions in browser
# 4. Recorder captures all interactions
# 5. Scenario saved to TestScenarios/
```

### Programmatic Test Execution

```csharp
using AgenticAI.UIAutomation;

// Initialize test runner
var runner = new ZeroCodeTestRunner();

// Execute single scenario
await runner.ExecuteScenarioAsync("Login Test", "Authentication");

// Execute entire module
await runner.ExecuteModuleAsync("Authentication");

// Execute by tag
await runner.ExecuteByTagAsync("smoke");
```

## 🎯 Testing Workflows

### Standard Testing Flow
1. **Design** → Use Web UI to create test scenarios
2. **Data** → Add test data in TestData/ directory
3. **Execute** → Run tests via CLI or CI/CD pipeline
4. **Review** → Check reports and evidence

### Recording-First Flow
1. **Record** → Use test recorder on live application
2. **Review** → Verify generated scenario in Web UI
3. **Enhance** → Add assertions and data bindings
4. **Execute** → Run and validate tests

## 📚 Documentation

Comprehensive documentation is available in the `docs/` directory:

- **[Documentation Index](docs/DOCUMENTATION_INDEX.md)** - Start here for all docs
- **[Enhanced Recorder Guide](docs/Enhanced_Recorder_Architecture.md)** - Test recording details
- **[Evidence Capture](docs/Evidence_Capture_Enhancement.md)** - Screenshots and logs
- **[IFrame Quick Start](docs/IFrame_Quick_Start.md)** - Working with iframes
- **[Data-Driven Testing](TestData/QUICKSTART_DataDriven.md)** - Data-driven test guide

## 🔧 Utility Scripts

### Setup & Installation
- `Setup-Complete-Environment.ps1` - Complete environment setup
- `InstallPlaywrightBrowsers.ps1` - Install Playwright browsers

### Launching Tools
- `LaunchWebUI.ps1` - Start the Web UI Designer
- `LaunchZeroCodeTesting.ps1` - Start test recorder

### Testing
- `test-datadriven.ps1` - Run data-driven tests
- `Validate-EnhancedRecorder.ps1` - Validate recorder functionality

### Maintenance
- `Cleanup-Project.ps1` - Clean build artifacts
- `GenerateExcelTestData.ps1` - Generate test data templates

## 🏗️ Architecture Highlights

### Design Principles
- **Scalable**: Support for thousands of test cases with parallel execution
- **Maintainable**: Clean architecture with separation of concerns
- **Observable**: Deep visibility into test execution and failures
- **Intelligent**: AI-driven test stability and self-healing capabilities
- **Extensible**: Plugin architecture for custom actions and validators

### Technology Stack
- **.NET 8.0** - Framework platform
- **Playwright** - Browser automation
- **xUnit** - Test framework
- **Serilog** - Logging
- **ASP.NET Core** - Web UI backend
- **JavaScript/HTML5** - Web UI frontend

## 🤝 Contributing

1. Create a feature branch
2. Make your changes
3. Add tests for new functionality
4. Update documentation
5. Submit a pull request

## 📝 Configuration

Environment configurations are stored in `Configuration/Environments/`:

```json
{
  "environment": "QA",
  "baseUrl": "https://qa.example.com",
  "apiUrl": "https://api-qa.example.com",
  "browser": "chromium",
  "headless": false,
  "timeout": 30000
}
```

## 🐛 Troubleshooting

### Playwright browsers not installed
```powershell
.\InstallPlaywrightBrowsers.ps1
```

### Build errors
```powershell
dotnet clean
dotnet restore
dotnet build
```

### Web UI not starting
```powershell
# Check port availability (5000/5001)
# Kill existing processes if needed
netstat -ano | findstr :5000
```

## 📄 License

Copyright © 2026 WinWire Technologies. All rights reserved.

## 🔗 Related Resources

- [Playwright Documentation](https://playwright.dev/dotnet/)
- [.NET Testing Guide](https://docs.microsoft.com/en-us/dotnet/core/testing/)
- [Project Documentation Index](docs/DOCUMENTATION_INDEX.md)

---

**Version**: 1.0.0  
**Last Updated**: April 2026  
**Maintained by**: WinWire Team 8
