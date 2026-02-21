// API Configuration
const API_BASE_URL = 'http://localhost:5000/api';

// SignalR Connection
let connection = null;

// Global State
let currentView = 'dashboard';
let scenarios = [];
let configuration = {};
let executionResults = [];

// Initialize App
document.addEventListener('DOMContentLoaded', async () => {
    console.log('?? Initializing Agentic AI Test Management Platform...');
    
    // Initialize SignalR connection
    await initializeSignalR();
    
    // Load initial data
    await loadDashboard();
    
    console.log('? Platform initialized successfully!');
});

// SignalR Initialization
async function initializeSignalR() {
    connection = new signalR.HubConnectionBuilder()
        .withUrl("http://localhost:5000/testExecutionHub")
        .withAutomaticReconnect()
        .build();

    connection.on("ReceiveTestUpdate", (testName, status, message) => {
        addConsoleLog(message, status);
        updateExecutionStatus(status, message);
    });

    connection.on("ReceiveTestProgress", (current, total) => {
        updateProgressBar(current, total);
    });

    connection.on("ReceiveTestResult", (result) => {
        displayTestResult(result);
    });

    try {
        await connection.start();
        console.log("? SignalR Connected");
    } catch (err) {
        console.error("? SignalR Connection Error:", err);
        showWarning('Real-time updates unavailable. Some features may not work.');
    }
}

// View Management
function showView(viewName) {
    // Hide all views
    document.querySelectorAll('.view-content').forEach(view => {
        view.classList.add('hidden');
    });
    
    // Show selected view
    document.getElementById(`${viewName}-view`).classList.remove('hidden');
    
    // Update active nav item (only if event is available)
    document.querySelectorAll('.nav-item').forEach(item => {
        item.classList.remove('active');
    });
    
    if (typeof event !== 'undefined' && event && event.target) {
        const navItem = event.target.closest('.nav-item');
        if (navItem) {
            navItem.classList.add('active');
        }
    } else {
        // Find and activate the nav item programmatically
        const navItems = document.querySelectorAll('.nav-item');
        navItems.forEach(item => {
            const onClick = item.getAttribute('onclick');
            if (onClick && onClick.includes(`showView('${viewName}')`)) {
                item.classList.add('active');
            }
        });
    }
    
    currentView = viewName;
    
    // Load view-specific content
    switch(viewName) {
        case 'dashboard':
            loadDashboard();
            break;
        case 'record':
            loadRecordView();
            break;
        case 'scenarios':
            loadScenariosView();
            break;
        case 'create':
            loadCreateView();
            break;
        case 'execute':
            loadExecuteView();
            break;
        case 'results':
            loadResultsView();
            break;
        case 'configuration':
            loadConfigurationView();
            break;
        case 'documentation':
            loadDocumentationView();
            break;
    }
}

// Dashboard Functions
async function loadDashboard() {
    try {
        showLoading('Loading dashboard data...');
        
        // Load scenarios
        const response = await fetch(`${API_BASE_URL}/scenarios`);
        const data = await response.json();
        
        // Load execution history
        let historyData = { history: [] };
        try {
            const historyResponse = await fetch(`${API_BASE_URL}/history`);
            if (historyResponse.ok) {
                historyData = await historyResponse.json();
            }
        } catch (historyError) {
            console.warn('Could not load execution history:', historyError);
        }
        
        hideLoading();
        
        if (data.success) {
            scenarios = data.scenarios;
            
            // Update stats
            document.getElementById('total-scenarios').textContent = data.count;
            
            // Get modules
            const modules = [...new Set(scenarios.map(s => s.module))];
            document.getElementById('total-modules').textContent = modules.length;
            
            // Calculate execution statistics from history
            const history = historyData.history || [];
            const totalExecutions = history.length;
            const passedTests = history.filter(h => h.status === 'Passed').length;
            const failedTests = history.filter(h => h.status === 'Failed').length;
            const skippedTests = history.filter(h => h.status === 'Skipped').length;
            
            // Update execution stats if elements exist
            const totalExecElement = document.getElementById('total-executions');
            const passedElement = document.getElementById('total-passed');
            const failedElement = document.getElementById('total-failed');
            
            if (totalExecElement) totalExecElement.textContent = totalExecutions;
            if (passedElement) passedElement.textContent = passedTests;
            if (failedElement) failedElement.textContent = failedTests;
            
            // Display recent scenarios
            displayRecentScenarios(scenarios.slice(0, 5));
        }
    } catch (error) {
        hideLoading();
        console.error('Error loading dashboard:', error);
        handleError(error, 'Failed to load dashboard data');
    }
}

function displayRecentScenarios(recentScenarios) {
    const container = document.getElementById('recent-scenarios');
    
    if (recentScenarios.length === 0) {
        container.innerHTML = `
            <div class="empty-state">
                <i class="fas fa-inbox"></i>
                <h3>No test scenarios found</h3>
                <p>Create your first test scenario to get started!</p>
                <button class="btn btn-primary mt-20" onclick="showView('create')">
                    <i class="fas fa-plus"></i> Create Test
                </button>
            </div>
        `;
        return;
    }
    
    const html = `
        <table>
            <thead>
                <tr>
                    <th>Name</th>
                    <th>Module</th>
                    <th>Tags</th>
                    <th>Actions</th>
                    <th>Actions</th>
                </tr>
            </thead>
            <tbody>
                ${recentScenarios.map(scenario => `
                    <tr>
                        <td><strong>${scenario.name}</strong></td>
                        <td><span class="badge badge-primary">${scenario.module}</span></td>
                        <td>
                            ${scenario.tags.slice(0, 3).map(tag => 
                                `<span class="badge badge-info">${tag}</span>`
                            ).join(' ')}
                        </td>
                        <td>${scenario.actionCount} steps</td>
                        <td>
                            <button class="btn btn-success btn-icon" onclick="executeScenario('${scenario.module}', '${scenario.name}')">
                                <i class="fas fa-play"></i>
                            </button>
                            <button class="btn btn-secondary btn-icon" onclick="viewScenario('${scenario.module}', '${scenario.name}')">
                                <i class="fas fa-eye"></i>
                            </button>
                        </td>
                    </tr>
                `).join('')}
            </tbody>
        </table>
    `;
    
    container.innerHTML = html;
}

function displayExecutionHistory(historyList) {
    const container = document.getElementById('execution-history');
    if (!container) return; // Element might not exist on dashboard
    
    if (historyList.length === 0) {
        container.innerHTML = `
            <div class="empty-state">
                <i class="fas fa-history"></i>
                <h3>No execution history</h3>
                <p>Execute some tests to see history here!</p>
                <button class="btn btn-primary mt-20" onclick="showView('execute')">
                    <i class="fas fa-play"></i> Execute Tests
                </button>
            </div>
        `;
        return;
    }
    
    const html = `
        <table>
            <thead>
                <tr>
                    <th>Test Name</th>
                    <th>Status</th>
                    <th>Duration</th>
                    <th>Executed At</th>
                </tr>
            </thead>
            <tbody>
                ${historyList.map(item => {
                    const statusIcon = item.status === 'Passed' ? '?' : item.status === 'Failed' ? '?' : '??';
                    const statusClass = item.status === 'Passed' ? 'success' : item.status === 'Failed' ? 'danger' : 'warning';
                    const executedDate = item.executedAt ? new Date(item.executedAt).toLocaleString() : 'Unknown';
                    
                    return `
                    <tr>
                        <td><strong>${escapeHtml(item.testName || 'Unknown Test')}</strong></td>
                        <td>
                            <span class="badge badge-${statusClass}">
                                ${statusIcon} ${item.status}
                            </span>
                        </td>
                        <td>${item.duration || 'N/A'}</td>
                        <td>${executedDate}</td>
                    </tr>
                    `;
                }).join('')}
            </tbody>
        </table>
    `;
    
    container.innerHTML = html;
}


// Scenarios View
async function loadScenariosView() {
    const view = document.getElementById('scenarios-view');
    
    view.innerHTML = `
        <div class="header">
            <h2><i class="fas fa-list-check"></i> Test Scenarios</h2>
            <div class="header-actions">
                <button class="btn btn-secondary" onclick="loadScenariosView()">
                    <i class="fas fa-sync"></i> Refresh
                </button>
                <button class="btn btn-primary" onclick="showView('create')">
                    <i class="fas fa-plus"></i> New Scenario
                </button>
            </div>
        </div>

        <div class="card">
            <div class="card-header">
                <div class="card-title">Filter & Search</div>
            </div>
            <div class="grid-3">
                <div class="form-group">
                    <label>Module</label>
                    <select class="form-control" id="filter-module" onchange="filterScenarios()">
                        <option value="">All Modules</option>
                    </select>
                </div>
                <div class="form-group">
                    <label>Tag</label>
                    <select class="form-control" id="filter-tag" onchange="filterScenarios()">
                        <option value="">All Tags</option>
                    </select>
                </div>
                <div class="form-group">
                    <label>Search</label>
                    <input type="text" class="form-control" id="search-scenarios" 
                           placeholder="Search scenarios..." onkeyup="filterScenarios()">
                </div>
            </div>
        </div>

        <div class="card">
            <div id="scenarios-list">
                <div class="spinner"></div>
            </div>
        </div>
    `;
    
    try {
        // Load scenarios
        const response = await fetch(`${API_BASE_URL}/scenarios`);
        const data = await response.json();
        
        if (data.success) {
            scenarios = data.scenarios;
            
            // Populate filters
            const modules = [...new Set(scenarios.map(s => s.module))];
            const tags = [...new Set(scenarios.flatMap(s => s.tags))];
            
            document.getElementById('filter-module').innerHTML += 
                modules.map(m => `<option value="${m}">${m}</option>`).join('');
            
            document.getElementById('filter-tag').innerHTML += 
                tags.map(t => `<option value="${t}">${t}</option>`).join('');
            
            // Display scenarios
            displayAllScenarios(scenarios);
        }
    } catch (error) {
        console.error('Error loading scenarios:', error);
        showError('Failed to load scenarios');
    }
}

