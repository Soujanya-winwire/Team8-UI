# Agentic AI - Web-Based Test Management Platform

## ?? Overview

A comprehensive web-based platform for creating, managing, and executing automated tests without writing code. Built with ASP.NET Core backend and modern JavaScript frontend.

## ? Features

### 1. **Zero-Code Test Creation**
- Visual test designer
- Drag-and-drop interface
- No programming required
- JSON-based test scenarios

### 2. **Real-Time Test Execution**
- Execute tests directly from the UI
- Live console output
- Real-time progress tracking
- SignalR for instant updates

### 3. **Test Management**
- View all test scenarios
- Filter by module or tag
- Search functionality
- Edit and delete tests

### 4. **Configuration Management**
- Change browser settings
- Toggle headless mode
- Adjust timeouts and retries
- Enable/disable features

### 5. **Dashboard**
- View test statistics
- Recent scenarios
- Quick actions
- Module overview

## ?? Getting Started

### Prerequisites
- .NET 9 SDK
- Chrome/Firefox/Edge browser
- Playwright or Selenium drivers (installed automatically)

### Running the Platform

#### Option 1: Using PowerShell Script
```powershell
.\LaunchWebUI.ps1
```

#### Option 2: Using .NET CLI
```bash
cd tools/AgenticAI.WebUI
dotnet run
```

#### Option 3: Using Visual Studio
1. Open `AgenticAI.sln`
2. Set `AgenticAI.WebUI` as startup project
3. Press F5

### Access the Platform
Once running, open your browser to:
- **Web UI**: http://localhost:5000
- **API Documentation**: http://localhost:5000/swagger
- **SignalR Hub**: ws://localhost:5000/testExecutionHub

## ?? User Guide

### Creating a Test Scenario

1. Navigate to **Create Test** from the sidebar
2. Fill in scenario details:
   - **Name**: Unique identifier for your test
   - **Module**: Logical grouping (e.g., "Authentication")
   - **Description**: What the test does
   - **Start URL**: Application URL
   - **Tags**: For filtering (e.g., "smoke", "regression")

3. Add **Actions**:
   - **Click**: Click on an element
   - **Type**: Enter text into a field
   - **Navigate**: Go to a URL
   - **Wait**: Wait for specified time
   - **WaitForElement**: Wait for element to appear

4. Add **Assertions**:
   - **ElementVisible**: Verify element is visible
   - **TextEquals**: Check exact text match
   - **TextContains**: Check if text contains value
   - **UrlContains**: Verify URL contains text

5. Click **Save Scenario**

### Executing Tests

#### Execute Single Test
1. Go to **Execute Tests**
2. Select **Module** and **Scenario**
3. Click **Execute Scenario**
4. Watch real-time output in console

#### Execute Module Tests
1. Select a **Module**
2. Click **Execute Module**
3. All tests in that module will run

#### Execute Tagged Tests
1. Select a **Tag** (e.g., "smoke")
2. Click **Execute Tagged Tests**
3. All tests with that tag will run

### Viewing Results
- Real-time console output shows execution progress
- Status updates appear instantly
- Success/failure indicated with colors
- Detailed test results displayed after execution

### Configuration
1. Navigate to **Configuration**
2. Adjust settings:
   - **Browser**: Chrome, Firefox, Edge, Safari
   - **Headless**: Enable for faster execution
   - **Environment**: Dev, QA, Staging, Prod
   - **Execution Mode**: Sequential or Parallel
   - **Features**: Video, Screenshots, Self-Healing
3. Click **Save Changes**

## ?? API Endpoints

### Scenarios
```http
GET    /api/scenarios                    - Get all scenarios
GET    /api/scenarios/module/{module}    - Get scenarios by module
GET    /api/scenarios/tag/{tag}          - Get scenarios by tag
GET    /api/scenarios/{module}/{name}    - Get specific scenario
POST   /api/scenarios                    - Create scenario
PUT    /api/scenarios/{module}/{name}    - Update scenario
DELETE /api/scenarios/{module}/{name}    - Delete scenario

POST   /api/scenarios/execute/{module}/{name}  - Execute single scenario
POST   /api/scenarios/execute/module/{module}  - Execute module
POST   /api/scenarios/execute/tag/{tag}        - Execute by tag
POST   /api/scenarios/execute/all              - Execute all scenarios

GET    /api/scenarios/modules            - Get available modules
GET    /api/scenarios/tags               - Get available tags
```

### Configuration
```http
GET    /api/configuration                      - Get current configuration
PUT    /api/configuration                      - Update configuration
GET    /api/configuration/environment/{env}    - Get environment config
PUT    /api/configuration/environment/{env}    - Update environment config

GET    /api/configuration/browsers             - Get available browsers
GET    /api/configuration/environments         - Get available environments
GET    /api/configuration/execution-modes      - Get execution modes
```

