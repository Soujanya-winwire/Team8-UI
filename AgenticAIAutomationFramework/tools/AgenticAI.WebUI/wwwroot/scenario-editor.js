// Scenario Editor - Editable Modal for Test Scenarios
// This file contains functions for viewing and editing test scenarios

// Global variable to store current editing scenario
let currentEditingScenario = null;

/**
 * View and edit a test scenario
 * @param {string} module - The module name
 * @param {string} name - The scenario name
 */
async function viewScenario(module, name) {
    try {
        const response = await fetch(`${API_BASE_URL}/scenarios/${module}/${name}`);
        const data = await response.json();

        if (data.success) {
            const scenario = data.scenario;

            // CRITICAL FIX: Convert backend executeAfterActionIndex to frontend afterActionIndex
            // This ensures assertions display correctly in the UI
            const assertions = (scenario.assertions || []).map(a => {
                const assertionWithIndex = {
                    type: a.type,
                    locator: a.locator,
                    expectedValue: a.expectedValue,
                    description: a.description
                };

                // Convert executeAfterActionIndex to afterActionIndex for frontend
                // This is CRITICAL for rendering assertions in correct position
                if (a.executeAfterActionIndex !== undefined && a.executeAfterActionIndex !== null) {
                    assertionWithIndex.afterActionIndex = a.executeAfterActionIndex;
                    console.log(`? Assertion mapped: type=${a.type}, afterActionIndex=${a.executeAfterActionIndex}, locator=${a.locator}`);
                } else {
                    console.log(`?? Assertion without index: type=${a.type}, locator=${a.locator} (will show at end)`);
                }

                return assertionWithIndex;
            });

            console.log('?? Total assertions loaded:', assertions.length);
            console.log('?? Assertions with positions:', assertions.filter(a => a.afterActionIndex !== undefined).length);
            console.log('?? Assertions without positions:', assertions.filter(a => a.afterActionIndex === undefined).length);

            currentEditingScenario = {
                module: module,
                name: name,
                description: scenario.description || '',
                startUrl: scenario.startUrl || '',
                tags: scenario.tags || [],
                actions: JSON.parse(JSON.stringify(scenario.actions || [])), // Deep copy
                assertions: assertions
            };

            console.log('?? Current editing scenario:', currentEditingScenario);
            console.log('?? Actions count:', currentEditingScenario.actions.length);
            console.log('?? Assertions count:', currentEditingScenario.assertions.length);

            renderScenarioModal();
        }
    } catch (error) {
        console.error('Error loading scenario:', error);
        showError('Failed to load scenario details: ' + error.message);
    }
}

/**
 * Render the editable scenario modal
 */
function renderScenarioModal() {
    const scenario = currentEditingScenario;

    showModal('Scenario Details', `
        <div style="max-height: 70vh; overflow-y: auto; padding-right: 10px;">
            <!-- Basic Info - Read Only Display -->
            <div style="margin-bottom: 20px; padding: 15px; background: #f9fafb; border-radius: 8px;">
                <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 10px; margin-bottom: 10px;">
                    <div>
                        <strong style="color: #6b7280;">Name:</strong> 
                        <span style="color: #1f2937;">${escapeHtml(scenario.name)}</span>
                    </div>
                    <div>
                        <strong style="color: #6b7280;">Module:</strong> 
                        <span class="badge badge-primary">${escapeHtml(scenario.module)}</span>
                    </div>
                </div>
                <div style="margin-bottom: 10px;">
                    <strong style="color: #6b7280;">Description:</strong>
                    <div style="color: #1f2937; margin-top: 5px;">${escapeHtml(scenario.description) || 'N/A'}</div>
                </div>
                <div style="margin-bottom: 10px;">
                    <strong style="color: #6b7280;">Start URL:</strong>
                    <div style="color: #1f2937; margin-top: 5px;">${escapeHtml(scenario.startUrl)}</div>
                </div>
                <div>
                    <strong style="color: #6b7280;">Tags:</strong>
                    <div style="margin-top: 5px;">
                        ${scenario.tags.map(tag => `<span class="badge badge-info">${escapeHtml(tag)}</span>`).join(' ') || '<span style="color: #9ca3af;">No tags</span>'}
                    </div>
                </div>
            </div>

            <!-- Test Steps Section -->
            <div style="margin-bottom: 20px;">
                <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 15px; padding-bottom: 10px; border-bottom: 2px solid #e5e7eb;">
                    <h4 style="margin: 0; color: #1f2937;">
                        <i class="fas fa-list-ol"></i> Test Steps
                    </h4>
                </div>
                <div id="steps-list-container">
                    ${renderStepsList()}
                </div>
            </div>
        </div>

        <!-- Action Buttons -->
        <div style="display: flex; justify-content: space-between; align-items: center; margin-top: 20px; padding-top: 15px; border-top: 2px solid #e5e7eb;">
            <button class="btn btn-success" onclick="executeScenarioFromModal()">
                <i class="fas fa-play"></i> Execute Now
            </button>
            <div style="display: flex; gap: 10px;">
                <button class="btn btn-secondary" onclick="closeModal()">
                    <i class="fas fa-times"></i> Cancel
                </button>
                <button class="btn btn-primary" onclick="saveScenarioChanges()">
                    <i class="fas fa-save"></i> Save Changes
                </button>
            </div>
        </div>
    `);
}

