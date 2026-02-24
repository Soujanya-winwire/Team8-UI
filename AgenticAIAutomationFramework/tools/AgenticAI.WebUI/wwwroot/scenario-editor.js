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
            const assertions = (scenario.assertions || []).map(a => {
                const assertionWithIndex = {
                    type: a.type,
                    locator: a.locator,
                    expectedValue: a.expectedValue,
                    description: a.description
                };

                if (a.executeAfterActionIndex !== undefined && a.executeAfterActionIndex !== null) {
                    assertionWithIndex.afterActionIndex = a.executeAfterActionIndex;
                }

                return assertionWithIndex;
            });

            currentEditingScenario = {
                module: module,
                name: name,
                description: scenario.description || '',
                startUrl: scenario.startUrl || '',
                tags: scenario.tags || [],
                actions: JSON.parse(JSON.stringify(scenario.actions || [])),
                assertions: assertions
            };

            renderScenarioModal();
        }
    } catch (error) {
        console.error('Error loading scenario:', error);
        showError('Failed to load scenario details: ' + error.message);
    }
}

/**
 * Render the editable scenario modal with premium UI
 */
function renderScenarioModal() {
    const scenario = currentEditingScenario;

    showModal('Scenario Details', `
        <div style="max-height: 70vh; overflow-y: auto; padding-right: 10px;">
            <!-- Basic Info - Premium Display -->
            <div class="glass-card mb-20">
                <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 15px; margin-bottom: 15px;">
                    <div>
                        <label class="premium-form-label">Name</label> 
                        <div style="color: #1e293b; font-weight: 500;">${escapeHtml(scenario.name)}</div>
                    </div>
                    <div>
                        <label class="premium-form-label">Module</label> 
                        <div><span class="step-type-badge action" style="padding: 4px 10px;">${escapeHtml(scenario.module)}</span></div>
                    </div>
                </div>
                <div style="margin-bottom: 15px;">
                    <label class="premium-form-label">Description</label>
                    <div style="color: #475569; font-size: 14px;">${escapeHtml(scenario.description) || 'No description provided'}</div>
                </div>
                <div style="margin-bottom: 15px;">
                    <label class="premium-form-label">Start URL</label>
                    <div style="color: #475569; font-size: 14px; word-break: break-all;"><code>${escapeHtml(scenario.startUrl)}</code></div>
                </div>
                <div>
                    <label class="premium-form-label">Tags</label>
                    <div style="display: flex; gap: 6px; flex-wrap: wrap; margin-top: 5px;">
                        ${scenario.tags.map(tag => `<span class="badge badge-info" style="border-radius: 6px; font-weight: 500;">${escapeHtml(tag)}</span>`).join('') || '<span style="color: #94a3b8; font-size: 12px;">No tags</span>'}
                    </div>
                </div>
            </div>

            <!-- Test Steps Timeline -->
            <div style="margin-bottom: 20px;">
                <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px;">
                    <h4 style="margin: 0; color: #1e293b; font-weight: 700; font-size: 1.1em;">
                        <i class="fas fa-stream" style="color: var(--primary-color); margin-right: 8px;"></i> Test Scenario Flow
                    </h4>
                </div>
                <div id="steps-list-container">
                    ${renderStepsList()}
                </div>
            </div>
        </div>

        <!-- Action Footer -->
        <div style="display: flex; justify-content: space-between; align-items: center; margin-top: 25px; padding-top: 20px; border-top: 1px solid #e2e8f0;">
            <button class="btn btn-success" onclick="executeScenarioFromModal()" style="box-shadow: 0 4px 12px rgba(16, 185, 129, 0.2);">
                <i class="fas fa-play"></i> Execute Scenario
            </button>
            <div style="display: flex; gap: 12px;">
                <button class="btn btn-secondary" onclick="closeModal()" style="background: #f8fafc; color: #64748b; border: 1px solid #e2e8f0;">
                    Cancel
                </button>
                <button class="btn btn-primary" onclick="saveScenarioChanges()" style="box-shadow: 0 4px 12px rgba(79, 70, 229, 0.2);">
                    <i class="fas fa-save"></i> Save Changes
                </button>
            </div>
        </div>
    `);
}

/**
 * Render the list of test steps
 */
