# AI Context Template - Agentic AI Automation Framework

This file is a machine- and AI-friendly context descriptor to help code-generation tools produce consistent tests, page objects and utilities.

- projectName: Agentic AI Automation Framework
- language: C# (.NET 9)
- codingStyle: "PascalCase for types and methods, camelCase for locals, async suffix for async methods"
- testFolderStructure:
  - Tests/
  - Tests/<suite>/Scenarios/
  - Tests/<suite>/Data/
  - Tests/<suite>/PageObjects/
  - Tests/<suite>/Reports/
- pageObjectPattern: "Class per page, methods for actions, properties for locators. Use `IWebDriver` abstraction from `AgenticAI.Core.Interfaces`"
- locatorsRepo: "HtmlSnapshots/" # folder with stored page HTML snapshots
- assertionGuidelines:
  - use AssertionHelper for soft assertions
  - use hard assert for critical validations
- retryPolicy: "Use RetryHelper.ExecuteWithRetryAsync for transient actions"
- codeGenPrompts:
  - generateTestSkeleton: |
      "Create a new TestScenario class for <feature> using PageObjects. Include setup/teardown and sample assertions. Use RetryHelper for flaky operations."
  - locatorGeneration: |
      "Given the HTML snapshot file <file>, generate robust CSS/XPath selectors and fallback locators."