function displayAllScenarios(scenariosList) {
    const container = document.getElementById('scenarios-list');
    
    if (scenariosList.length === 0) {
        container.innerHTML = `
            <div class="empty-state">
                <i class="fas fa-search"></i>
                <h3>No scenarios found</h3>
                <p>Try adjusting your filters or create a new scenario</p>
            </div>
        `;
        return;
    }
    
    // Create table
    const table = document.createElement('table');
    table.innerHTML = `
        <thead>
            <tr>
                <th>Name</th>
                <th>Module</th>
                <th>Description</th>
                <th>Tags</th>
                <th>Steps</th>
                <th>Created</th>
                <th>Actions</th>
            </tr>
        </thead>
        <tbody></tbody>
    `;
    
    const tbody = table.querySelector('tbody');
    
    // Add each scenario row
    scenariosList.forEach(scenario => {
        const row = document.createElement('tr');
        
        // Name
        const nameCell = document.createElement('td');
        nameCell.innerHTML = `<strong>${escapeHtml(scenario.name)}</strong>`;
        row.appendChild(nameCell);
        
        // Module
        const moduleCell = document.createElement('td');
        moduleCell.innerHTML = `<span class="badge badge-primary">${escapeHtml(scenario.module)}</span>`;
        row.appendChild(moduleCell);
        
        // Description
        const descCell = document.createElement('td');
        descCell.textContent = scenario.description || 'No description';
        row.appendChild(descCell);
        
        // Tags
        const tagsCell = document.createElement('td');
        tagsCell.innerHTML = (scenario.tags || []).map(tag => 
            `<span class="badge badge-info">${escapeHtml(tag)}</span>`
        ).join(' ');
        row.appendChild(tagsCell);
        
        // Action count
        const stepsCell = document.createElement('td');
        stepsCell.textContent = `${scenario.actionCount || scenario.actions?.length || 0} steps`;
        row.appendChild(stepsCell);
        
        // Created date
        const dateCell = document.createElement('td');
        dateCell.textContent = scenario.createdAt ? new Date(scenario.createdAt).toLocaleDateString() : 'Unknown';
        row.appendChild(dateCell);
        
        // Action buttons
        const actionsCell = document.createElement('td');
        
        // Execute button
        const executeBtn = document.createElement('button');
        executeBtn.className = 'btn btn-success btn-icon';
        executeBtn.title = 'Execute';
        executeBtn.innerHTML = '<i class="fas fa-play"></i>';
        executeBtn.onclick = () => executeScenario(scenario.module, scenario.name);
        actionsCell.appendChild(executeBtn);
        
        // View button
        const viewBtn = document.createElement('button');
        viewBtn.className = 'btn btn-secondary btn-icon';
        viewBtn.title = 'View Details';
        viewBtn.innerHTML = '<i class="fas fa-eye"></i>';
        viewBtn.onclick = () => viewScenario(scenario.module, scenario.name);
        actionsCell.appendChild(viewBtn);
        
        // Delete button
        const deleteBtn = document.createElement('button');
        deleteBtn.className = 'btn btn-danger btn-icon';
        deleteBtn.title = 'Delete';
        deleteBtn.innerHTML = '<i class="fas fa-trash"></i>';
        deleteBtn.onclick = () => deleteScenario(scenario.module, scenario.name);
        actionsCell.appendChild(deleteBtn);
        
        row.appendChild(actionsCell);
        tbody.appendChild(row);
    });
    
    container.innerHTML = '';
    container.appendChild(table);
}

// Helper function to escape HTML
function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

function filterScenarios() {
    const moduleFilter = document.getElementById('filter-module').value;
    const tagFilter = document.getElementById('filter-tag').value;
    const searchText = document.getElementById('search-scenarios').value.toLowerCase();
    
    let filtered = scenarios;
    
    if (moduleFilter) {
        filtered = filtered.filter(s => s.module === moduleFilter);
    }
    
    if (tagFilter) {
        filtered = filtered.filter(s => s.tags.includes(tagFilter));
    }
    
    if (searchText) {
        filtered = filtered.filter(s => 
            s.name.toLowerCase().includes(searchText) || 
            (s.description && s.description.toLowerCase().includes(searchText))
        );
    }
    
    displayAllScenarios(filtered);
}

// Create Test View
function loadCreateView() {
    const view = document.getElementById('create-view');
    
    view.innerHTML = `
        <div class="header">
            <h2><i class="fas fa-edit"></i> Create Test Scenario</h2>
        </div>

        <div class="card">
            <div class="card-header">
                <div class="card-title">Scenario Details</div>
            </div>
            
            <form id="create-scenario-form" onsubmit="saveScenario(event)">
                <div class="grid-2">
                    <div class="form-group">
                        <label>Scenario Name *</label>
                        <input type="text" class="form-control" id="scenario-name" 
                               placeholder="e.g., Login_Test" required>
                    </div>
                    <div class="form-group">
                        <label>Module *</label>
                        <input type="text" class="form-control" id="scenario-module" 
                               placeholder="e.g., Authentication" required>
                    </div>
                </div>

                <div class="form-group">
                    <label>Description</label>
                    <textarea class="form-control" id="scenario-description" 
                              placeholder="Describe what this test does..."></textarea>
                </div>

                <div class="form-group">
                    <label>Start URL *</label>
                    <input type="url" class="form-control" id="scenario-url" 
                           placeholder="https://example.com" required>
                </div>

                <div class="form-group">
                    <label>Tags (comma-separated)</label>
                    <input type="text" class="form-control" id="scenario-tags" 
                           placeholder="smoke, regression, login">
                </div>

                <div class="card mt-20">
                    <div class="card-header">
                        <div class="card-title">Test Actions</div>
                        <div>
                            <button type="button" class="btn btn-primary btn-icon" onclick="addAction('Click')">
                                <i class="fas fa-mouse-pointer"></i> Click
                            </button>
                            <button type="button" class="btn btn-primary btn-icon" onclick="addAction('Type')">
                                <i class="fas fa-keyboard"></i> Type
                            </button>
                            <button type="button" class="btn btn-primary btn-icon" onclick="addAction('Navigate')">
                                <i class="fas fa-compass"></i> Navigate
                            </button>
                            <button type="button" class="btn btn-primary btn-icon" onclick="addAction('Wait')">
                                <i class="fas fa-clock"></i> Wait
                            </button>
                        </div>
                    </div>
                    <div id="actions-list"></div>
                </div>

                <div class="card mt-20">
                    <div class="card-header">
                        <div class="card-title">Assertions</div>
                        <div>
                            <button type="button" class="btn btn-success btn-icon" onclick="addAssertion('ElementVisible')">
                                <i class="fas fa-eye"></i> Visible
                            </button>
                            <button type="button" class="btn btn-success btn-icon" onclick="addAssertion('TextEquals')">
                                <i class="fas fa-equals"></i> Text Equals
                            </button>
                            <button type="button" class="btn btn-success btn-icon" onclick="addAssertion('TextContains')">
                                <i class="fas fa-font"></i> Contains
                            </button>
                            <button type="button" class="btn btn-success btn-icon" onclick="addAssertion('UrlContains')">
                                <i class="fas fa-link"></i> URL Contains
                            </button>
                        </div>
                    </div>
                    <div id="assertions-list"></div>
                </div>

                <div class="mt-20">
                    <button type="submit" class="btn btn-success">
                        <i class="fas fa-save"></i> Save Scenario
                    </button>
                    <button type="button" class="btn btn-secondary" onclick="clearCreateForm()">
                        <i class="fas fa-times"></i> Clear
                    </button>
                </div>
            </form>
        </div>
    `;
    
    window.currentActions = [];
    window.currentAssertions = [];
}

let currentActions = [];
let currentAssertions = [];
let isRecording = false;