function renderStepsList() {
    const actions = currentEditingScenario.actions || [];
    const assertions = currentEditingScenario.assertions || [];

    if (actions.length === 0 && assertions.length === 0) {
        return `
            <div style="text-align: center; padding: 60px 40px; background: white; border-radius: 16px; border: 2px dashed #e2e8f0; margin-top: 20px;">
                <div style="width: 80px; height: 80px; background: #f8fafc; border-radius: 50%; display: flex; align-items: center; justify-content: center; margin: 0 auto 20px;">
                    <i class="fas fa-magic" style="font-size: 2em; color: var(--primary-color);"></i>
                </div>
                <h3 style="color: var(--dark); margin-bottom: 10px;">Start Building Your Test</h3>
                <p style="color: #64748b; font-size: 15px; margin-bottom: 25px;">Add actions or assertions to define your test scenario flow.</p>
                <div style="display: flex; justify-content: center; gap: 15px;">
                    <button class="btn btn-primary" onclick="addNewStep(0, 'action')">
                        <i class="fas fa-plus"></i> Add First Action
                    </button>
                    <button class="btn btn-success" onclick="addNewStep(0, 'assertion')">
                        <i class="fas fa-check"></i> Add First Assertion
                    </button>
                </div>
            </div>
        `;
    }

    let html = '<div class="step-timeline-container">';

    // 1. Render all actions
    actions.forEach((action, actionIndex) => {
        html += renderStepItem(action, actionIndex, 'action');
    });

    // 2. Render unassigned assertions
    const unassignedAssertions = assertions.filter(a =>
        a.afterActionIndex === undefined || a.afterActionIndex === null || a.afterActionIndex < 0
    );

    if (unassignedAssertions.length > 0) {
        html += `
            <div style="margin-top: 40px; margin-bottom: 20px; position: relative; z-index: 1;">
                <div style="display: flex; align-items: center; gap: 10px; padding: 10px 0; background: white;">
                    <div style="width: 100%; height: 1px; background: #e2e8f0; flex: 1;"></div>
                    <span style="color: #94a3b8; font-size: 11px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.1em;">Final Assertions</span>
                    <div style="width: 100%; height: 1px; background: #e2e8f0; flex: 1;"></div>
                </div>
            </div>
        `;
        unassignedAssertions.forEach(assertion => {
            const globalIndex = assertions.indexOf(assertion);
            html += renderStepItem(assertion, globalIndex, 'assertion');
        });
    }

    html += '</div>';
    return html;
}

/**
 * Render a single step item
 */
function renderStepItem(item, index, type) {
    const isAction = type === 'action';
    const displayIndex = index + 1;

    let description = '';
    let icon = '';

    if (isAction) {
        description = `<strong>${item.actionType}</strong> ${item.locator ? `on <code>${escapeHtml(item.locator)}</code>` : ''}`;
        if (item.value) description += ` with value <code>${escapeHtml(item.value)}</code>`;

        switch (item.actionType) {
            case 'Click': icon = 'fa-mouse-pointer'; break;
            case 'Type': icon = 'fa-keyboard'; break;
            case 'Navigate': icon = 'fa-compass'; break;
            case 'Wait': icon = 'fa-clock'; break;
            default: icon = 'fa-bolt';
        }
    } else {
        description = getAssertionDescription(item);
        icon = 'fa-check-double';
    }

    let html = `
        <div class="step-item-premium ${isAction ? 'action' : 'assertion'}">
            <div class="timeline-node">${displayIndex}</div>
            <div class="step-content-card">
                <div style="flex: 1;">
                    <span class="step-type-badge ${isAction ? 'action' : 'assertion'}">
                        <i class="fas ${icon}"></i> ${isAction ? 'Action' : 'Assertion'}
                    </span>
                    <div style="color: #1e293b; font-size: 15px; line-height: 1.5;">${description}</div>
                </div>
                
                <div class="step-actions">
                    ${isAction ? `
                        <button class="btn btn-icon" style="background: #f1f5f9; color: #64748b;" 
                                onclick="addNewStep(${index + 1}, 'action')" title="Add Step Below">
                            <i class="fas fa-plus"></i>
                        </button>
                    ` : ''}
                    <button class="btn btn-icon" style="background: #f1f5f9; color: var(--primary-color);" 
                            onclick="editStep(${index}, '${type}')" title="Edit Step">
                        <i class="fas fa-edit"></i>
                    </button>
                    <button class="btn btn-icon" style="background: #fef2f2; color: var(--danger-color);" 
                            onclick="${isAction ? `deleteStep(${index})` : `deleteAssertion(${index})`}" title="Delete Step">
                        <i class="fas fa-trash"></i>
                    </button>
                </div>
            </div>
    `;

    if (isAction) {
        const linkedAssertions = currentEditingScenario.assertions.filter(
            a => a.afterActionIndex === index
        );

        if (linkedAssertions.length > 0) {
            html += '<div style="margin-left: 20px; margin-top: 20px; border-left: 2px dashed rgba(16, 185, 129, 0.2); padding-left: 20px;">';
            linkedAssertions.forEach(assertion => {
                const globalAssertionIndex = currentEditingScenario.assertions.indexOf(assertion);
                html += renderStepItem(assertion, globalAssertionIndex, 'assertion');
            });
            html += '</div>';
        }
    }

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
            return `Element visible: <code class="premium-input" style="padding: 2px 6px; font-size: 13px;">${escapeHtml(locator)}</code>`;
        case 'ElementNotVisible':
            return `Element NOT visible: <code class="premium-input" style="padding: 2px 6px; font-size: 13px;">${escapeHtml(locator)}</code>`;
        case 'TextEquals':
            return `Text equals <strong>"${escapeHtml(value)}"</strong> on <code>${escapeHtml(locator)}</code>`;
        case 'TextContains':
            return `Text contains <strong>"${escapeHtml(value)}"</strong> on <code>${escapeHtml(locator)}</code>`;
        case 'UrlContains':
            return `URL contains <strong>"${escapeHtml(locator)}"</strong>`;
        case 'ElementExists':
            return `Element exists: <code>${escapeHtml(locator)}</code>`;
        case 'ElementNotExists':
            return `Element NOT exists: <code>${escapeHtml(locator)}</code>`;
        case 'TitleEquals':
            return `Page title equals <strong>"${escapeHtml(value)}"</strong>`;
        case 'TitleContains':
            return `Page title contains <strong>"${escapeHtml(value)}"</strong>`;
        case 'ValueEquals':
            return `Value equals <strong>"${escapeHtml(value)}"</strong> on <code>${escapeHtml(locator)}</code>`;
        default:
            return `${assertion.type}: <code>${escapeHtml(locator)}</code>`;
    }
}

