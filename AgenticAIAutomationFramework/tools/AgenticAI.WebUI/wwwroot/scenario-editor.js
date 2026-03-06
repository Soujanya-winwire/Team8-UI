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
        const response = await fetch(`${API_BASE_URL}/scenarios/${encodeURIComponent(module)}/${encodeURIComponent(name)}`);
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
 * Render the editable scenario modal with compact layout
 */
function renderScenarioModal() {
    const scenario = currentEditingScenario;

    showModal('Scenario Details', `
        <div style="display: flex; flex-direction: column; max-height: calc(90vh - 140px);">
            <!-- Compact Metadata Section (Grid Layout) -->
            <div style="background: #f9fafb; border-radius: 8px; padding: 16px; margin-bottom: 16px;">
                <div style="display: grid; grid-template-columns: repeat(2, 1fr); gap: 12px; font-size: 14px;">
                    <div>
                        <strong style="color: #6b7280; font-size: 12px; text-transform: uppercase;">Name</strong>
                        <div style="color: #1f2937; font-weight: 600; margin-top: 4px;">${escapeHtml(scenario.name)}</div>
                    </div>
                    <div>
                        <strong style="color: #6b7280; font-size: 12px; text-transform: uppercase;">Module</strong>
                        <div style="margin-top: 4px;"><span class="badge badge-primary">${escapeHtml(scenario.module)}</span></div>
                    </div>
                    <div style="grid-column: 1 / -1;">
                        <strong style="color: #6b7280; font-size: 12px; text-transform: uppercase;">Start URL</strong>
                        <div style="color: #1f2937; margin-top: 4px; word-break: break-all; font-size: 13px;">${escapeHtml(scenario.startUrl)}</div>
                    </div>
                    <div>
                        <strong style="color: #6b7280; font-size: 12px; text-transform: uppercase;">Tags</strong>
                        <div style="margin-top: 4px; display: flex; gap: 4px; flex-wrap: wrap;">
                            ${scenario.tags.map(t => `<span class="badge badge-info" style="margin-right: 4px; font-size: 11px;">${escapeHtml(t)}</span>`).join('') || '<span style="color: #6b7280; font-size: 12px;">No tags</span>'}
                        </div>
                    </div>
                    <div>
                        <strong style="color: #6b7280; font-size: 12px; text-transform: uppercase;">Description</strong>
                        <div style="color: #1f2937; margin-top: 4px; font-size: 13px;">${escapeHtml(scenario.description) || 'No description'}</div>
                    </div>
                </div>
            </div>

            <!-- Scrollable Content Area (Compact Table for Steps) -->
            <div style="flex: 1; overflow-y: auto; margin-bottom: 16px;">
                ${renderCompactStepsList()}
            </div>

            <!-- Sticky Action Buttons at Bottom -->
            <div style="display: flex; gap: 12px; padding-top: 16px; border-top: 2px solid #e5e7eb; background: white; justify-content: space-between;">
                <button class="btn btn-success" onclick="executeScenarioFromModal()" style="flex: 1; max-width: 200px;">
                    <i class="fas fa-play"></i> Execute Scenario
                </button>
                <div style="display: flex; gap: 12px;">
                    <button class="btn btn-secondary" onclick="closeModal();">
                        <i class="fas fa-times"></i> Cancel
                    </button>
                    <button class="btn btn-primary" onclick="saveScenarioChanges()">
                        <i class="fas fa-save"></i> Save Changes
                    </button>
                </div>
            </div>
        </div>
    `);
}

/**
 * Render steps in a compact table layout
 */