// Record Test View (NEW)
async function loadRecordView() {
    const view = document.getElementById('record-view');
    
    // Check recording status
    try {
        const statusResponse = await fetch(`${API_BASE_URL}/recorder/status`);
        const statusData = await statusResponse.json();
        isRecording = statusData.isRecording;
    } catch (error) {
        console.error('Error checking recording status:', error);
        isRecording = false;
    }
    
    view.innerHTML = `
        <div class="header">
            <h2><i class="fas fa-video"></i> Interactive Test Recorder</h2>
        </div>

        <div class="card" style="background: linear-gradient(135deg, rgba(102, 126, 234, 0.1), rgba(118, 75, 162, 0.1)); border: 2px solid var(--primary-color);">
            <div style="text-align: center; padding: 20px;">
                <i class="fas fa-circle-dot" style="font-size: 3em; color: var(--primary-color); margin-bottom: 15px;"></i>
                <h3 style="color: var(--dark); margin-bottom: 10px;">True Record & Playback Experience</h3>
                <p style="color: #6b7280; line-height: 1.6;">
                    Simply interact with your application in the browser - all actions are automatically recorded!
                    No need to manually enter XPath or element IDs.
                </p>
            </div>
        </div>

        <!-- Interactive Test Recorder -->
        <div class="card">
            <div class="card-header">
                <div class="card-title" style="display: flex; align-items: center; gap: 10px;">
                    <i class="fas fa-video" style="color: var(--primary-color);"></i>
                    <span>Record Your Test</span>
                </div>
            </div>

            <div style="background: rgba(16, 185, 129, 0.05); padding: 15px; border-radius: 8px; margin-bottom: 20px;">
                <strong style="color: var(--success-color);">? How It Works:</strong>
                <ul style="margin-top: 10px; padding-left: 20px; line-height: 1.8;">
                    <li>Browser opens automatically for you</li>
                    <li>Interact with your application naturally</li>
                    <li>All actions are captured automatically</li>
                    <li>Save complete test scenario when done</li>
                </ul>
            </div>

            <form id="assisted-record-form" onsubmit="startAssistedRecording(event)">
                <div class="grid-2">
                    <div class="form-group">
                        <label>Scenario Name *</label>
                        <input type="text" class="form-control" id="record-name" 
                               placeholder="e.g., Login_Test" required>
                    </div>
                    <div class="form-group">
                        <label>Module *</label>
                        <input type="text" class="form-control" id="record-module" 
                               placeholder="e.g., Authentication" required>
                    </div>
                </div>
                
                <div class="form-group">
                    <label>Description</label>
                    <textarea class="form-control" id="record-description" 
                              placeholder="What does this test do?"></textarea>
                </div>
                
                <div class="form-group">
                    <label>Start URL *</label>
                    <input type="url" class="form-control" id="record-url" 
                           placeholder="https://www.saucedemo.com" required>
                </div>
                
                <div class="form-group">
                    <label>Tags (comma-separated)</label>
                    <input type="text" class="form-control" id="record-tags" 
                           placeholder="smoke, regression">
                </div>

                <div style="display: flex; gap: 15px; align-items: center;">
                    <button type="submit" class="btn btn-success" id="start-recording-btn" ${isRecording ? 'disabled' : ''}>
                        <i class="fas fa-circle-dot"></i> Start Recording
                    </button>
                    <button type="button" class="btn btn-danger" id="stop-recording-btn" 
                            onclick="stopRecording()" ${!isRecording ? 'disabled' : ''}>
                        <i class="fas fa-stop-circle"></i> Stop Recording
                    </button>
                </div>
            </form>

            <div id="recording-status" class="hidden" style="margin-top: 20px; padding: 15px; background: rgba(239, 68, 68, 0.1); border-radius: 8px; border-left: 4px solid var(--danger-color);">
                <div style="display: flex; align-items: center; gap: 10px;">
                    <div class="spinner" style="width: 20px; height: 20px; margin: 0;"></div>
                    <strong style="color: var(--danger-color);">?? Recording in progress...</strong>
                </div>
                <p style="margin-top: 10px; color: #6b7280;">
                    Perform your test actions in the opened browser window. 
                    Click "Stop Recording" when done.
                </p>
            </div>
        </div>

        <!-- How It Works Card -->
        <div class="card">
            <div class="card-header">
                <div class="card-title"><i class="fas fa-lightbulb"></i> How Interactive Recording Works</div>
            </div>

            <div style="padding: 20px;">
                <h4 style="color: var(--primary-color); margin-bottom: 20px;">
                    <i class="fas fa-list-ol"></i> Simple 5-Step Process
                </h4>
                <ol style="line-height: 2.5; padding-left: 20px; font-size: 1.05em;">
                    <li><strong>Fill in scenario details</strong> - Give your test a name and description</li>
                    <li><strong>Click "Start Recording"</strong> - Browser opens automatically</li>
                    <li><strong>Interact with your application</strong> - Click, type, navigate naturally</li>
                    <li><strong>All actions are captured</strong> - No manual XPath or element selection needed</li>
                    <li><strong>Click "Stop Recording"</strong> - Test scenario is saved and ready to execute!</li>
                </ol>
            </div>

            <div style="margin-top: 30px; padding: 20px; background: rgba(102, 126, 234, 0.05); border-radius: 8px;">
                <h4 style="margin-bottom: 15px;"><i class="fas fa-bullseye"></i> Why Use Interactive Recording?</h4>
                <div class="grid-3">
                    <div>
                        <strong style="color: var(--primary-color);">? 10x Faster</strong>
                        <p style="color: #6b7280; margin-top: 5px;">
                            Record in 2 minutes vs. 30 minutes manual creation
                        </p>
                    </div>
                    <div>
                        <strong style="color: var(--primary-color);">?? No Technical Skills Needed</strong>
                        <p style="color: #6b7280; margin-top: 5px;">
                            Just click and type - locators generated automatically
                        </p>
                    </div>
                    <div>
                        <strong style="color: var(--primary-color);">? Accurate</strong>
                        <p style="color: #6b7280; margin-top: 5px;">
                            Records exactly what you do - no mistakes
                        </p>
                    </div>
                </div>
            </div>
        </div>

        <!-- Quick Tips Card -->
        <div class="card">
            <div class="card-header">
                <div class="card-title"><i class="fas fa-lightbulb"></i> Tips for Best Results</div>
            </div>
            
            <div style="padding: 20px;">
                <div class="grid-2">
                    <div>
                        <h4 style="color: var(--success-color); margin-bottom: 15px;">
                            <i class="fas fa-check-circle"></i> Do's
                        </h4>
                        <ul style="line-height: 2; padding-left: 20px;">
                            <li>Perform actions naturally and slowly</li>
                            <li>Wait for pages to load completely</li>
                            <li>Use clear, descriptive test names</li>
                            <li>Group related tests in same module</li>
                        </ul>
                    </div>
                    <div>
                        <h4 style="color: var(--danger-color); margin-bottom: 15px;">
                            <i class="fas fa-times-circle"></i> Don'ts
                        </h4>
                        <ul style="line-height: 2; padding-left: 20px;">
                            <li>Don't click too fast - give time to record</li>
                            <li>Don't switch tabs during recording</li>
                            <li>Don't close browser manually</li>
                            <li>Don't use browser extensions that interfere</li>
                        </ul>
                    </div>
                </div>
            </div>
        </div>
    `;
}

async function startAssistedRecording(event) {
    event.preventDefault();
    
    const request = {
        scenarioName: document.getElementById('record-name').value,
        module: document.getElementById('record-module').value,
        description: document.getElementById('record-description').value,
        startUrl: document.getElementById('record-url').value,
        tags: document.getElementById('record-tags').value.split(',').map(t => t.trim()).filter(t => t)
    };
    
    try {
        // Show loading state
        const startBtn = document.getElementById('start-recording-btn');
        const originalText = startBtn.innerHTML;
        startBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Starting...';
        startBtn.disabled = true;
        
        const response = await fetch(`${API_BASE_URL}/recorder/start`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(request)
        });
        
        const data = await response.json();
        
        if (data.success) {
            isRecording = true;
            document.getElementById('start-recording-btn').innerHTML = originalText;
            document.getElementById('start-recording-btn').disabled = true;
            document.getElementById('stop-recording-btn').disabled = false;
            document.getElementById('recording-status').classList.remove('hidden');
            
            // Try to add console log if available
            try {
                addConsoleLog('Recording started! Browser opened. Perform your test actions.', 'success');
            } catch (e) {
                console.log('Console not available:', e);
            }
            
            showSuccess('Recording started! Interact with the browser that just opened.');
        } else {
            // Reset button
            startBtn.innerHTML = originalText;
            startBtn.disabled = false;
            showError(data.error || 'Failed to start recording');
        }
    } catch (error) {
        console.error('Start recording error:', error);
        
        // Reset button state
        const startBtn = document.getElementById('start-recording-btn');
        if (startBtn) {
            startBtn.innerHTML = '<i class="fas fa-circle-dot"></i> Start Recording';
            startBtn.disabled = false;
        }
        
        showError('Failed to start recording: ' + error.message);
    }
}

async function stopRecording() {
    try {
        // Show loading state
        const stopBtn = document.getElementById('stop-recording-btn');
        const originalText = stopBtn.innerHTML;
        stopBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Stopping...';
        stopBtn.disabled = true;
        
        const response = await fetch(`${API_BASE_URL}/recorder/stop`, {
            method: 'POST'
        });
        
        const data = await response.json();
        
        // Reset button
        stopBtn.innerHTML = originalText;
        
        if (data.success) {
            isRecording = false;
            document.getElementById('start-recording-btn').disabled = false;
            document.getElementById('stop-recording-btn').disabled = true;
            document.getElementById('recording-status').classList.add('hidden');
            
            // Handle different response formats
            const actionCount = data.scenario?.actionCount || 
                               data.scenario?.actions?.length || 
                               data.actionCount || 
                               0;
            const scenarioName = data.scenario?.name || 
                                data.scenarioName || 
                                'Test Scenario';
            
            showSuccess(`Recording saved! ${actionCount} action(s) captured.`);
            
            // Try to add console log if console exists
            try {
                addConsoleLog(`Recording completed: ${scenarioName}`, 'success');
            } catch (e) {
                console.log('Console log not available:', e);
            }
            
            // Clear form
            const form = document.getElementById('assisted-record-form');
            if (form) {
                form.reset();
            }
            
            // Ask if user wants to view the scenario
            if (confirm('Recording saved! Would you like to view the scenario?')) {
                showView('scenarios');
            }
        } else {
            stopBtn.disabled = false;
            showError(data.error || 'Failed to stop recording');
        }
    } catch (error) {
        console.error('Stop recording error:', error);
        
        // Reset button state
        const stopBtn = document.getElementById('stop-recording-btn');
        if (stopBtn) {
            stopBtn.innerHTML = '<i class="fas fa-stop-circle"></i> Stop Recording';
            stopBtn.disabled = false;
        }
        
        // Show user-friendly error
        showError('Failed to stop recording. Please check if the browser window is still open.');
    }
}