/**
 * Render the list of test steps (actions + assertions combined)
 */
function renderStepsList() {
    const actions = currentEditingScenario.actions;
    const assertions = currentEditingScenario.assertions;

    if (actions.length === 0 && assertions.length === 0) {
        return `
            <div style="text-align: center; padding: 40px; background: #f9fafb; border-radius: 8px; border: 2px dashed #d1d5db;">
                <i class="fas fa-inbox" style="font-size: 3em; color: #9ca3af; margin-bottom: 15px;"></i>
                <p style="color: #6b7280; font-size: 16px; margin-bottom: 15px;">No test steps yet</p>
                <button class="btn btn-primary" onclick="addNewStep(0, 'action')">
                    <i class="fas fa-plus"></i> Add First Step
                </button>
            </div>
        `;
    }

    let html = '<div style="display: flex; flex-direction: column; gap: 8px;">';

    // Render all actions (they maintain their original order)
    actions.forEach((action, actionIndex) => {
        html += renderStepItem(action, actionIndex, 'action');
    });

    html += '</div>';
    return html;
}

/**
 * Render a single step item (action or assertion)
 */
function renderStepItem(step, index, type) {
    const isAction = type === 'action';
    const stepNumber = index + 1;
    const bgColor = isAction ? '#ffffff' : '#f0fdf4';
    const borderColor = isAction ? '#e5e7eb' : '#86efac';
    const iconColor = isAction ? '#3b82f6' : '#10b981';
    const iconBg = isAction ? '#eff6ff' : '#d1fae5';

    // Get step type and details
    const stepType = isAction ? step.actionType : step.type;
    const locator = step.locator || '';
    const value = step.value || step.expectedValue || '';

    // Build step description
    let stepDescription = '';
    if (isAction) {
        switch (step.actionType) {
            case 'Click':
                stepDescription = `Click on element: <code style="background: #f3f4f6; padding: 2px 6px; border-radius: 3px; font-size: 12px;">${escapeHtml(locator)}</code>`;
                break;
            case 'Type':
                stepDescription = `Type "${escapeHtml(value)}" into: <code style="background: #f3f4f6; padding: 2px 6px; border-radius: 3px; font-size: 12px;">${escapeHtml(locator)}</code>`;
                break;
            case 'Navigate':
                stepDescription = `Navigate to: <code style="background: #f3f4f6; padding: 2px 6px; border-radius: 3px; font-size: 12px;">${escapeHtml(locator)}</code>`;
                break;
            case 'Wait':
                stepDescription = `Wait for: <code style="background: #f3f4f6; padding: 2px 6px; border-radius: 3px; font-size: 12px;">${escapeHtml(locator)}</code>`;
                break;
            case 'Select':
                stepDescription = `Select "${escapeHtml(value)}" from: <code style="background: #f3f4f6; padding: 2px 6px; border-radius: 3px; font-size: 12px;">${escapeHtml(locator)}</code>`;
                break;
            default:
                stepDescription = `${stepType}: <code style="background: #f3f4f6; padding: 2px 6px; border-radius: 3px; font-size: 12px;">${escapeHtml(locator)}</code>`;
        }
    } else {
        switch (step.type) {
            case 'ElementVisible':
                stepDescription = `? Assert element visible: <code style="background: #f3f4f6; padding: 2px 6px; border-radius: 3px; font-size: 12px;">${escapeHtml(locator)}</code>`;
                break;
            case 'TextEquals':
                stepDescription = `? Assert text equals "${escapeHtml(value)}": <code style="background: #f3f4f6; padding: 2px 6px; border-radius: 3px; font-size: 12px;">${escapeHtml(locator)}</code>`;
                break;
            case 'TextContains':
                stepDescription = `? Assert text contains "${escapeHtml(value)}": <code style="background: #f3f4f6; padding: 2px 6px; border-radius: 3px; font-size: 12px;">${escapeHtml(locator)}</code>`;
                break;
            case 'UrlContains':
                stepDescription = `? Assert URL contains: <code style="background: #f3f4f6; padding: 2px 6px; border-radius: 3px; font-size: 12px;">${escapeHtml(locator)}</code>`;
                break;
            case 'ElementExists':
                stepDescription = `? Assert element exists: <code style="background: #f3f4f6; padding: 2px 6px; border-radius: 3px; font-size: 12px;">${escapeHtml(locator)}</code>`;
                break;
            default:
                stepDescription = `? ${stepType}: <code style="background: #f3f4f6; padding: 2px 6px; border-radius: 3px; font-size: 12px;">${escapeHtml(locator)}</code>`;
        }
    }

    return `
        <div style="position: relative; background: ${bgColor}; border: 1px solid ${borderColor}; border-radius: 8px; padding: 12px; padding-left: 50px;">
            <!-- Step Number -->
            <div style="position: absolute; left: 12px; top: 50%; transform: translateY(-50%); background: ${iconBg}; color: ${iconColor}; width: 28px; height: 28px; border-radius: 50%; display: flex; align-items: center; justify-content: center; font-weight: bold; font-size: 12px; box-shadow: 0 1px 3px rgba(0,0,0,0.1);">
                ${stepNumber}
            </div>
            
            <!-- Step Content -->
            <div style="display: flex; justify-content: space-between; align-items: center;">
                <div style="flex: 1; min-width: 0;">
                    <div style="font-size: 13px; color: #374151; line-height: 1.6; word-break: break-word;">
                        ${stepDescription}
                    </div>
                </div>
                
                <!-- Action Buttons -->
                <div style="display: flex; gap: 4px; margin-left: 12px; flex-shrink: 0;">
                    <!-- Edit Button -->
                    <button class="btn btn-sm" onclick="editStep(${index}, '${type}')" 
                            title="Edit step"
                            style="padding: 4px 8px; background: #3b82f6; color: white; border: none; border-radius: 4px; cursor: pointer; font-size: 11px;">
                        <i class="fas fa-edit"></i>
                    </button>
                    
                    <!-- Add Assertion Button (only for actions) -->
                    ${isAction ? `
                        <button class="btn btn-sm" onclick="addAssertionAfterStep(${index})" 
                                title="Add assertion after this step"
                                style="padding: 4px 8px; background: #10b981; color: white; border: none; border-radius: 4px; cursor: pointer; font-size: 11px;">
                            <i class="fas fa-check"></i>
                        </button>
                    ` : ''}
                    
                    <!-- Add Step Below Button -->
                    <button class="btn btn-sm" onclick="addNewStep(${index + 1}, 'action')" 
                            title="Add step below"
                            style="padding: 4px 8px; background: #6b7280; color: white; border: none; border-radius: 4px; cursor: pointer; font-size: 11px;">
                        <i class="fas fa-plus"></i>
                    </button>
                    
                    <!-- Delete Button -->
                    <button class="btn btn-sm" onclick="deleteStep(${index}, '${type}')" 
                            title="Delete step"
                            style="padding: 4px 8px; background: #ef4444; color: white; border: none; border-radius: 4px; cursor: pointer; font-size: 11px;">
                        <i class="fas fa-trash"></i>
                    </button>
                </div>
            </div>
            
            <!-- Associated Assertions (if any) -->
            ${isAction ? renderAssertionsForStep(index) : ''}
        </div>
    `;
}