/**
 * Add a new step with premium modal
 */
function addNewStep(position, type) {
    const isAction = type === 'action';
    const title = isAction ? 'Add Test Step' : 'Add Assertion';

    const modalContent = `
        <div style="padding: 10px;">
            <div class="glass-card mb-20">
                <div class="form-group">
                    <label class="premium-form-label">Step Category</label>
                    <select id="step-category-selector" class="form-control premium-input" onchange="updateStepTypeOptions(this.value)">
                        <option value="action" ${isAction ? 'selected' : ''}>Action (Interaction)</option>
                        <option value="assertion" ${!isAction ? 'selected' : ''}>Assertion (Verification)</option>
                    </select>
                </div>
            </div>
            
            <div class="card" style="border: 1px solid rgba(102, 126, 234, 0.1); box-shadow: 0 4px 12px rgba(0,0,0,0.03); padding: 15px; border-radius: 12px;">
                <div class="form-group">
                    <label class="premium-form-label">${isAction ? 'Action' : 'Assertion'} Type</label>
                    <select id="new-step-type" class="form-control premium-input" onchange="handleStepTypeChange(this.value, document.getElementById('step-category-selector').value === 'action')">
                        ${isAction ? `
                            <option value="Click">Click Element</option>
                            <option value="Type">Type Text</option>
                            <option value="Navigate">Navigate to URL</option>
                            <option value="Wait">Wait for Element</option>
                            <option value="Select">Select from Dropdown</option>
                        ` : `
                            <option value="ElementVisible">Element is Visible</option>
                            <option value="ElementNotVisible">Element is NOT Visible</option>
                            <option value="TextEquals">Text Equals</option>
                            <option value="TextContains">Text Contains</option>
                            <option value="TextNotContains">Text Does NOT Contain</option>
                            <option value="UrlContains">URL Contains</option>
                            <option value="ElementExists">Element Exists</option>
                            <option value="ElementNotExists">Element Does NOT Exist</option>
                            <option value="TitleEquals">Title Equals</option>
                            <option value="TitleContains">Title Contains</option>
                            <option value="ValueEquals">Value Equals</option>
                        `}
                    </select>
                </div>
                
                <div class="form-group">
                    <label class="premium-form-label">${isAction ? 'Element Locator' : 'Target Locator'}</label>
                    <input type="text" id="new-step-locator" class="form-control premium-input" 
                           placeholder="${isAction ? 'e.g., #username, .btn-login' : 'e.g., .alert-success, h1'}"
                           style="font-family: monospace;">
                </div>
                
                <div id="value-field-container"></div>
            </div>
            
            <div style="display: flex; justify-content: flex-end; gap: 12px; margin-top: 25px;">
                <button class="btn btn-secondary" onclick="closeModal()" style="background: #f1f5f9; color: #475569; border: 1px solid #e2e8f0;">
                    Cancel
                </button>
                <button class="btn btn-primary" id="save-new-step-btn" onclick="saveNewStep(${position}, document.getElementById('step-category-selector').value === 'action')">
                    Add Step
                </button>
            </div>
        </div>
    `;

    showModal(title, modalContent);
    handleStepTypeChange(isAction ? 'Click' : 'ElementVisible', isAction);
}