// Execute Tests View
async function loadExecuteView() {
    const view = document.getElementById('execute-view');
    
    view.innerHTML = `
        <div class="header">
            <h2><i class="fas fa-play-circle"></i> Execute Tests</h2>
        </div>

        <div class="execution-status" id="execution-status">
            <div style="display: flex; justify-content: space-between; align-items: center;">
                <div>
                    <strong id="execution-status-text">Ready to execute tests</strong>
                    <p id="execution-message" style="margin-top: 5px; color: #6b7280;"></p>
                </div>
                <div class="spinner hidden" id="execution-spinner"></div>
            </div>
            <div class="progress-bar">
                <div class="progress-fill" id="execution-progress" style="width: 0%"></div>
            </div>
        </div>

        <div class="grid-2">
            <div class="card">
                <div class="card-header">
                    <div class="card-title">Execute Single Scenario</div>
                </div>
                <div class="form-group">
                    <label>Module</label>
                    <select class="form-control" id="exec-module">
                        <option value="">Select Module</option>
                    </select>
                </div>
                <div class="form-group">
                    <label>Scenario</label>
                    <select class="form-control" id="exec-scenario">
                        <option value="">Select Scenario</option>
                    </select>
                </div>
                <button class="btn btn-success" onclick="executeSingleScenario()">
                    <i class="fas fa-play"></i> Execute Scenario
                </button>
            </div>

            <div class="card">
                <div class="card-header">
                    <div class="card-title">Execute by Category</div>
                </div>
                <div class="form-group">
                    <label>Module</label>
                    <select class="form-control" id="exec-module-all">
                        <option value="">Select Module</option>
                    </select>
                </div>
                <button class="btn btn-success" onclick="executeModule()">
                    <i class="fas fa-folder"></i> Execute Module
                </button>
                <div class="form-group mt-20">
                    <label>Tag</label>
                    <select class="form-control" id="exec-tag">
                        <option value="">Select Tag</option>
                    </select>
                </div>
                <button class="btn btn-success" onclick="executeTag()">
                    <i class="fas fa-tag"></i> Execute Tagged Tests
                </button>
            </div>
        </div>

        <div class="card">
            <div class="card-header">
                <div class="card-title">Console Output</div>
                <button class="btn btn-secondary btn-icon" onclick="clearConsole()">
                    <i class="fas fa-eraser"></i> Clear
                </button>
            </div>
            <div class="console" id="console-output">
                <div class="console-line">Ready to execute tests...</div>
            </div>
        </div>
    `;
    
    // Load modules and tags
    try {
        const modulesResponse = await fetch(`${API_BASE_URL}/scenarios/modules`);
        const modulesData = await modulesResponse.json();
        
        if (modulesData.success) {
            const moduleOptions = modulesData.modules.map(m => 
                `<option value="${m}">${m}</option>`
            ).join('');
            
            document.getElementById('exec-module').innerHTML += moduleOptions;
            document.getElementById('exec-module-all').innerHTML += moduleOptions;
        }
        
        const tagsResponse = await fetch(`${API_BASE_URL}/scenarios/tags`);
        const tagsData = await tagsResponse.json();
        
        if (tagsData.success) {
            const tagOptions = tagsData.tags.map(t => 
                `<option value="${t}">${t}</option>`
            ).join('');
            
            document.getElementById('exec-tag').innerHTML += tagOptions;
        }
    } catch (error) {
        console.error('Error loading execution options:', error);
    }
    
    // Load scenarios when module changes
    document.getElementById('exec-module').addEventListener('change', async (e) => {
        const module = e.target.value;
        if (!module) return;
        
        try {
            const response = await fetch(`${API_BASE_URL}/scenarios/module/${module}`);
            const data = await response.json();
            
            if (data.success) {
                const scenarioOptions = data.scenarios.map(s => 
                    `<option value="${s.name}">${s.name}</option>`
                ).join('');
                
                document.getElementById('exec-scenario').innerHTML = 
                    '<option value="">Select Scenario</option>' + scenarioOptions;
            }
        } catch (error) {
            console.error('Error loading scenarios:', error);
        }
    });
}

async function executeSingleScenario() {
    const module = document.getElementById('exec-module').value;
    const scenario = document.getElementById('exec-scenario').value;
    
    if (!module || !scenario) {
        showError('Please select both module and scenario');
        return;
    }
    
    await executeScenario(module, scenario);
}

async function executeScenario(module, name) {
    try {
        // Check if we're on the execute view
        const isOnExecuteView = currentView === 'execute';
        
        if (!isOnExecuteView) {
            // Show notification and navigate to execute view
            showInfo(`Executing test: ${name}. Navigating to Execute Tests view...`);
            
            // Small delay to show the notification
            await new Promise(resolve => setTimeout(resolve, 500));
            
            // Navigate to execute view
            showView('execute');
            
            // Wait for view to load
            await new Promise(resolve => setTimeout(resolve, 1000));
        }
        
        // Now execute the scenario
        if (typeof updateExecutionStatus === 'function') {
            updateExecutionStatus('running', `Executing ${name}...`);
        }
        
        if (typeof addConsoleLog === 'function') {
            addConsoleLog(`Starting execution of ${name}`, 'info');
        }
        
        showInfo(`Executing test scenario: ${name}...`);
        
        const response = await fetch(`${API_BASE_URL}/scenarios/execute/${module}/${name}`, {
            method: 'POST'
        });
        
        const data = await response.json();
        
        if (data.success) {
            // Check actual test result status
            const testPassed = data.result.status === 'Passed';
            const statusType = testPassed ? 'success' : 'failed';
            const statusMessage = testPassed ? 'success' : 'error';
            
            if (typeof updateExecutionStatus === 'function') {
                updateExecutionStatus(statusType, testPassed ? 'Execution completed successfully' : 'Test execution completed with failures');
            }
            if (typeof addConsoleLog === 'function') {
                addConsoleLog(`? Test completed: ${data.result.status}`, statusMessage);
                displayTestResult(data.result);
            }
            
            // Show appropriate notification based on actual test result
            if (testPassed) {
                showSuccess(`? Test passed: ${data.result.status}`);
            } else {
                showError(`? Test failed: ${data.result.status}${data.result.errorMessage ? ' - ' + data.result.errorMessage : ''}`);
            }
        } else {
            if (typeof updateExecutionStatus === 'function') {
                updateExecutionStatus('failed', 'Execution failed');
            }
            if (typeof addConsoleLog === 'function') {
                addConsoleLog(`? Test failed: ${data.error}`, 'error');
            }
            showError(`Test execution failed: ${data.error}`);
        }
    } catch (error) {
        console.error('Execution error:', error);
        if (typeof updateExecutionStatus === 'function') {
            updateExecutionStatus('failed', 'Execution error');
        }
        if (typeof addConsoleLog === 'function') {
            addConsoleLog(`? Error: ${error.message}`, 'error');
        }
        showError(`Execution error: ${error.message}`);
    }
}

async function executeModule() {
    const module = document.getElementById('exec-module-all').value;
    
    if (!module) {
        showError('Please select a module');
        return;
    }
    
    try {
        updateExecutionStatus('running', `Executing module: ${module}...`);
        addConsoleLog(`Starting module execution: ${module}`, 'info');
        
        const response = await fetch(`${API_BASE_URL}/scenarios/execute/module/${module}`, {
            method: 'POST'
        });
        
        const data = await response.json();
        
        if (data.success) {
            updateExecutionStatus('success', `Module completed: ${data.count} tests executed`);
            addConsoleLog(`? Module completed: ${data.count} tests`, 'success');
        } else {
            updateExecutionStatus('failed', 'Module execution failed');
            addConsoleLog(`? Module failed: ${data.error}`, 'error');
        }
    } catch (error) {
        updateExecutionStatus('failed', 'Execution error');
        addConsoleLog(`? Error: ${error.message}`, 'error');
    }
}

async function executeTag() {
    const tag = document.getElementById('exec-tag').value;
    
    if (!tag) {
        showError('Please select a tag');
        return;
    }
    
    try {
        updateExecutionStatus('running', `Executing tests with tag: ${tag}...`);
        addConsoleLog(`Starting tagged execution: ${tag}`, 'info');
        
        const response = await fetch(`${API_BASE_URL}/scenarios/execute/tag/${tag}`, {
            method: 'POST'
        });
        
        const data = await response.json();
        
        if (data.success) {
            updateExecutionStatus('success', `Tagged tests completed: ${data.count} tests executed`);
            addConsoleLog(`? Tagged tests completed: ${data.count} tests`, 'success');
        } else {
            updateExecutionStatus('failed', 'Tagged execution failed');
            addConsoleLog(`? Tagged execution failed: ${data.error}`, 'error');
        }
    } catch (error) {
        updateExecutionStatus('failed', 'Execution error');
        addConsoleLog(`? Error: ${error.message}`, 'error');
    }
}