/**
 * Render assertions associated with a specific action step
 */
function renderAssertionsForStep(actionIndex) {
    // Filter assertions that are specifically tied to this action
    // Only show assertions that have afterActionIndex explicitly set to this index
    const assertions = currentEditingScenario.assertions.filter(a =>
        a.afterActionIndex !== undefined &&
        a.afterActionIndex !== null &&
        a.afterActionIndex === actionIndex
    );

    console.log(`?? Rendering assertions for action index ${actionIndex}:`, assertions.length);
    console.log(`   Available assertions in scenario:`, currentEditingScenario.assertions.length);
    currentEditingScenario.assertions.forEach((a, idx) => {
        console.log(`   Assertion ${idx}: type=${a.type}, afterActionIndex=${a.afterActionIndex}, locator=${a.locator}`);
    });

    if (assertions.length === 0) {
        console.log(`   ? No assertions found for action index ${actionIndex}`);
        return '';
    }

    console.log(`   ? Found ${assertions.length} assertion(s) for action index ${actionIndex}`);

    let html = '<div style="margin-top: 8px; margin-left: 8px; padding-left: 20px; border-left: 3px solid #86efac;">';

    assertions.forEach((assertion, assertionIndex) => {
        const globalAssertionIndex = currentEditingScenario.assertions.indexOf(assertion);
        console.log(`      Rendering assertion ${assertionIndex}: type=${assertion.type}, globalIndex=${globalAssertionIndex}`);

        html += `
            <div style="background: #f0fdf4; border: 1px solid #86efac; border-radius: 6px; padding: 8px 12px; margin-bottom: 6px;">
                <div style="display: flex; justify-content: space-between; align-items: center;">
                    <div style="flex: 1; min-width: 0;">
                        <div style="font-size: 12px; color: #059669; line-height: 1.5;">
                            <i class="fas fa-check-circle" style="margin-right: 4px;"></i>
                            ${getAssertionDescription(assertion)}
                        </div>
                    </div>
                    <div style="display: flex; gap: 4px; margin-left: 8px; flex-shrink: 0;">
                        <button class="btn btn-sm" onclick="editAssertion(${globalAssertionIndex})" 
                                title="Edit assertion"
                                style="padding: 3px 6px; background: #10b981; color: white; border: none; border-radius: 3px; cursor: pointer; font-size: 10px;">
                            <i class="fas fa-edit"></i>
                        </button>
                        <button class="btn btn-sm" onclick="deleteAssertion(${globalAssertionIndex})" 
                                title="Delete assertion"
                                style="padding: 3px 6px; background: #ef4444; color: white; border: none; border-radius: 3px; cursor: pointer; font-size: 10px;">
                            <i class="fas fa-trash"></i>
                        </button>
                    </div>
                </div>
            </div>
        `;
    });

    html += '</div>';
    return html;
}