/**
 * Update step type options
 */
function updateStepTypeOptions(category) {
    const isAction = category === 'action';
    const typeSelect = document.getElementById('new-step-type');
    if (!typeSelect) return;

    if (isAction) {
        typeSelect.innerHTML = `
            <option value="Click">Click Element</option>
            <option value="Type">Type Text</option>
            <option value="Navigate">Navigate to URL</option>
            <option value="Wait">Wait for Element</option>
            <option value="Select">Select from Dropdown</option>
        `;
    } else {
        typeSelect.innerHTML = `
            <option value="ElementVisible">Element is Visible</option>
            <option value="ElementNotVisible">Element is NOT Visible</option>
            <option value="TextEquals">Text Equals</option>
            <option value="TextContains">Text Contains</option>
            <option value="TextNotContains">Text Does NOT Contain</option>
            <option value="UrlContains">URL Contains</option>
            <option value="ElementExists">Element Exists</option>
            <option value="ElementNotExists">Element Does NOT Exist</option>
            <option value="TitleEquals">Title Equals</option>
            <option value="TitleContains">Title Contains</option>
            <option value="ValueEquals">Value Equals</option>
        `;
    }
    handleStepTypeChange(typeSelect.value, isAction);
}

/**
 * Handle step type change
 */
function handleStepTypeChange(stepType, isAction) {
    const container = document.getElementById('value-field-container');
    if (!container) return;

    const actionTypes = ['Click', 'Type', 'Navigate', 'Wait', 'Select'];
    const needsValue = actionTypes.includes(stepType)
        ? (stepType === 'Type' || stepType === 'Select')
        : (stepType === 'TextEquals' || stepType === 'TextContains' || stepType === 'TextNotContains' ||
            stepType === 'TitleEquals' || stepType === 'TitleContains' || stepType === 'ValueEquals');

    if (needsValue) {
        const label = isAction
            ? (stepType === 'Type' ? 'Text to Type' : 'Option to Select')
            : 'Expected Value';
        const placeholder = isAction
            ? (stepType === 'Type' ? 'Enter the text to type' : 'Select option text or value')
            : 'Enter expected text value';

        container.innerHTML = `
            <div class="form-group">
                <label class="premium-form-label">${label}</label>
                <input type="text" id="new-step-value" class="form-control premium-input" 
                       placeholder="${placeholder}">
            </div>
        `;
    } else {
        container.innerHTML = '';
    }
}

/**
 * Save new step
 */
function saveNewStep(position, isAction) {
    const stepType = document.getElementById('new-step-type').value;
    const locator = document.getElementById('new-step-locator').value.trim();
    const valueField = document.getElementById('new-step-value');
    const value = valueField ? valueField.value.trim() : '';

    if (!locator) {
        showError('Please enter a locator or URL');
        return;
    }

    if (isAction) {
        const newAction = { actionType: stepType, locator: locator, value: value };
        currentEditingScenario.actions.splice(position, 0, newAction);
    } else {
        const newAssertion = {
            type: stepType,
            locator: locator,
            expectedValue: value,
            afterActionIndex: position - 1
        };
        currentEditingScenario.assertions.push(newAssertion);
    }

    closeModal();
    renderScenarioModal();
    showSuccess('Step added successfully');
}

/**
 * Edit a step with premium modal
 */