async function executeQuickTest(tag) {
    showView('execute');
    setTimeout(async () => {
        document.getElementById('exec-tag').value = tag;
        await executeTag();
    }, 500);
}

function updateExecutionStatus(status, message) {
    const statusEl = document.getElementById('execution-status');
    const textEl = document.getElementById('execution-status-text');
    const messageEl = document.getElementById('execution-message');
    const spinner = document.getElementById('execution-spinner');
    
    statusEl.className = 'execution-status active ' + status;
    textEl.textContent = status.charAt(0).toUpperCase() + status.slice(1);
    messageEl.textContent = message;
    
    if (status === 'running') {
        spinner.classList.remove('hidden');
    } else {
        spinner.classList.add('hidden');
    }
}

function updateProgressBar(current, total) {
    const progress = (current / total) * 100;
    document.getElementById('execution-progress').style.width = progress + '%';
}

function addConsoleLog(message, type = 'info') {
    const console = document.getElementById('console-output');
    if (!console) return;
    
    const timestamp = new Date().toLocaleTimeString();
    const line = document.createElement('div');
    line.className = `console-line ${type}`;
    line.textContent = `[${timestamp}] ${message}`;
    
    console.appendChild(line);
    console.scrollTop = console.scrollHeight;
}

function clearConsole() {
    const console = document.getElementById('console-output');
    console.innerHTML = '<div class="console-line">Console cleared...</div>';
}

function displayTestResult(result) {
    addConsoleLog(`Test: ${result.testCaseName}`, 'info');
    addConsoleLog(`Status: ${result.status}`, result.status === 'Passed' ? 'success' : 'error');
    addConsoleLog(`Duration: ${result.duration}`, 'info');
    addConsoleLog(`Steps: ${result.steps?.length || 0}`, 'info');
    addConsoleLog('---', 'info');
}

// Configuration View
async function loadConfigurationView() {
    const view = document.getElementById('configuration-view');
    
    view.innerHTML = '<div class="spinner"></div>';
    
    try {
        const response = await fetch(`${API_BASE_URL}/configuration`);
        const data = await response.json();
        
        if (data.success) {
            configuration = data.configuration;
            
            view.innerHTML = `
        <div class="header">
            <h2><i class="fas fa-cog"></i> Configuration</h2>
                    <div class="header-actions">
                        <button class="btn btn-success" onclick="saveConfiguration()">
                            <i class="fas fa-save"></i> Save Changes
                        </button>
                    </div>
                </div>

                <div class="card">
                    <div class="card-header">
                        <div class="card-title">Framework Settings</div>
                    </div>
                    
                    <form id="config-form">
                        <div class="form-group">
                            <label>?? Base URL (Application Under Test)</label>
                            <input type="url" class="form-control" id="config-base-url" 
                                   value="${configuration.baseUrl || 'https://www.saucedemo.com'}" 
                                   placeholder="https://your-app.com">
                            <small style="color: #6b7280; display: block; margin-top: 5px;">
                                The default URL for your test scenarios. Tests can override this with their own Start URL.
                            </small>
                        </div>

                        <div class="grid-2">
                            <div class="form-group">
                                <label>Automation Framework</label>
                                <select class="form-control" id="config-framework">
                                    <option value="Playwright" ${configuration.automationFramework === 'Playwright' ? 'selected' : ''}>Playwright</option>
                                    <option value="Selenium" ${configuration.automationFramework === 'Selenium' ? 'selected' : ''}>Selenium</option>
                                </select>
                            </div>
                            <div class="form-group">
                                <label>Browser</label>
                                <select class="form-control" id="config-browser">
                                    <option value="Chrome" ${configuration.browser === 'Chrome' ? 'selected' : ''}>Chrome</option>
                                    <option value="Firefox" ${configuration.browser === 'Firefox' ? 'selected' : ''}>Firefox</option>
                                    <option value="Edge" ${configuration.browser === 'Edge' ? 'selected' : ''}>Edge</option>
                                    <option value="Safari" ${configuration.browser === 'Safari' ? 'selected' : ''}>Safari</option>
                                </select>
                            </div>
                            <div class="form-group">
                                <label>Environment</label>
                                <select class="form-control" id="config-environment">
                                    <option value="Dev" ${configuration.environment === 'Dev' ? 'selected' : ''}>Dev</option>
                                    <option value="QA" ${configuration.environment === 'QA' ? 'selected' : ''}>QA</option>
                                    <option value="UAT" ${configuration.environment === 'UAT' ? 'selected' : ''}>UAT</option>
                                    <option value="Staging" ${configuration.environment === 'Staging' ? 'selected' : ''}>Staging</option>
                                    <option value="Prod" ${configuration.environment === 'Prod' ? 'selected' : ''}>Prod</option>
                                </select>
                            </div>
                            <div class="form-group">
                                <label>Execution Mode</label>
                                <select class="form-control" id="config-execution-mode">
                                    <option value="Sequential" ${configuration.executionMode === 'Sequential' ? 'selected' : ''}>Sequential</option>
                                    <option value="Parallel" ${configuration.executionMode === 'Parallel' ? 'selected' : ''}>Parallel</option>
                                </select>
                            </div>
                            <div class="form-group">
                                <label>Timeout (seconds)</label>
                                <input type="number" class="form-control" id="config-timeout" 
                                       value="${configuration.timeoutInSeconds}" min="5" max="300">
                            </div>
                            <div class="form-group">
                                <label>Max Retry Count</label>
                                <input type="number" class="form-control" id="config-retry" 
                                       value="${configuration.maxRetryCount}" min="0" max="5">
                            </div>
                        </div>

                        <div class="card mt-20">
                            <div class="card-header">
                                <div class="card-title">Features</div>
                            </div>
                            <div class="grid-2">
                                <div class="form-group">
                                    <label style="display: flex; align-items: center; gap: 10px; cursor: pointer;">
                                        <input type="checkbox" id="config-headless" 
                                               ${configuration.headless ? 'checked' : ''}> 
                                        <span>Headless Mode</span>
                                    </label>
                                </div>
                                <div class="form-group">
                                    <label style="display: flex; align-items: center; gap: 10px; cursor: pointer;">
                                        <input type="checkbox" id="config-video" 
                                               ${configuration.enableVideo ? 'checked' : ''}> 
                                        <span>Enable Video Recording</span>
                                    </label>
                                </div>
                                <div class="form-group">
                                    <label style="display: flex; align-items: center; gap: 10px; cursor: pointer;">
                                        <input type="checkbox" id="config-screenshots" 
                                               ${configuration.enableScreenshots ? 'checked' : ''}> 
                                        <span>Enable Screenshots</span>
                                    </label>
                                </div>
                                <div class="form-group">
                                    <label style="display: flex; align-items: center; gap: 10px; cursor: pointer;">
                                        <input type="checkbox" id="config-self-healing" 
                                               ${configuration.enableSelfHealing ? 'checked' : ''}> 
                                        <span>Enable Self-Healing</span>
                                    </label>
                                </div>
                            </div>
                        </div>
                    </form>
                </div>
            `;
        }
    } catch (error) {
        showError('Failed to load configuration');
    }
}

async function saveConfiguration() {
    const updatedConfig = {
        automationFramework: document.getElementById('config-framework').value,
        browser: document.getElementById('config-browser').value,
        environment: document.getElementById('config-environment').value,
        executionMode: document.getElementById('config-execution-mode').value,
        baseUrl: document.getElementById('config-base-url').value,
        timeoutInSeconds: parseInt(document.getElementById('config-timeout').value),
        maxRetryCount: parseInt(document.getElementById('config-retry').value),
        headless: document.getElementById('config-headless').checked,
        enableVideo: document.getElementById('config-video').checked,
        enableScreenshots: document.getElementById('config-screenshots').checked,
        enableSelfHealing: document.getElementById('config-self-healing').checked,
        // Keep other settings from original config
        ...configuration
    };

    try {
        const response = await fetch(`${API_BASE_URL}/configuration`, {
            method: 'PUT',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(updatedConfig)
        });

        const data = await response.json();

        if (data.success) {
            showSuccess('Configuration saved successfully!');
            configuration = data.configuration;
        } else {
            showError(data.error);
        }
    } catch (error) {
        showError('Failed to save configuration: ' + error.message);
    }
}