/**
 * Get assertion description text
 */
function getAssertionDescription(assertion) {
    const locator = assertion.locator || '';
    const value = assertion.expectedValue || '';

    switch (assertion.type) {
        case 'ElementVisible':
            return `Element visible: <code style="background: #dcfce7; padding: 1px 4px; border-radius: 2px; font-size: 11px;">${escapeHtml(locator)}</code>`;
        case 'TextEquals':
            return `Text equals "${escapeHtml(value)}": <code style="background: #dcfce7; padding: 1px 4px; border-radius: 2px; font-size: 11px;">${escapeHtml(locator)}</code>`;
        case 'TextContains':
            return `Text contains "${escapeHtml(value)}": <code style="background: #dcfce7; padding: 1px 4px; border-radius: 2px; font-size: 11px;">${escapeHtml(locator)}</code>`;
        case 'UrlContains':
            return `URL contains: <code style="background: #dcfce7; padding: 1px 4px; border-radius: 2px; font-size: 11px;">${escapeHtml(locator)}</code>`;
        case 'ElementExists':
            return `Element exists: <code style="background: #dcfce7; padding: 1px 4px; border-radius: 2px; font-size: 11px;">${escapeHtml(locator)}</code>`;
        default:
            return `${assertion.type}: <code style="background: #dcfce7; padding: 1px 4px; border-radius: 2px; font-size: 11px;">${escapeHtml(locator)}</code>`;
    }
}

/**
 * Add a new step at specific position
 */