function editStep(index, type) {
    const isAction = type === 'action';
    const step = isAction ? currentEditingScenario.actions[index] : currentEditingScenario.assertions[index];
    const title = isAction ? `Edit Step ${index + 1}` : `Edit Assertion ${index + 1}`;

    const modalContent = `
        <div style="padding: 10px;">
            <div class="glass-card mb-20">
                <div class="form-group">
                    <label class="premium-form-label">Step Category</label>
                    <select id="edit-step-category-selector" class="form-control premium-input" onchange="updateEditStepTypeOptions(this.value, ${index})">
                        <option value="action" ${isAction ? 'selected' : ''}>Action (Interaction)</option>
                        <option value="assertion" ${!isAction ? 'selected' : ''}>Assertion (Verification)</option>
                    </select>
                </div>
            </div>
            
            <div class="card" style="border: 1px solid rgba(102, 126, 234, 0.1); box-shadow: 0 4px 12px rgba(0,0,0,0.03); padding: 15px; border-radius: 12px;">
                <div class="form-group">
                    <label class="premium-form-label">${isAction ? 'Action' : 'Assertion'} Type</label>
                    <select id="edit-step-type" class="form-control premium-input" onchange="handleStepTypeChange(this.value, document.getElementById('edit-step-category-selector').value === 'action')">
                        ${isAction ? `
                            <option value="Click" ${step.actionType === 'Click' ? 'selected' : ''}>Click Element</option>
                            <option value="Type" ${step.actionType === 'Type' ? 'selected' : ''}>Type Text</option>
                            <option value="Navigate" ${step.actionType === 'Navigate' ? 'selected' : ''}>Navigate to URL</option>
                            <option value="Wait" ${step.actionType === 'Wait' ? 'selected' : ''}>Wait for Element</option>
                            <option value="Select" ${step.actionType === 'Select' ? 'selected' : ''}>Select from Dropdown</option>
                        ` : `
                            <option value="ElementVisible" ${step.type === 'ElementVisible' ? 'selected' : ''}>Element is Visible</option>
                            <option value="ElementNotVisible" ${step.type === 'ElementNotVisible' ? 'selected' : ''}>Element is NOT Visible</option>
                            <option value="TextEquals" ${step.type === 'TextEquals' ? 'selected' : ''}>Text Equals</option>
                            <option value="TextContains" ${step.type === 'TextContains' ? 'selected' : ''}>Text Contains</option>
                            <option value="TextNotContains" ${step.type === 'TextNotContains' ? 'selected' : ''}>Text Does NOT Contain</option>
                            <option value="UrlContains" ${step.type === 'UrlContains' ? 'selected' : ''}>URL Contains</option>
                            <option value="ElementExists" ${step.type === 'ElementExists' ? 'selected' : ''}>Element Exists</option>
                            <option value="ElementNotExists" ${step.type === 'ElementNotExists' ? 'selected' : ''}>Element Does NOT Exist</option>
                            <option value="TitleEquals" ${step.type === 'TitleEquals' ? 'selected' : ''}>Title Equals</option>
                            <option value="TitleContains" ${step.type === 'TitleContains' ? 'selected' : ''}>Title Contains</option>
                            <option value="ValueEquals" ${step.type === 'ValueEquals' ? 'selected' : ''}>Value Equals</option>
                        `}
                    </select>
                </div>
                
                <div class="form-group">
                    <label class="premium-form-label">Locator / URL</label>
                    <input type="text" id="edit-step-locator" class="form-control premium-input" 
                           value="${escapeHtml(step.locator || '')}"
                           style="font-family: monospace;">
                </div>
                
                <div id="value-field-container"></div>
            </div>
            
            <div style="display: flex; justify-content: flex-end; gap: 12px; margin-top: 25px;">
                <button class="btn btn-secondary" onclick="closeModal()" style="background: #f1f5f9; color: #475569; border: 1px solid #e2e8f0;">
                    Cancel
                </button>
                <button class="btn btn-primary" onclick="saveEditedStep(${index}, document.getElementById('edit-step-category-selector').value === 'action', '${type}')">
                    Save Changes
                </button>
            </div>
        </div>
    `;

    showModal(title, modalContent);
    const stepType = isAction ? step.actionType : step.type;
    handleStepTypeChange(stepType, isAction);

    setTimeout(() => {
        const valueField = document.getElementById('new-step-value');
        if (valueField) {
            valueField.value = step.value || step.expectedValue || '';
        }
    }, 100);
}

/**
 * Update edit step type options
 */