// Results View
async function loadResultsView() {
    const view = document.getElementById('results-view');
    
    view.innerHTML = `
        <div class="header">
            <h2><i class="fas fa-chart-bar"></i> Test Results</h2>
            <div class="header-actions">
                <button class="btn btn-secondary" onclick="loadResultsView()">
                    <i class="fas fa-sync"></i> Refresh
                </button>
            </div>
        </div>

        <div class="stats-grid">
            <div class="stat-card">
                <div class="stat-icon success">
                    <i class="fas fa-check-circle"></i>
                </div>
                <div class="stat-details">
                    <h3 id="results-total-passed">0</h3>
                    <p>Passed Tests</p>
                </div>
            </div>
            
            <div class="stat-card">
                <div class="stat-icon danger">
                    <i class="fas fa-times-circle"></i>
                </div>
                <div class="stat-details">
                    <h3 id="results-total-failed">0</h3>
                    <p>Failed Tests</p>
                </div>
            </div>
            
            <div class="stat-card">
                <div class="stat-icon info">
                    <i class="fas fa-play-circle"></i>
                </div>
                <div class="stat-details">
                    <h3 id="results-total-executions">0</h3>
                    <p>Total Executions</p>
                </div>
            </div>
            
            <div class="stat-card">
                <div class="stat-icon warning">
                    <i class="fas fa-clock"></i>
                </div>
                <div class="stat-details">
                    <h3 id="results-avg-duration">0s</h3>
                    <p>Avg Duration</p>
                </div>
            </div>
        </div>

        <div class="card">
            <div class="card-header">
                <div class="card-title">Execution History</div>
            </div>
            <div id="results-history-container">
                <div class="spinner"></div>
            </div>
        </div>
    `;
    
    // Load test execution history
    try {
        const response = await fetch(`${API_BASE_URL}/history`);
        const data = await response.json();
        
        if (data.success && data.history && data.history.length > 0) {
            const history = data.history;
            
            // Calculate statistics
            const totalExecutions = history.length;
            const passedTests = history.filter(h => h.status === 'Passed').length;
            const failedTests = history.filter(h => h.status === 'Failed').length;
            
            // Calculate average duration
            const totalDuration = history.reduce((sum, h) => sum + (h.duration || 0), 0);
            const avgDuration = totalExecutions > 0 ? (totalDuration / totalExecutions).toFixed(2) : 0;
            
            // Update stats
            document.getElementById('results-total-passed').textContent = passedTests;
            document.getElementById('results-total-failed').textContent = failedTests;
            document.getElementById('results-total-executions').textContent = totalExecutions;
            document.getElementById('results-avg-duration').textContent = `${avgDuration}s`;
            
            // Display history table
            displayResultsHistory(history);
        } else {
            // No history found
            document.getElementById('results-history-container').innerHTML = `
                <div class="empty-state">
                    <i class="fas fa-history"></i>
                    <h3>No execution history found</h3>
                    <p>Execute some tests to see results here!</p>
                    <button class="btn btn-primary mt-20" onclick="showView('execute')">
                        <i class="fas fa-play"></i> Execute Tests
                    </button>
                </div>
            `;
        }
    } catch (error) {
        console.error('Error loading test results:', error);
        document.getElementById('results-history-container').innerHTML = `
            <div class="empty-state">
                <i class="fas fa-exclamation-triangle"></i>
                <h3>Failed to load test results</h3>
                <p>${error.message}</p>
            </div>
        `;
    }
}

function normalizeScreenshotPath(path) {
    if (!path) return '';
    // Replace backslashes with forward slashes
    let normalized = path.replace(/\\/g, '/');
    // Ensure it starts with /
    if (!normalized.startsWith('/')) {
        normalized = '/' + normalized;
    }
    return normalized;
}

function displayResultsHistory(historyList) {
    const container = document.getElementById('results-history-container');
    
    if (!historyList || historyList.length === 0) {
        container.innerHTML = `
            <div class="empty-state">
                <i class="fas fa-inbox"></i>
                <h3>No test results</h3>
                <p>Start executing tests to see results here</p>
            </div>
        `;
        return;
    }
    
    const html = `
        <div style="overflow-x: auto;">
            <table id="results-table">
                <thead>
                    <tr>
                        <th style="width: 40px;">
                            <input type="checkbox" id="select-all-results" onchange="toggleSelectAllResults(this)">
                        </th>
                        <th>Test Case Name</th>
                        <th>Status</th>
                        <th>Duration</th>
                        <th>Browser</th>
                        <th>Environment</th>
                        <th>Date</th>
                        <th>Evidence</th>
                        <th>Logs</th>
                    </tr>
                </thead>
                <tbody>
                    ${historyList.map((item, index) => {
                        const statusClass = item.status === 'Passed' ? 'success' : 
                                          item.status === 'Failed' ? 'danger' : 'warning';
                        const statusBadgeColor = item.status === 'Passed' ? '#10b981' : 
                                                item.status === 'Failed' ? '#ef4444' : '#f59e0b';
                        
                        const executedDate = new Date(item.executedAt);
                        const formattedDate = executedDate.toLocaleString('en-US', {
                            year: 'numeric',
                            month: '2-digit',
                            day: '2-digit',
                            hour: '2-digit',
                            minute: '2-digit',
                            second: '2-digit',
                            hour12: false
                        });
                        
                        const browser = item.browser || 'Chrome';
                        const environment = item.environment || 'QA';
                        const hasEvidence = item.steps && item.steps.some(s => s.screenshotPath);
                        const hasLogs = item.steps && item.steps.length > 0;
                        
                        return `
                            <tr class="result-row">
                                <td>
                                    <input type="checkbox" class="result-checkbox" value="${index}">
                                </td>
                                <td><strong>${escapeHtml(item.scenarioName)}</strong></td>
                                <td>
                                    <span class="badge" style="background-color: ${statusBadgeColor}; color: white; padding: 4px 12px; border-radius: 4px; font-size: 12px; font-weight: 600;">
                                        ${escapeHtml(item.status.toUpperCase())}
                                    </span>
                                </td>
                                <td>${item.duration ? item.duration + 's' : 'N/A'}</td>
                                <td>${browser}</td>
                                <td>${environment}</td>
                                <td style="font-size: 0.9em;">${formattedDate}</td>
                                <td>
                                    ${hasEvidence ? `
                                        <button class="btn btn-primary btn-sm" onclick="viewEvidence(${index})" style="padding: 6px 16px; font-size: 13px;">
                                            <i class="fas fa-eye"></i> View
                                        </button>
                                    ` : '-'}
                                </td>
                                <td>
                                    ${hasLogs ? `
                                        <button class="btn btn-primary btn-sm" onclick="viewLogs(${index})" style="padding: 6px 16px; font-size: 13px;">
                                            <i class="fas fa-file-alt"></i> View
                                        </button>
                                    ` : '-'}
                                </td>
                            </tr>
                        `;
                    }).join('')}
                </tbody>
            </table>
        </div>
    `;
    
    container.innerHTML = html;
    
    // Store history data globally for access by view functions
    window.testResultsHistory = historyList;
}

function toggleSelectAllResults(checkbox) {
    const checkboxes = document.querySelectorAll('.result-checkbox');
    checkboxes.forEach(cb => cb.checked = checkbox.checked);
}

function viewEvidence(index) {
    const item = window.testResultsHistory[index];
    if (!item || !item.steps) return;
    
    const stepsWithScreenshots = item.steps.filter(s => s.screenshotPath);
    
    if (stepsWithScreenshots.length === 0) {
        showToast('No screenshots available for this test', 'warning');
        return;
    }
    
    const modal = document.createElement('div');
    modal.id = 'evidence-modal';
    modal.className = 'modal-overlay';
    modal.style.cssText = `
        position: fixed;
        top: 0;
        left: 0;
        width: 100%;
        height: 100%;
        background: rgba(0, 0, 0, 0.8);
        display: flex;
        align-items: center;
        justify-content: center;
        z-index: 10000;
        overflow-y: auto;
        padding: 20px;
    `;
    
    modal.innerHTML = `
        <div class="modal-content" onclick="event.stopPropagation()" style="background: white; border-radius: 12px; max-width: 1200px; width: 100%; max-height: 90vh; overflow-y: auto; position: relative;">
            <div style="position: sticky; top: 0; background: white; border-bottom: 2px solid #e5e7eb; padding: 20px; z-index: 1; border-radius: 12px 12px 0 0;">
                <div style="display: flex; justify-content: space-between; align-items: center;">
                    <h2 style="margin: 0; color: #1f2937;">
                        <i class="fas fa-images"></i> Evidence - ${escapeHtml(item.scenarioName)}
                    </h2>
                    <button onclick="closeDynamicModal('evidence-modal')" style="background: #ef4444; color: white; border: none; padding: 10px 20px; border-radius: 6px; cursor: pointer; font-size: 14px; font-weight: 600;">
                        <i class="fas fa-times"></i> Close
                    </button>
                </div>
                <p style="margin: 8px 0 0 0; color: #6b7280;">Step-by-step screenshots from test execution</p>
            </div>
            <div style="padding: 24px;">
                ${stepsWithScreenshots.map((step, idx) => `
                    <div style="margin-bottom: 30px; border: 1px solid #e5e7eb; border-radius: 8px; padding: 16px; background: #f9fafb;">
                        <div style="display: flex; align-items: center; gap: 12px; margin-bottom: 12px;">
                            <div style="background: #3b82f6; color: white; width: 32px; height: 32px; border-radius: 50%; display: flex; align-items: center; justify-content: center; font-weight: bold; font-size: 14px;">
                                ${idx + 1}
                            </div>
                            <div style="flex: 1;">
                                <h3 style="margin: 0; font-size: 16px; color: #1f2937;">${escapeHtml(step.stepName)}</h3>
                                ${step.description ? `<p style="margin: 4px 0 0 0; color: #6b7280; font-size: 13px;">${escapeHtml(step.description)}</p>` : ''}
                            </div>
                            <span class="badge badge-${step.status === 'Passed' ? 'success' : 'danger'}" style="padding: 6px 12px;">
                                <i class="fas fa-${step.status === 'Passed' ? 'check' : 'times'}"></i> ${escapeHtml(step.status)}
                            </span>
                        </div>
                        <div style="text-align: center; background: white; padding: 12px; border-radius: 6px;">
                            <img src="${normalizeScreenshotPath(step.screenshotPath)}" 
                                 alt="Step ${idx + 1} screenshot" 
                                 onclick="openScreenshotModal('${normalizeScreenshotPath(step.screenshotPath)}')"
                                 style="max-width: 100%; cursor: pointer; border-radius: 6px; box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1); transition: transform 0.2s;"
                                 onmouseover="this.style.transform='scale(1.02)'"
                                 onmouseout="this.style.transform='scale(1)'">
                        </div>
                    </div>
                `).join('')}
            </div>
        </div>
    `;
    
    // Click on overlay background to close
    modal.onclick = function(e) {
        if (e.target === modal) {
            closeDynamicModal('evidence-modal');
        }
    };
    
    document.body.appendChild(modal);
}