function addNewStep(position, type) {
    // Create modal for adding step
    const isAction = type === 'action';
    const title = isAction ? 'Add Test Step' : 'Add Assertion';

    const modalContent = `
        <div style="padding: 20px;">
            <h4 style="margin-bottom: 20px; color: #1f2937;">
                <i class="fas fa-${isAction ? 'tasks' : 'check-circle'}"></i> ${title}
            </h4>
            
            <div class="form-group" style="margin-bottom: 15px;">
                <label style="display: block; margin-bottom: 8px; font-weight: 600; color: #374151;">
                    ${isAction ? 'Action' : 'Assertion'} Type
                </label>
                <select id="new-step-type" class="form-control" onchange="handleStepTypeChange(this.value, ${isAction})">
                    ${isAction ? `
                        <option value="Click">Click Element</option>
                        <option value="Type">Type Text</option>
                        <option value="Navigate">Navigate to URL</option>
                        <option value="Wait">Wait for Element</option>
                        <option value="Select">Select from Dropdown</option>
                    ` : `
                        <option value="ElementVisible">Element is Visible</option>
                        <option value="TextEquals">Text Equals</option>
                        <option value="TextContains">Text Contains</option>
                        <option value="UrlContains">URL Contains</option>
                        <option value="ElementExists">Element Exists</option>
                    `}
                </select>
            </div>
            
            <div class="form-group" style="margin-bottom: 15px;">
                <label style="display: block; margin-bottom: 8px; font-weight: 600; color: #374151;">
                    ${isAction ? 'Element Locator / URL' : 'Element Locator / URL Pattern'}
                </label>
                <input type="text" id="new-step-locator" class="form-control" 
                       placeholder="${isAction ? 'e.g., #username, .btn-login, //button[@id=\'submit\']' : 'e.g., #success-message, .alert'}"
                       style="font-family: monospace;">
                <small style="color: #6b7280; display: block; margin-top: 4px;">
                    Use CSS selector, XPath, or URL
                </small>
            </div>
            
            <div id="value-field-container" style="margin-bottom: 15px;">
                <!-- Value field will be inserted here dynamically -->
            </div>
            
            <div style="display: flex; justify-content: flex-end; gap: 10px; margin-top: 25px; padding-top: 15px; border-top: 1px solid #e5e7eb;">
                <button class="btn btn-secondary" onclick="closeModal()">
                    <i class="fas fa-times"></i> Cancel
                </button>
                <button class="btn btn-primary" onclick="saveNewStep(${position}, ${isAction})">
                    <i class="fas fa-plus"></i> Add Step
                </button>
            </div>
        </div>
    `;

    showModal(title, modalContent);

    // Initialize value field visibility
    handleStepTypeChange(isAction ? 'Click' : 'ElementVisible', isAction);
}

/**
 * Handle step type change to show/hide value field
 */
function handleStepTypeChange(stepType, isAction) {
    const container = document.getElementById('value-field-container');
    if (!container) return;

    const needsValue = isAction
        ? (stepType === 'Type' || stepType === 'Select')
        : (stepType === 'TextEquals' || stepType === 'TextContains');

    if (needsValue) {
        const label = isAction
            ? (stepType === 'Type' ? 'Text to Type' : 'Option to Select')
            : 'Expected Value';
        const placeholder = isAction
            ? (stepType === 'Type' ? 'Enter the text to type' : 'Select option text or value')
            : 'Enter expected text value';

        container.innerHTML = `
            <div class="form-group">
                <label style="display: block; margin-bottom: 8px; font-weight: 600; color: #374151;">
                    ${label}
                </label>
                <input type="text" id="new-step-value" class="form-control" 
                       placeholder="${placeholder}">
            </div>
        `;
    } else {
        container.innerHTML = '';
    }
}

/**
 * Save the new step
 */
function saveNewStep(position, isAction) {
    const stepType = document.getElementById('new-step-type').value;
    const locator = document.getElementById('new-step-locator').value.trim();
    const valueField = document.getElementById('new-step-value');
    const value = valueField ? valueField.value.trim() : '';

    // Validation
    if (!locator) {
        showError('Please enter a locator or URL');
        return;
    }

    if ((stepType === 'Type' || stepType === 'Select' || stepType === 'TextEquals' || stepType === 'TextContains') && !value) {
        showError('Please enter a value');
        return;
    }

    // Add the step
    if (isAction) {
        const newAction = {
            actionType: stepType,
            locator: locator,
            value: value
        };
        currentEditingScenario.actions.splice(position, 0, newAction);
    }

    closeModal();
    renderScenarioModal();
    showSuccess(`Step added successfully at position ${position + 1}`);
}