function renderCompactStepsList() {
    const actions = currentEditingScenario.actions || [];
    const assertions = currentEditingScenario.assertions || [];

    if (actions.length === 0 && assertions.length === 0) {
        return `
            <div style="text-align: center; padding: 40px 20px; background: white; border-radius: 8px; border: 1px dashed #e5e7eb;">
                <i class="fas fa-magic" style="font-size: 2.5em; color: var(--primary-color); margin-bottom: 15px; opacity: 0.3;"></i>
                <h3 style="color: var(--dark); margin-bottom: 10px; font-size: 1.1em;">No Steps Defined</h3>
                <p style="color: #64748b; font-size: 14px; margin-bottom: 20px;">Add actions or assertions to define your test scenario</p>
                <div style="display: flex; justify-content: center; gap: 12px;">
                    <button class="btn btn-primary btn-sm" onclick="addNewStep(0, 'action')">
                        <i class="fas fa-plus"></i> Add Action
                    </button>
                    <button class="btn btn-success btn-sm" onclick="addNewStep(0, 'assertion')">
                        <i class="fas fa-check"></i> Add Assertion
                    </button>
                </div>
            </div>
        `;
    }

    let html = '<div style="margin-bottom: 20px;">';
    
    // Actions Table
    if (actions.length > 0) {
        html += `
            <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px;">
                <h4 style="margin: 0; color: #1f2937; font-size: 16px; display: flex; align-items: center; gap: 8px;">
                    <i class="fas fa-list-ol" style="color: var(--primary-color);"></i>
                    Scenario Flow (${actions.length} steps)
                </h4>
                <button class="btn btn-primary btn-sm" onclick="addNewStep(${actions.length}, 'action')" title="Add Step at End">
                    <i class="fas fa-plus"></i> Add Step
                </button>
            </div>
            <table style="width: 100%; border-collapse: collapse; font-size: 13px; margin-bottom: 20px; table-layout: fixed;">
                <thead>
                    <tr style="background: #f3f4f6; border-bottom: 2px solid #e5e7eb;">
                        <th style="padding: 10px 12px; text-align: left; width: 50px; color: #6b7280; font-weight: 600;">#</th>
                        <th style="padding: 10px 12px; text-align: left; width: 100px; color: #6b7280; font-weight: 600;">Action</th>
                        <th style="padding: 10px 12px; text-align: left; width: 150px; color: #6b7280; font-weight: 600;">Locator</th>
                        <th style="padding: 10px 12px; text-align: left; width: 120px; color: #6b7280; font-weight: 600;">Value</th>
                        <th style="padding: 10px 12px; text-align: center; width: 200px; color: #6b7280; font-weight: 600;">Actions</th>
                    </tr>
                </thead>
                <tbody>
        `;
        
        actions.forEach((action, idx) => {
            const canMoveUp = idx > 0;
            const canMoveDown = idx < actions.length - 1;
            
            html += `
                <tr style="border-bottom: 1px solid #e5e7eb;">
                    <td style="padding: 10px 12px; color: #3b82f6; font-weight: 600;">${idx + 1}</td>
                    <td style="padding: 10px 12px;">
                        <span class="badge badge-primary" style="font-size: 11px;">${escapeHtml(action.actionType)}</span>
                    </td>
                    <td style="padding: 10px 12px; color: #374151; font-family: monospace; font-size: 11px; word-wrap: break-word; word-break: break-all; overflow-wrap: break-word; line-height: 1.4;">
                        ${escapeHtml(action.locator)}
                    </td>
                    <td style="padding: 10px 12px; color: #6b7280; font-size: 11px; word-wrap: break-word; word-break: break-all; overflow-wrap: break-word; line-height: 1.4;">
                        ${action.value ? escapeHtml(action.value) : '-'}
                    </td>
                    <td style="padding: 10px 12px; text-align: center;">
                        <div style="display: flex; gap: 4px; justify-content: center; flex-wrap: wrap;">
                            <button class="btn btn-sm" style="padding: 4px 8px; background: #f0f9ff; color: #0284c7; border: none; cursor: pointer; ${!canMoveUp ? 'opacity: 0.3; cursor: not-allowed;' : ''}" 
                                    onclick="moveStepUp(${idx})" title="Move Up" ${!canMoveUp ? 'disabled' : ''}>
                                <i class="fas fa-arrow-up"></i>
                            </button>
                            <button class="btn btn-sm" style="padding: 4px 8px; background: #f0f9ff; color: #0284c7; border: none; cursor: pointer; ${!canMoveDown ? 'opacity: 0.3; cursor: not-allowed;' : ''}" 
                                    onclick="moveStepDown(${idx})" title="Move Down" ${!canMoveDown ? 'disabled' : ''}>
                                <i class="fas fa-arrow-down"></i>
                            </button>
                            <button class="btn btn-sm" style="padding: 4px 8px; background: #ecfdf5; color: #059669; border: none; cursor: pointer;" 
                                    onclick="addNewStep(${idx + 1}, 'action')" title="Add Step Below">
                                <i class="fas fa-plus"></i>
                            </button>
                            <button class="btn btn-sm" style="padding: 4px 8px; background: #f1f5f9; color: var(--primary-color); border: none; cursor: pointer;" 
                                    onclick="editStep(${idx}, 'action')" title="Edit">
                                <i class="fas fa-edit"></i>
                            </button>
                            <button class="btn btn-sm" style="padding: 4px 8px; background: #fef2f2; color: var(--danger-color); border: none; cursor: pointer;" 
                                    onclick="deleteStep(${idx})" title="Delete">
                                <i class="fas fa-trash"></i>
                            </button>
                        </div>
                    </td>
                </tr>
            `;
        });
        
        html += `
                </tbody>
            </table>
        `;
    }
    
    // Assertions Table
    const unassignedAssertions = assertions.filter(a => 
        a.afterActionIndex === undefined || a.afterActionIndex === null || a.afterActionIndex < 0
    );
    
    if (unassignedAssertions.length > 0) {
        html += `
            <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px; margin-top: 20px;">
                <h4 style="margin: 0; color: #1f2937; font-size: 16px; display: flex; align-items: center; gap: 8px;">
                    <i class="fas fa-check-circle" style="color: var(--success-color);"></i>
                    Assertions (${unassignedAssertions.length})
                </h4>
                <button class="btn btn-success btn-sm" onclick="addNewStep(${actions.length}, 'assertion')" title="Add Assertion">
                    <i class="fas fa-plus"></i> Add Assertion
                </button>
            </div>
            <table style="width: 100%; border-collapse: collapse; font-size: 13px; table-layout: fixed;">
                <thead>
                    <tr style="background: #f0fdf4; border-bottom: 2px solid #d1fae5;">
                        <th style="padding: 10px 12px; text-align: left; width: 50px; color: #059669; font-weight: 600;">#</th>
                        <th style="padding: 10px 12px; text-align: left; width: 120px; color: #059669; font-weight: 600;">Type</th>
                        <th style="padding: 10px 12px; text-align: left; width: 150px; color: #059669; font-weight: 600;">Locator</th>
                        <th style="padding: 10px 12px; text-align: left; width: 150px; color: #059669; font-weight: 600;">Expected Value</th>
                        <th style="padding: 10px 12px; text-align: center; width: 150px; color: #059669; font-weight: 600;">Actions</th>
                    </tr>
                </thead>
                <tbody>
        `;
        
        unassignedAssertions.forEach((assertion, idx) => {
            const globalIndex = assertions.indexOf(assertion);
            html += `
                <tr style="border-bottom: 1px solid #d1fae5;">
                    <td style="padding: 10px 12px; color: #10b981; font-weight: 600;">${idx + 1}</td>
                    <td style="padding: 10px 12px;">
                        <span class="badge badge-success" style="font-size: 11px;">${escapeHtml(assertion.type)}</span>
                    </td>
                    <td style="padding: 10px 12px; color: #374151; font-family: monospace; font-size: 11px; word-wrap: break-word; word-break: break-all; overflow-wrap: break-word; line-height: 1.4;">
                        ${escapeHtml(assertion.locator)}
                    </td>
                    <td style="padding: 10px 12px; color: #6b7280; font-size: 11px; word-wrap: break-word; word-break: break-all; overflow-wrap: break-word; line-height: 1.4;">
                        ${assertion.expectedValue ? escapeHtml(assertion.expectedValue) : '-'}
                    </td>
                    <td style="padding: 10px 12px; text-align: center;">
                        <div style="display: flex; gap: 4px; justify-content: center;">
                            <button class="btn btn-sm" style="padding: 4px 8px; background: #f1f5f9; color: var(--primary-color); border: none; cursor: pointer;" 
                                    onclick="editStep(${globalIndex}, 'assertion')" title="Edit">
                                <i class="fas fa-edit"></i>
                            </button>
                            <button class="btn btn-sm" style="padding: 4px 8px; background: #fef2f2; color: var(--danger-color); border: none; cursor: pointer;" 
                                    onclick="deleteAssertion(${globalIndex})" title="Delete">
                                <i class="fas fa-trash"></i>
                            </button>
                        </div>
                    </td>
                </tr>
            `;
        });
        
        html += `
                </tbody>
            </table>
        `;
    }
    
    html += '</div>';
    return html;
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
            case 'Select': icon = 'fa-list-ul'; break;
            case 'Check': icon = 'fa-check-square'; break;
            case 'Uncheck': icon = 'fa-square'; break;
            case 'Hover': icon = 'fa-hand-pointer'; break;
            case 'Scroll': icon = 'fa-arrows-alt-v'; break;
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
                            <option value="Check">Check Checkbox/Radio</option>
                            <option value="Uncheck">Uncheck Checkbox/Radio</option>
                            <option value="Hover">Hover Over Element</option>
                            <option value="Scroll">Scroll Into View</option>
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
            <option value="Check">Check Checkbox/Radio</option>
            <option value="Uncheck">Uncheck Checkbox/Radio</option>
            <option value="Hover">Hover Over Element</option>
            <option value="Scroll">Scroll Into View</option>
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

    const noValueActions = ['Hover', 'Scroll', 'Click'];
    const isNoValue = isAction && noValueActions.includes(stepType);

    if (!isNoValue) {
        let label = 'Expected Value';
        let placeholder = 'Enter expected text value';

        if (isAction) {
            if (stepType === 'Type') {
                label = 'Text to Type';
                placeholder = 'Enter the text to type';
            } else if (stepType === 'Select') {
                label = 'Option to Select';
                placeholder = 'Select option text or value';
            } else if (stepType === 'Wait') {
                label = 'Seconds to Wait';
                placeholder = 'Enter wait time in seconds';
            } else {
                label = 'Value (optional)';
                placeholder = 'Optional parameters';
            }
        }

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
                            <option value="Check" ${step.actionType === 'Check' ? 'selected' : ''}>Check Checkbox/Radio</option>
                            <option value="Uncheck" ${step.actionType === 'Uncheck' ? 'selected' : ''}>Uncheck Checkbox/Radio</option>
                            <option value="Hover" ${step.actionType === 'Hover' ? 'selected' : ''}>Hover Over Element</option>
                            <option value="Scroll" ${step.actionType === 'Scroll' ? 'selected' : ''}>Scroll Into View</option>
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
            
            <div style="display: flex; justify-content: flex-end, gap: 12px; margin-top: 25px;">
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
            <option value="Check">Check Checkbox/Radio</option>
            <option value="Uncheck">Uncheck Checkbox/Radio</option>
            <option value="Hover">Hover Over Element</option>
            <option value="Scroll">Scroll Into View</option>
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
        const response = await fetch(`${API_BASE_URL}/scenarios/${encodeURIComponent(currentEditingScenario.module)}/${encodeURIComponent(currentEditingScenario.name)}`, {
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