function viewLogs(index) {
    const item = window.testResultsHistory[index];
    if (!item || !item.steps) return;
    
    const modal = document.createElement('div');
    modal.id = 'logs-modal';
    modal.className = 'modal-overlay';
    modal.style.cssText = `
        position: fixed;
        top: 0;
        left: 0;
        width: 100%;
        height: 100%;
        background: rgba(0, 0, 0, 0.8);
        display: flex;
        align-items: center;
        justify-content: center;
        z-index: 10000;
        overflow-y: auto;
        padding: 20px;
    `;
    
    const totalSteps = item.steps.length;
    const passedSteps = item.steps.filter(s => s.status === 'Passed').length;
    const failedSteps = item.steps.filter(s => s.status === 'Failed').length;
    
    modal.innerHTML = `
        <div class="modal-content" onclick="event.stopPropagation()" style="background: white; border-radius: 12px; max-width: 1400px; width: 100%; max-height: 90vh; overflow-y: auto; position: relative;">
            <div style="position: sticky; top: 0; background: white; border-bottom: 2px solid #e5e7eb; padding: 20px; z-index: 1; border-radius: 12px 12px 0 0;">
                <div style="display: flex; justify-content: space-between; align-items: center;">
                    <div>
                        <h2 style="margin: 0; color: #1f2937;">
                            <i class="fas fa-file-alt"></i> Execution Logs - ${escapeHtml(item.scenarioName)}
                        </h2>
                        <p style="margin: 8px 0 0 0; color: #6b7280;">
                            Module: <strong>${escapeHtml(item.module)}</strong> | 
                            Duration: <strong>${item.duration}s</strong> | 
                            Status: <strong style="color: ${item.status === 'Passed' ? '#10b981' : '#ef4444'};">${item.status}</strong>
                        </p>
                    </div>
                    <button onclick="closeDynamicModal('logs-modal')" style="background: #ef4444; color: white; border: none; padding: 10px 20px; border-radius: 6px; cursor: pointer; font-size: 14px; font-weight: 600;">
                        <i class="fas fa-times"></i> Close
                    </button>
                </div>
                <div style="display: grid; grid-template-columns: repeat(3, 1fr); gap: 12px; margin-top: 16px;">
                    <div style="background: #f3f4f6; padding: 12px; border-radius: 6px; text-align: center;">
                        <div style="font-size: 24px; font-weight: bold; color: #3b82f6;">${totalSteps}</div>
                        <div style="font-size: 13px; color: #6b7280;">Total Steps</div>
                    </div>
                    <div style="background: #f0fdf4; padding: 12px; border-radius: 6px; text-align: center;">
                        <div style="font-size: 24px; font-weight: bold; color: #10b981;">${passedSteps}</div>
                        <div style="font-size: 13px; color: #6b7280;">Passed Steps</div>
                    </div>
                    <div style="background: #fef2f2; padding: 12px; border-radius: 6px; text-align: center;">
                        <div style="font-size: 24px; font-weight: bold; color: #ef4444;">${failedSteps}</div>
                        <div style="font-size: 13px; color: #6b7280;">Failed Steps</div>
                    </div>
                </div>
            </div>
            <div style="padding: 24px;">
                <div style="background: #f9fafb; border-radius: 8px; padding: 16px;">
                    <h3 style="margin: 0 0 16px 0; color: #1f2937; font-size: 16px;">
                        <i class="fas fa-list-ol"></i> Step-by-Step Execution Details
                    </h3>
                    <table style="width: 100%; border-collapse: collapse; background: white; border-radius: 6px; overflow: hidden;">
                        <thead>
                            <tr style="background: #f3f4f6; border-bottom: 2px solid #e5e7eb;">
                                <th style="padding: 12px; text-align: left; font-size: 13px; color: #6b7280; width: 60px;">#</th>
                                <th style="padding: 12px; text-align: left; font-size: 13px; color: #6b7280;">Step Name</th>
                                <th style="padding: 12px; text-align: left; font-size: 13px; color: #6b7280;">Description</th>
                                <th style="padding: 12px; text-align: left; font-size: 13px; color: #6b7280; width: 100px;">Status</th>
                                <th style="padding: 12px; text-align: left; font-size: 13px; color: #6b7280;">Result / Error Details</th>
                            </tr>
                        </thead>
                        <tbody>
                            ${item.steps.map((step, idx) => {
                                const statusColor = step.status === 'Passed' ? '#10b981' : 
                                                  step.status === 'Failed' ? '#ef4444' : '#f59e0b';
                                const statusBg = step.status === 'Passed' ? '#f0fdf4' : 
                                               step.status === 'Failed' ? '#fef2f2' : '#fef3c7';
                                const rowBg = step.status === 'Failed' ? '#fef2f2' : 'white';
                                
                                return `
                                    <tr style="border-bottom: 1px solid #e5e7eb; background: ${rowBg};">
                                        <td style="padding: 16px; font-weight: 600; color: #3b82f6;">${idx + 1}</td>
                                        <td style="padding: 16px;">
                                            <strong style="color: #1f2937;">${escapeHtml(step.stepName)}</strong>
                                        </td>
                                        <td style="padding: 16px; color: #6b7280; font-size: 13px;">
                                            ${step.description ? escapeHtml(step.description) : '-'}
                                        </td>
                                        <td style="padding: 16px;">
                                            <span style="background: ${statusBg}; color: ${statusColor}; padding: 4px 10px; border-radius: 4px; font-size: 12px; font-weight: 600; display: inline-flex; align-items: center; gap: 4px;">
                                                <i class="fas fa-${step.status === 'Passed' ? 'check-circle' : step.status === 'Failed' ? 'times-circle' : 'exclamation-circle'}"></i>
                                                ${escapeHtml(step.status)}
                                            </span>
                                        </td>
                                        <td style="padding: 16px;">
                                            ${step.status === 'Passed' ? 
                                                '<span style="color: #10b981; font-size: 13px;"><i class="fas fa-check"></i> Step executed successfully</span>' : 
                                                step.error ? 
                                                    `<div style="background: #fff7ed; border-left: 3px solid #ef4444; padding: 10px; border-radius: 4px;">
                                                        <div style="color: #ef4444; font-weight: 600; font-size: 13px; margin-bottom: 4px;">
                                                            <i class="fas fa-exclamation-triangle"></i> Error:
                                                        </div>
                                                        <div style="color: #991b1b; font-size: 13px; font-family: 'Courier New', monospace;">
                                                            ${escapeHtml(step.error)}
                                                        </div>
                                                    </div>` : 
                                                '<span style="color: #6b7280; font-size: 13px;">No error details</span>'
                                            }
                                        </td>
                                    </tr>
                                `;
                            }).join('')}
                        </tbody>
                    </table>
                </div>
                ${item.error ? `
                    <div style="margin-top: 20px; background: #fef2f2; border: 2px solid #ef4444; border-radius: 8px; padding: 16px;">
                        <h3 style="margin: 0 0 12px 0; color: #991b1b; font-size: 16px;">
                            <i class="fas fa-exclamation-triangle"></i> Test Execution Error
                        </h3>
                        <pre style="background: white; padding: 12px; border-radius: 4px; color: #991b1b; margin: 0; overflow-x: auto; font-size: 13px;">${escapeHtml(item.error)}</pre>
                    </div>
                ` : ''}
            </div>
        </div>
    `;
    
    // Click on overlay background to close
    modal.onclick = function(e) {
        if (e.target === modal) {
            closeDynamicModal('logs-modal');
        }
    };
    
    document.body.appendChild(modal);
}

function closeDynamicModal(modalId) {
    const modal = document.getElementById(modalId);
    if (modal) {
        modal.remove();
    }
}

function openScreenshotModal(imagePath) {
    // Create modal overlay
    const modal = document.createElement('div');
    modal.id = 'screenshot-modal';
    modal.style.cssText = `
        position: fixed;
        top: 0;
        left: 0;
        width: 100%;
        height: 100%;
        background: rgba(0, 0, 0, 0.9);
        display: flex;
        align-items: center;
        justify-content: center;
        z-index: 10000;
    `;
    
    modal.innerHTML = `
        <div onclick="event.stopPropagation()" style="max-width: 90%; max-height: 90%; position: relative;">
            <button onclick="closeScreenshotModal()" style="position: absolute; top: -40px; right: 0; background: white; border: none; padding: 10px 15px; border-radius: 4px; cursor: pointer; font-size: 16px;">
                <i class="fas fa-times"></i> Close
            </button>
            <img src="${imagePath}" style="max-width: 100%; max-height: 90vh; border-radius: 8px; box-shadow: 0 4px 20px rgba(0,0,0,0.5);">
        </div>
    `;
    
    // Click on overlay background to close
    modal.onclick = function(e) {
        if (e.target === modal) {
            closeScreenshotModal();
        }
    };
    
    document.body.appendChild(modal);
}