/**
 * Add assertion after a specific action step
 */
function addAssertionAfterStep(actionIndex) {
    const modalContent = `
        <div style="padding: 20px;">
            <h4 style="margin-bottom: 20px; color: #1f2937;">
                <i class="fas fa-check-circle" style="color: #10b981;"></i> Add Assertion After Step ${actionIndex + 1}
            </h4>
            
            <div class="form-group" style="margin-bottom: 15px;">
                <label style="display: block; margin-bottom: 8px; font-weight: 600; color: #374151;">
                    Assertion Type
                </label>
                <select id="new-assertion-type" class="form-control" onchange="handleStepTypeChange(this.value, false)">
                    <option value="ElementVisible">Element is Visible</option>
                    <option value="TextEquals">Text Equals</option>
                    <option value="TextContains">Text Contains</option>
                    <option value="UrlContains">URL Contains</option>
                    <option value="ElementExists">Element Exists</option>
                </select>
            </div>
            
            <div class="form-group" style="margin-bottom: 15px;">
                <label style="display: block; margin-bottom: 8px; font-weight: 600; color: #374151;">
                    Element Locator / URL Pattern
                </label>
                <input type="text" id="new-assertion-locator" class="form-control" 
                       placeholder="e.g., #success-message, .alert, /dashboard"
                       style="font-family: monospace;">
            </div>
            
            <div id="value-field-container">
                <!-- Value field will be inserted here dynamically -->
            </div>
            
            <div style="display: flex; justify-content: flex-end; gap: 10px; margin-top: 25px; padding-top: 15px; border-top: 1px solid #e5e7eb;">
                <button class="btn btn-secondary" onclick="closeModal()">
                    <i class="fas fa-times"></i> Cancel
                </button>
                <button class="btn btn-success" onclick="saveNewAssertion(${actionIndex})">
                    <i class="fas fa-check"></i> Add Assertion
                </button>
            </div>
        </div>
    `;

    showModal('Add Assertion', modalContent);
    handleStepTypeChange('ElementVisible', false);
}

/**
 * Save new assertion
 */
function saveNewAssertion(actionIndex) {
    const assertionType = document.getElementById('new-assertion-type').value;
    const locator = document.getElementById('new-assertion-locator').value.trim();
    const valueField = document.getElementById('new-step-value');
    const expectedValue = valueField ? valueField.value.trim() : '';

    // Validation
    if (!locator) {
        showError('Please enter a locator or URL pattern');
        return;
    }

    if ((assertionType === 'TextEquals' || assertionType === 'TextContains') && !expectedValue) {
        showError('Please enter an expected value');
        return;
    }

    // Add the assertion
    const newAssertion = {
        type: assertionType,
        locator: locator,
        expectedValue: expectedValue,
        afterActionIndex: actionIndex
    };

    currentEditingScenario.assertions.push(newAssertion);

    closeModal();
    renderScenarioModal();
    showSuccess('Assertion added successfully');
}

/**
 * Edit a step
 */