## ?? Project Structure

```
tools/AgenticAI.WebUI/
??? Controllers/
?   ??? ScenariosController.cs       # Test scenarios API
?   ??? ConfigurationController.cs   # Configuration API
??? Hubs/
?   ??? TestExecutionHub.cs          # SignalR real-time updates
??? wwwroot/
?   ??? index.html                   # Main UI
?   ??? app.js                       # JavaScript logic
??? Program.cs                       # Application entry point
??? AgenticAI.WebUI.csproj          # Project file
```

## ?? UI Components

### Dashboard
- Test statistics (total, passed, failed)
- Recent scenarios
- Quick action buttons
- Module count

### Scenarios View
- List all test scenarios
- Filter by module or tag
- Search functionality
- Execute, view, delete actions

### Create Test View
- Form for scenario details
- Action builder
- Assertion builder
- Save functionality

### Execute Tests View
- Execute single scenario
- Execute by module
- Execute by tag
- Real-time console output
- Progress tracking

### Configuration View
- Framework settings
- Browser selection
- Feature toggles
- Environment settings

### Documentation View
- Getting started guide
- Feature overview
- Quick start instructions

## ?? Real-Time Features

### SignalR Integration
The platform uses SignalR for real-time communication:

- **Test Updates**: Live status updates during execution
- **Progress Tracking**: Real-time progress bar
- **Console Output**: Instant log messages
- **Results**: Immediate result display

### Events
- `ReceiveTestUpdate`: Test status changes
- `ReceiveTestProgress`: Execution progress
- `ReceiveTestResult`: Test completion results
- `Connected`: Connection established

## ?? Use Cases

### 1. QA Team
- Create tests without coding
- Execute regression suites
- Monitor test health
- Generate reports

### 2. Developers
- Quick smoke tests
- Integration testing
- Debug failing tests
- Configuration management

### 3. Test Automation Engineers
- Rapid test creation
- Test maintenance
- Execution monitoring
- Result analysis

## ??? Advanced Features

### Parallel Execution
Set `ExecutionMode` to `Parallel` in configuration to run multiple tests simultaneously.

### Self-Healing
Enable `Self-Healing` to automatically fix broken locators during execution.

### Video Recording
Enable `Video Recording` to capture test execution for debugging.

### Cross-Browser Testing
Easily switch browsers in configuration without code changes.

## ?? Best Practices

1. **Organize by Modules**: Group related tests together
2. **Use Meaningful Names**: Clear scenario and step names
3. **Add Tags**: Enable flexible test execution
4. **Regular Execution**: Run tests frequently
5. **Monitor Results**: Review failures promptly

## ?? Troubleshooting

### Server Won't Start
```bash
# Check if port 5000 is in use
netstat -ano | findstr :5000

# Try a different port
dotnet run --urls "http://localhost:5001"
```

### Tests Not Executing
1. Check configuration is correct
2. Verify scenario JSON files exist
3. Check console for errors
4. Ensure drivers are installed

### SignalR Not Connecting
1. Check browser console for errors
2. Verify server is running
3. Check firewall settings
4. Try refreshing the page

## ?? Security Notes

- The platform is intended for development/testing environments
- No authentication is implemented (add as needed)
- Be cautious when exposing to networks
- Sensitive data should be stored securely

## ?? Deployment

### Local Development
Already configured - just run the application

### CI/CD Integration
```yaml
# Example GitHub Actions
- name: Start Web UI
  run: |
    cd tools/AgenticAI.WebUI
    dotnet run &
    sleep 10

- name: Run Tests via API
  run: |
    curl -X POST http://localhost:5000/api/scenarios/execute/tag/smoke
```

### Docker (Optional)
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY . .
EXPOSE 5000
ENTRYPOINT ["dotnet", "AgenticAI.WebUI.dll"]
```

## ?? Roadmap

- [ ] Test result history
- [ ] Analytics dashboard
- [ ] Scheduled test execution
- [ ] Email notifications
- [ ] Multiple report formats
- [ ] Test data management
- [ ] API testing UI
- [ ] Performance testing

## ?? Contributing

Feel free to enhance the platform:
1. Add new features
2. Improve UI/UX
3. Fix bugs
4. Add documentation

## ?? License

Part of the Agentic AI Automation Framework

## ?? Support

For issues or questions:
1. Check the documentation
2. Review console logs
3. Check API responses
4. Contact the development team

---

**Built with ?? using ASP.NET Core, SignalR, and Modern JavaScript**