function updateEditStepTypeOptions(category, index) {
    const isAction = category === 'action';
    const typeSelect = document.getElementById('edit-step-type');
    if (!typeSelect) return;

    if (isAction) {
        typeSelect.innerHTML = `
            <option value="Click">Click Element</option>
            <option value="Type">Type Text</option>
            <option value="Navigate">Navigate to URL</option>
            <option value="Wait">Wait for Element</option>
            <option value="Select">Select from Dropdown</option>
        `;
    } else {
        typeSelect.innerHTML = `
            <option value="ElementVisible">Element is Visible</option>
            <option value="ElementNotVisible">Element is NOT Visible</option>
            <option value="TextEquals">Text Equals</option>
            <option value="TextContains">Text Contains</option>
            <option value="TextNotContains">Text Does NOT Contain</option>
            <option value="UrlContains">URL Contains</option>
            <option value="ElementExists">Element Exists</option>
            <option value="ElementNotExists">Element Does NOT Exist</option>
            <option value="TitleEquals">Title Equals</option>
            <option value="TitleContains">Title Contains</option>
            <option value="ValueEquals">Value Equals</option>
        `;
    }
    handleStepTypeChange(typeSelect.value, isAction);
}

/**
 * Save edited step
 */
function saveEditedStep(index, isAction, originalType) {
    const stepType = document.getElementById('edit-step-type').value;
    const locator = document.getElementById('edit-step-locator').value.trim();
    const valueField = document.getElementById('new-step-value');
    const value = valueField ? valueField.value.trim() : '';

    if (!locator) {
        showError('Please enter a locator or URL');
        return;
    }

    const wasAction = originalType === 'action';

    if (wasAction && isAction) {
        currentEditingScenario.actions[index] = { actionType: stepType, locator: locator, value: value };
    } else if (!wasAction && !isAction) {
        currentEditingScenario.assertions[index] = {
            ...currentEditingScenario.assertions[index],
            type: stepType,
            locator: locator,
            expectedValue: value
        };
    } else if (wasAction && !isAction) {
        const newAssertion = { type: stepType, locator: locator, expectedValue: value, afterActionIndex: index - 1 };
        currentEditingScenario.actions.splice(index, 1);
        currentEditingScenario.assertions.push(newAssertion);
    } else if (!wasAction && isAction) {
        const newAction = { actionType: stepType, locator: locator, value: value };
        const assertion = currentEditingScenario.assertions[index];
        const targetPos = (assertion.afterActionIndex !== undefined && assertion.afterActionIndex !== null)
            ? assertion.afterActionIndex + 1
            : currentEditingScenario.actions.length;

        currentEditingScenario.assertions.splice(index, 1);
        currentEditingScenario.actions.splice(targetPos, 0, newAction);
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
function deleteStep(index) {
    if (confirm(`Are you sure you want to delete this step?`)) {
        currentEditingScenario.actions.splice(index, 1);
        currentEditingScenario.assertions = currentEditingScenario.assertions.filter(a => a.afterActionIndex !== index);
        currentEditingScenario.assertions.forEach(a => {
            if (a.afterActionIndex > index) a.afterActionIndex--;
        });
        renderScenarioModal();
        showSuccess(`Step deleted`);
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
        if (!currentEditingScenario.startUrl) {
            showError('Start URL is required');
            return;
        }

        const orderedActions = [];
        const orderedAssertions = [];
        const unifiedSteps = [];
        let orderIndex = 0;

        currentEditingScenario.actions.forEach((action, actionIndex) => {
            orderedActions.push(action);
            unifiedSteps.push({
                order: orderIndex++,
                stepName: 'Action_' + actionIndex,
                stepType: 'Action',
                action: action,
                assertion: null
            });

            const assertionsForThisStep = currentEditingScenario.assertions.filter(
                a => a.afterActionIndex === actionIndex
            );

            assertionsForThisStep.forEach(assertion => {
                const savedAssertion = {
                    type: assertion.type,
                    locator: assertion.locator,
                    expectedValue: assertion.expectedValue,
                    executeAfterActionIndex: actionIndex
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
        });

        const unmatchedAssertions = currentEditingScenario.assertions.filter(
            a => a.afterActionIndex === undefined || a.afterActionIndex === null || a.afterActionIndex < 0
        );

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

        showLoading('Saving changes...');
        const response = await fetch(`${API_BASE_URL}/scenarios/${currentEditingScenario.module}/${currentEditingScenario.name}`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(updatedScenario)
        });
        hideLoading();

        const data = await response.json();
        if (data.success) {
            showSuccess('Scenario updated successfully!');
            closeModal();
            if (currentView === 'scenarios') loadScenariosView();
            else if (currentView === 'dashboard') loadDashboard();
        } else {
            showError(data.error || 'Failed to update scenario');
        }
    } catch (error) {
        hideLoading();
        console.error('Error saving scenario:', error);
        showError('Failed to save changes: ' + error.message);
    }
}