function editStep(index, type) {
    const isAction = type === 'action';
    const step = isAction ? currentEditingScenario.actions[index] : currentEditingScenario.assertions[index];
    const title = isAction ? `Edit Step ${index + 1}` : `Edit Assertion ${index + 1}`;

    const modalContent = `
        <div style="padding: 20px;">
            <h4 style="margin-bottom: 20px; color: #1f2937;">
                <i class="fas fa-edit"></i> ${title}
            </h4>
            
            <div class="form-group" style="margin-bottom: 15px;">
                <label style="display: block; margin-bottom: 8px; font-weight: 600; color: #374151;">
                    ${isAction ? 'Action' : 'Assertion'} Type
                </label>
                <select id="edit-step-type" class="form-control" onchange="handleStepTypeChange(this.value, ${isAction})">
                    ${isAction ? `
                        <option value="Click" ${step.actionType === 'Click' ? 'selected' : ''}>Click Element</option>
                        <option value="Type" ${step.actionType === 'Type' ? 'selected' : ''}>Type Text</option>
                        <option value="Navigate" ${step.actionType === 'Navigate' ? 'selected' : ''}>Navigate to URL</option>
                        <option value="Wait" ${step.actionType === 'Wait' ? 'selected' : ''}>Wait for Element</option>
                        <option value="Select" ${step.actionType === 'Select' ? 'selected' : ''}>Select from Dropdown</option>
                    ` : `
                        <option value="ElementVisible" ${step.type === 'ElementVisible' ? 'selected' : ''}>Element is Visible</option>
                        <option value="TextEquals" ${step.type === 'TextEquals' ? 'selected' : ''}>Text Equals</option>
                        <option value="TextContains" ${step.type === 'TextContains' ? 'selected' : ''}>Text Contains</option>
                        <option value="UrlContains" ${step.type === 'UrlContains' ? 'selected' : ''}>URL Contains</option>
                        <option value="ElementExists" ${step.type === 'ElementExists' ? 'selected' : ''}>Element Exists</option>
                    `}
                </select>
            </div>
            
            <div class="form-group" style="margin-bottom: 15px;">
                <label style="display: block; margin-bottom: 8px; font-weight: 600; color: #374151;">
                    Element Locator / URL
                </label>
                <input type="text" id="edit-step-locator" class="form-control" 
                       value="${escapeHtml(step.locator || '')}"
                       placeholder="e.g., #username, .btn-login"
                       style="font-family: monospace;">
            </div>
            
            <div id="value-field-container">
                <!-- Value field will be inserted here dynamically -->
            </div>
            
            <div style="display: flex; justify-content: flex-end; gap: 10px; margin-top: 25px; padding-top: 15px; border-top: 1px solid #e5e7eb;">
                <button class="btn btn-secondary" onclick="closeModal()">
                    <i class="fas fa-times"></i> Cancel
                </button>
                <button class="btn btn-primary" onclick="saveEditedStep(${index}, ${isAction})">
                    <i class="fas fa-save"></i> Save Changes
                </button>
            </div>
        </div>
    `;

    showModal(title, modalContent);

    // Initialize with current values
    const stepType = isAction ? step.actionType : step.type;
    handleStepTypeChange(stepType, isAction);

    // Set value if it exists
    setTimeout(() => {
        const valueField = document.getElementById('new-step-value');
        if (valueField) {
            valueField.value = step.value || step.expectedValue || '';
        }
    }, 100);
}

/**
 * Save edited step
 */
function saveEditedStep(index, isAction) {
    const stepType = document.getElementById('edit-step-type').value;
    const locator = document.getElementById('edit-step-locator').value.trim();
    const valueField = document.getElementById('new-step-value');
    const value = valueField ? valueField.value.trim() : '';

    // Validation
    if (!locator) {
        showError('Please enter a locator or URL');
        return;
    }

    // Update the step
    if (isAction) {
        currentEditingScenario.actions[index] = {
            actionType: stepType,
            locator: locator,
            value: value
        };
    } else {
        currentEditingScenario.assertions[index] = {
            ...currentEditingScenario.assertions[index],
            type: stepType,
            locator: locator,
            expectedValue: value
        };
    }

    closeModal();
    renderScenarioModal();
    showSuccess('Step updated successfully');
}

/**
 * Edit an assertion
 */
function editAssertion(index) {
    editStep(index, 'assertion');
}

/**
 * Delete a step
 */
function deleteStep(index, type) {
    const isAction = type === 'action';
    const stepNumber = index + 1;

    if (confirm(`Are you sure you want to delete step ${stepNumber}?`)) {
        if (isAction) {
            currentEditingScenario.actions.splice(index, 1);
            // Also remove associated assertions
            currentEditingScenario.assertions = currentEditingScenario.assertions.filter(
                a => a.afterActionIndex !== index
            );
            // Update indices for remaining assertions
            currentEditingScenario.assertions.forEach(a => {
                if (a.afterActionIndex > index) {
                    a.afterActionIndex--;
                }
            });
        }

        renderScenarioModal();
        showSuccess(`Step ${stepNumber} deleted`);
    }
}

/**
 * Delete an assertion
 */
function deleteAssertion(index) {
    if (confirm('Are you sure you want to delete this assertion?')) {
        currentEditingScenario.assertions.splice(index, 1);
        renderScenarioModal();
        showSuccess('Assertion deleted');
    }
}

/**
 * Execute scenario from modal
 */
function executeScenarioFromModal() {
    executeScenario(currentEditingScenario.module, currentEditingScenario.name);
    closeModal();
}