function closeScreenshotModal() {
    const modal = document.getElementById('screenshot-modal');
    if (modal) {
        modal.remove();
    }
}

// Documentation View
function loadDocumentationView() {
    const view = document.getElementById('documentation-view');
    
    view.innerHTML = `
        <div class="header">
            <h2><i class="fas fa-book"></i> Documentation</h2>
        </div>

        <div class="card">
            <div class="card-header">
                <div class="card-title">Getting Started</div>
            </div>
            <h3 style="margin-bottom: 15px;">Welcome to Agentic AI Test Management Platform</h3>
            <p style="line-height: 1.6; margin-bottom: 20px;">
                This platform provides a comprehensive solution for creating, managing, and executing automated tests
                without writing code. You can design test scenarios visually, execute them in real-time, and monitor results.
            </p>

            <h4 style="margin: 20px 0 10px 0;">Key Features:</h4>
            <ul style="line-height: 2;">
                <li>? <strong>Zero-Code Test Creation</strong> - Create tests without writing code</li>
                <li>?? <strong>Real-Time Execution</strong> - Execute tests and see live results</li>
                <li>?? <strong>Multi-Browser Support</strong> - Test on Chrome, Firefox, Edge, Safari</li>
                <li>? <strong>Parallel Execution</strong> - Run multiple tests simultaneously</li>
                <li>?? <strong>Self-Healing</strong> - Automatically fix broken locators</li>
                <li>?? <strong>Video Recording</strong> - Record test execution for debugging</li>
            </ul>

            <h4 style="margin: 20px 0 10px 0;">Quick Start:</h4>
            <ol style="line-height: 2;">
                <li>Navigate to <strong>Create Test</strong> to design a new test scenario</li>
                <li>Add actions (Click, Type, Navigate, etc.) and assertions</li>
                <li>Save the scenario</li>
                <li>Go to <strong>Execute Tests</strong> and run your scenario</li>
                <li>Monitor execution in real-time through the console</li>
            </ol>

            <h4 style="margin: 20px 0 10px 0;">Need Help?</h4>
            <p style="line-height: 1.6;">
                Check out the configuration guide, API documentation, and examples in the GitHub repository.
            </p>
        </div>
    `;
}

// Utility Functions
async function viewScenario(module, name) {
    try {
        const response = await fetch(`${API_BASE_URL}/scenarios/${module}/${name}`);
        const data = await response.json();
        
        if (data.success) {
            const scenario = data.scenario;
            
            showModal('Scenario Details', `
                <div>
                    <div style="margin-bottom: 20px;">
                        <strong>Name:</strong> ${scenario.name}<br>
                        <strong>Module:</strong> <span class="badge badge-primary">${scenario.module}</span><br>
                        <strong>Description:</strong> ${scenario.description || 'N/A'}<br>
                        <strong>Start URL:</strong> ${scenario.startUrl}<br>
                        <strong>Tags:</strong> ${scenario.tags.map(t => `<span class="badge badge-info">${t}</span>`).join(' ')}
                    </div>

                    <h4>Actions (${scenario.actions.length}):</h4>
                    <div style="max-height: 300px; overflow-y: auto;">
                        ${scenario.actions.map(action => `
                            <div class="action-item" style="margin-bottom: 10px;">
                                <div>
                                    <div class="action-type">${action.actionType}</div>
                                    <div class="action-details">
                                        ${action.locator} ${action.value ? `? ${action.value}` : ''}
                                    </div>
                                </div>
                            </div>
                        `).join('')}
                    </div>

                    <h4 style="margin-top: 20px;">Assertions (${scenario.assertions.length}):</h4>
                    <div style="max-height: 200px; overflow-y: auto;">
                        ${scenario.assertions.map(assertion => `
                            <div class="action-item" style="margin-bottom: 10px;">
                                <div>
                                    <div class="action-type">${assertion.type}</div>
                                    <div class="action-details">
                                        ${assertion.locator} ${assertion.expectedValue ? `? ${assertion.expectedValue}` : ''}
                                    </div>
                                </div>
                            </div>
                        `).join('')}
                    </div>

                    <div style="margin-top: 20px;">
                        <button class="btn btn-success" onclick="executeScenario('${module}', '${name}'); closeModal();">
                            <i class="fas fa-play"></i> Execute Now
                        </button>
                    </div>
                </div>
            `);
        }
    } catch (error) {
        showError('Failed to load scenario details');
    }
}

async function deleteScenario(module, name) {
    if (!confirm(`Are you sure you want to delete "${name}"?`)) {
        return;
    }
    
    try {
        const response = await fetch(`${API_BASE_URL}/scenarios/${module}/${name}`, {
            method: 'DELETE'
        });
        
        const data = await response.json();
        
        if (data.success) {
            showSuccess('Scenario deleted successfully');
            loadScenariosView();
        } else {
            showError(data.error);
        }
    } catch (error) {
        showError('Failed to delete scenario');
    }
}

// Utility Functions - Better UI Notifications
function showModal(title, content) {
    document.getElementById('modal-title').textContent = title;
    document.getElementById('modal-body').innerHTML = content;
    document.getElementById('modal').classList.add('active');
}

function closeModal() {
    document.getElementById('modal').classList.remove('active');
}

// Enhanced notification system with toast messages
function showSuccess(message) {
    showNotification(message, 'success');
}

function showError(message) {
    showNotification(message, 'error');
}

function showWarning(message) {
    showNotification(message, 'warning');
}

function showInfo(message) {
    showNotification(message, 'info');
}

function showNotification(message, type = 'info') {
    // Remove existing notification if any
    const existing = document.getElementById('toast-notification');
    if (existing) {
        existing.remove();
    }

    // Create notification element
    const notification = document.createElement('div');
    notification.id = 'toast-notification';
    notification.className = `toast-notification toast-${type}`;
    
    const icons = {
        success: 'fa-check-circle',
        error: 'fa-exclamation-circle',
        warning: 'fa-exclamation-triangle',
        info: 'fa-info-circle'
    };
    
    const icon = icons[type] || icons.info;
    
    notification.innerHTML = `
        <div style="display: flex; align-items: center; gap: 15px;">
            <i class="fas ${icon}" style="font-size: 1.5em;"></i>
            <div style="flex: 1;">
                <div style="font-weight: 600; margin-bottom: 5px;">
                    ${type.charAt(0).toUpperCase() + type.slice(1)}
                </div>
                <div>${message}</div>
            </div>
            <button onclick="this.parentElement.parentElement.remove()" 
                    style="background: none; border: none; color: inherit; cursor: pointer; font-size: 1.2em;">
                <i class="fas fa-times"></i>
            </button>
        </div>
    `;
    
    document.body.appendChild(notification);
    
    // Auto-remove after 5 seconds
    setTimeout(() => {
        if (notification && notification.parentElement) {
            notification.classList.add('toast-fade-out');
            setTimeout(() => notification.remove(), 300);
        }
    }, 5000);
}

// Show confirmation dialog
function showConfirm(message, onConfirm, onCancel) {
    showModal('Confirm Action', `
        <div style="padding: 20px 0;">
            <p style="font-size: 1.1em; line-height: 1.6; margin-bottom: 30px;">${message}</p>
            <div style="display: flex; gap: 15px; justify-content: flex-end;">
                <button class="btn btn-secondary" onclick="closeModal(); ${onCancel ? onCancel + '()' : ''}">
                    <i class="fas fa-times"></i> Cancel
                </button>
                <button class="btn btn-primary" onclick="closeModal(); ${onConfirm}()">
                    <i class="fas fa-check"></i> Confirm
                </button>
            </div>
        </div>
    `);
}

// Enhanced error handler with details
function handleError(error, context = '') {
    console.error(`Error in ${context}:`, error);
    
    let errorMessage = error.message || 'An unknown error occurred';
    
    // Provide helpful error messages
    if (error.message && error.message.includes('fetch')) {
        errorMessage = 'Cannot connect to server. Make sure the Web UI is running.';
    } else if (error.message && error.message.includes('JSON')) {
        errorMessage = 'Server returned invalid data. Please check the backend logs.';
    } else if (error.message && error.message.includes('Playwright')) {
        errorMessage = 'Playwright error. Make sure Playwright browsers are installed. Run: .\\InstallPlaywrightBrowsers.ps1';
    }
    
    showError(context ? `${context}: ${errorMessage}` : errorMessage);
}

// Loading overlay functions
function showLoading(message) {
    const overlay = document.createElement('div');
    overlay.id = 'loading-overlay';
    overlay.className = 'loading-overlay';
    overlay.innerHTML = `
        <div class="loading-content">
            <div class="spinner"></div>
            <div class="loading-message">${message || 'Loading...'}</div>
        </div>
    `;
    
    document.body.appendChild(overlay);
}

function hideLoading() {
    const overlay = document.getElementById('loading-overlay');
    if (overlay) {
        overlay.remove();
    }
}
