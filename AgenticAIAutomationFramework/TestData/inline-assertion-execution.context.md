# Context File: Inline Assertion Execution in Test Scenarios
Project: Custom / Agentic AI Test Automation Framework
Module: Test Scenarios & Execution Engine

## 1. Background
The framework provides a Test Scenarios section where test cases are defined
as an ordered list of executable steps. Users can:
- Add steps at any position
- Delete steps
- Insert assertions at any position between steps

Steps are executed sequentially by the execution engine.

## 2. Current Problem
Assertions added at a specific step index are executed at the end of the test
instead of at the position where they are inserted.

## 3. Expected Behavior
Assertions must execute inline at their step position and be treated as
first-class executable steps.

## 4. Unified Step Model
Actions and assertions must exist in a single ordered list with type metadata.

## 5. Execution Engine Requirement
Execution must iterate over ordered steps and execute based on step type.

## 6. Insertion Rules
Insertion must update order and persist execution sequence.

## 7. Backward Compatibility
Existing tests must remain functional.

## 8. Design Principle
Assertions are part of execution flow, not post-processing.