/**
 * Save scenario changes
 */
async function saveScenarioChanges() {
    try {
        // Validate
        if (!currentEditingScenario.startUrl) {
            showError('Start URL is required');
            return;
        }

        if (currentEditingScenario.actions.length === 0) {
            if (!confirm('This scenario has no test steps. Save anyway?')) {
                return;
            }
        }

        console.log('?? Starting save process...');
        console.log('?? Current scenario state:');
        console.log('   Actions:', currentEditingScenario.actions.length);
        console.log('   Assertions:', currentEditingScenario.assertions.length);

        // Build actions and assertions with proper execution order
        const orderedActions = [];
        const orderedAssertions = [];

        // Build unified steps collection to guarantee sequential execution order
        const unifiedSteps = [];
        let orderIndex = 0;

        currentEditingScenario.actions.forEach((action, actionIndex) => {
            // Add the action
            orderedActions.push(action);
            unifiedSteps.push({
                order: orderIndex++,
                stepName: 'Action_' + actionIndex,
                stepType: 'Action',
                action: action,
                assertion: null
            });

            // Find and add assertions that come after this action
            const assertionsForThisStep = currentEditingScenario.assertions.filter(
                a => a.afterActionIndex === actionIndex
            );

            console.log(`   Action ${actionIndex}: Found ${assertionsForThisStep.length} assertion(s)`);

            assertionsForThisStep.forEach(assertion => {
                // Include executeAfterActionIndex to tell backend where to run this assertion (legacy)
                const savedAssertion = {
                    type: assertion.type,
                    locator: assertion.locator,
                    expectedValue: assertion.expectedValue,
                    executeAfterActionIndex: actionIndex
                };
                orderedAssertions.push(savedAssertion);

                // Add to unified steps model
                unifiedSteps.push({
                    order: orderIndex++,
                    stepName: 'Assertion_' + savedAssertion.type,
                    stepType: 'Assertion',
                    action: null,
                    assertion: savedAssertion
                });
                console.log(`      ? Saving assertion: type=${assertion.type}, executeAfterActionIndex=${actionIndex}, locator=${assertion.locator}`);
            });
        });

        // Add any assertions that don't have a specific action index (legacy support)
        const unmatchedAssertions = currentEditingScenario.assertions.filter(
            a => a.afterActionIndex === undefined || a.afterActionIndex === null
        );

        console.log(`   Unmatched assertions (will execute at end): ${unmatchedAssertions.length}`);

        unmatchedAssertions.forEach(assertion => {
            const savedAssertion = {
                type: assertion.type,
                locator: assertion.locator,
                expectedValue: assertion.expectedValue
            };
            orderedAssertions.push(savedAssertion);

            unifiedSteps.push({
                order: orderIndex++,
                stepName: 'Assertion_' + savedAssertion.type,
                stepType: 'Assertion',
                action: null,
                assertion: savedAssertion
            });
        });

        const updatedScenario = {
            name: currentEditingScenario.name,
            module: currentEditingScenario.module,
            description: currentEditingScenario.description,
            startUrl: currentEditingScenario.startUrl,
            tags: currentEditingScenario.tags,
            actions: orderedActions,
            assertions: orderedAssertions,
            steps: unifiedSteps
        };

        console.log('?? Sending to backend:');
        console.log('   Actions:', orderedActions.length);
        console.log('   Assertions:', orderedAssertions.length);
        console.log('   Assertions payload:', JSON.stringify(orderedAssertions, null, 2));

        showLoading('Saving changes...');

        // Save via API
        const response = await fetch(`${API_BASE_URL}/scenarios/${currentEditingScenario.module}/${currentEditingScenario.name}`, {
            method: 'PUT',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(updatedScenario)
        });

        hideLoading();

        const data = await response.json();

        console.log('?? Backend response:', data);

        if (data.success) {
            showSuccess('Scenario updated successfully! Assertions will now execute at the correct steps.');
            closeModal();

            // Reload the current view
            if (currentView === 'scenarios') {
                loadScenariosView();
            } else if (currentView === 'dashboard') {
                loadDashboard();
            }
        } else {
            showError(data.error || 'Failed to update scenario');
        }
    } catch (error) {
        hideLoading();
        console.error('Error saving scenario:', error);
        showError('Failed to save changes: ' + error.message);
    }
}
