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
    switch (viewName) {
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

            // Update execution stats - these IDs match the dashboard HTML
            document.getElementById('total-passed').textContent = passedTests;
            document.getElementById('total-failed').textContent = failedTests;

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

            // Load execution history to show last run logs per scenario
            let historyList = [];
            try {
                const historyResponse = await fetch(`${API_BASE_URL}/history`);
                const historyData = await historyResponse.json();
                if (historyData.success) {
                    historyList = historyData.history || [];
                }
            } catch (e) {
                console.warn('Could not load history for scenarios view', e);
            }

            // Display scenarios with history
            displayAllScenarios(scenarios, historyList);
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

        <div class="card" style="background: linear-gradient(135deg, rgba(102, 126, 234, 0.15), rgba(118, 75, 162, 0.15)); border: 2px solid var(--primary-color); box-shadow: 0 8px 16px rgba(102, 126, 234, 0.2);">
            <div style="text-align: center; padding: 25px;">
                <i class="fas fa-circle-dot" style="font-size: 3.5em; color: var(--primary-color); margin-bottom: 20px; filter: drop-shadow(0 4px 6px rgba(102, 126, 234, 0.3));"></i>
                <h3 style="color: #1f2937; margin-bottom: 15px; font-size: 1.6em; font-weight: 700;">True Record & Playback Experience</h3>
                <p style="color: #374151; line-height: 1.8; font-size: 1.05em; font-weight: 500;">
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

            <div style="background: linear-gradient(135deg, rgba(16, 185, 129, 0.12), rgba(5, 150, 105, 0.08)); padding: 20px; border-radius: 10px; margin-bottom: 20px; border-left: 4px solid var(--success-color); box-shadow: 0 4px 8px rgba(16, 185, 129, 0.15);">
                <strong style="color: #047857; font-size: 1.1em; display: flex; align-items: center; gap: 8px; margin-bottom: 12px;">
                    <i class="fas fa-lightbulb" style="color: #10b981;"></i> How It Works:
                </strong>
                <ul style="margin-top: 10px; padding-left: 30px; line-height: 2; color: #1f2937; font-weight: 500;">
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

        <!-- DATA-DRIVEN EXECUTION -->
        <div class="card" style="border:2px solid var(--info-color);box-shadow:0 8px 24px rgba(59,130,246,0.15);">
            <div class="card-header" style="background:linear-gradient(135deg,rgba(59,130,246,0.08),rgba(37,99,235,0.04));border-radius:12px 12px 0 0;">
                <div class="card-title" style="display:flex;align-items:center;gap:10px;color:var(--info-color);">
                    <i class="fas fa-table"></i>
                    <span>Data-Driven Execution</span>
                </div>
                <span class="badge badge-info"><i class="fas fa-flask"></i> CSV / JSON</span>
            </div>
            <div style="padding:4px 0 16px;">
                <p style="color:#6b7280;font-size:0.92em;line-height:1.7;margin-bottom:18px;">
                    Run a scenario once per data row. Use <code style="background:#f3f4f6;padding:2px 6px;border-radius:4px;">\${ColumnName}</code>
                    placeholders in your scenario action values (e.g. <code style="background:#f3f4f6;padding:2px 6px;border-radius:4px;">\${username}</code>).
                </p>
                <div class="grid-2">
                    <div class="form-group"><label>Module</label><select class="form-control" id="dd-module" onchange="loadDDScenarios()"><option value="">Select Module</option></select></div>
                    <div class="form-group"><label>Scenario</label><select class="form-control" id="dd-scenario"><option value="">Select Scenario</option></select></div>
                </div>
                <div class="grid-2">
                    <div class="form-group"><label>Data Format</label><select class="form-control" id="dd-format"><option value="CSV">CSV (comma-separated)</option><option value="JSON">JSON (array of objects)</option></select></div>
                    <div class="form-group" style="display:flex;align-items:flex-end;gap:8px;">
                        <input type="file" id="dd-file-upload" accept=".csv,.json" style="display:none;" onchange="handleDDFileUpload(event)">
                        <button class="btn btn-secondary" style="flex:1;" onclick="document.getElementById('dd-file-upload').click()">
                            <i class="fas fa-file-upload"></i> Upload File
                        </button>
                        <button class="btn btn-secondary" style="flex:1;" onclick="loadSampleData()">
                            <i class="fas fa-magic"></i> Example
                        </button>
                    </div>
                </div>
                <div class="form-group"><label>Data (paste CSV/JSON or upload a file)</label>
                    <textarea class="form-control" id="dd-data" rows="6" placeholder="username,password&#10;standard_user,secret_sauce&#10;locked_out_user,secret_sauce"></textarea>
                </div>
                <div id="dd-preview-area" class="hidden" style="margin-bottom:16px;padding:14px;background:#f0f9ff;border-radius:8px;border-left:4px solid var(--info-color);">
                    <div style="font-weight:600;color:var(--info-color);margin-bottom:8px;"><i class="fas fa-columns"></i> Preview</div>
                    <div id="dd-preview-content"></div>
                </div>
                <div style="display:flex;gap:12px;flex-wrap:wrap;">
                    <button class="btn" style="background:#e0f2fe;color:#0369a1;border:1px solid #7dd3fc;" onclick="previewDataSet()"><i class="fas fa-eye"></i> Preview Data</button>
                    <button class="btn btn-primary" id="dd-execute-btn" onclick="executeDataDriven()"><i class="fas fa-play"></i> Execute Data-Driven Test</button>
                </div>
                <div id="dd-results-area" class="hidden" style="margin-top:20px;">
                    <div id="dd-summary-bar" style="padding:14px;border-radius:8px;margin-bottom:16px;"></div>
                    <div id="dd-results-table"></div>
                </div>
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

            document.getElementById('exec-module').innerHTML = '<option value="">Select Module</option>' + moduleOptions;
            document.getElementById('exec-module-all').innerHTML = '<option value="">Select Module</option>' + moduleOptions;

            // Populate data-driven module dropdown
            const ddModuleEl = document.getElementById('dd-module');
            if (ddModuleEl) ddModuleEl.innerHTML = '<option value="">Select Module</option>' + moduleOptions;
        }

        const tagsResponse = await fetch(`${API_BASE_URL}/scenarios/tags`);
        const tagsData = await tagsResponse.json();

        if (tagsData.success) {
            const tagOptions = tagsData.tags.map(t =>
                `<option value="${t}">${t}</option>`
            ).join('');

            document.getElementById('exec-tag').innerHTML = '<option value="">Select Tag</option>' + tagOptions;
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

// Enhanced Results View with Filters and Advanced Features
async function loadResultsView() {
    const view = document.getElementById('results-view');

    view.innerHTML = `
        <div class="header">
            <h2><i class="fas fa-chart-bar"></i> Test Results</h2>
            <div class="header-actions">
                <button class="btn btn-secondary" onclick="refreshTestResults()">
                    <i class="fas fa-sync"></i> Refresh
                </button>
                <button class="btn btn-primary" onclick="exportTestResults()">
                    <i class="fas fa-download"></i> Export
                </button>
            </div>
        </div>

        <!-- Summary Statistics Cards -->
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

        <!-- Filters Section -->
        <div class="card">
            <div class="card-header">
                <div class="card-title">
                    <i class="fas fa-filter"></i> Filters
                </div>
                <button class="btn btn-secondary btn-sm" onclick="clearAllTestFilters()">
                    <i class="fas fa-times"></i> Clear All
                </button>
            </div>
            <div style="padding: 0 25px 25px 25px;">
                <div class="grid-4" style="gap: 15px;">
                    <div class="form-group" style="margin: 0;">
                        <label>Status</label>
                        <select class="form-control" id="filter-status" onchange="applyTestFilters()">
                            <option value="">All</option>
                            <option value="Passed">Passed</option>
                            <option value="Failed">Failed</option>
                            <option value="Skipped">Skipped</option>
                        </select>
                    </div>
                    <div class="form-group" style="margin: 0;">
                        <label>Browser</label>
                        <select class="form-control" id="filter-browser" onchange="applyTestFilters()">
                            <option value="">All</option>
                        </select>
                    </div>
                    <div class="form-group" style="margin: 0;">
                        <label>Environment</label>
                        <select class="form-control" id="filter-environment" onchange="applyTestFilters()">
                            <option value="">All</option>
                        </select>
                    </div>
                    <div class="form-group" style="margin: 0;">
                        <label>Date Range</label>
                        <select class="form-control" id="filter-date-range" onchange="applyTestFilters()">
                            <option value="all">All Time</option>
                            <option value="today">Today</option>
                            <option value="last7days" selected>Last 7 Days</option>
                            <option value="last30days">Last 30 Days</option>
                        </select>
                    </div>
                </div>
            </div>
        </div>

        <!-- Execution History Table -->
        <div class="card">
            <div class="card-header">
                <div class="card-title">
                    <i class="fas fa-history"></i> Execution History
                </div>
                <div id="bulk-actions-toolbar" style="display: flex; gap: 10px;">
                    <button class="btn btn-danger" onclick="deleteSelectedTests()">
                        <i class="fas fa-trash"></i> Delete
                    </button>
                </div>
            </div>
            <div id="results-history-container">
                <div class="spinner"></div>
            </div>
        </div>
    `;

    // Add grid-4 CSS if not already added
    if (!document.getElementById('test-results-grid-css')) {
        const style = document.createElement('style');
        style.id = 'test-results-grid-css';
        style.textContent = `
            .grid-4 {
                display: grid;
                grid-template-columns: repeat(4, 1fr);
            }
            
            @media (max-width: 1200px) {
                .grid-4 {
                    grid-template-columns: repeat(2, 1fr);
                }
            }
            
            @media (max-width: 768px) {
                .grid-4 {
                    grid-template-columns: 1fr;
                }
            }
        `;
        document.head.appendChild(style);
    }

    // Load and display test results
    await loadAndDisplayTestResults();
}

// Global variables for test results filtering and pagination
let allTestHistory = [];
let filteredTestHistory = [];
let currentPage = 1;
const recordsPerPage = 10;

// Load and display test results
async function loadAndDisplayTestResults() {
    try {
        const response = await fetch(`${API_BASE_URL}/history`);
        const data = await response.json();

        if (data.success && data.history && data.history.length > 0) {
            allTestHistory = data.history;

            // Populate filter dropdowns
            populateTestFilterDropdowns();

            // Apply filters and display
            applyTestFilters();
        } else {
            // No history found
            showEmptyTestResults();
        }
    } catch (error) {
        console.error('Error loading test results:', error);
        showTestResultsError(error.message);
    }
}

// Populate filter dropdowns with unique values
function populateTestFilterDropdowns() {
    // Get unique browsers
    const browsers = [...new Set(allTestHistory.map(h => h.browser || 'Chrome'))];
    const browserSelect = document.getElementById('filter-browser');
    browserSelect.innerHTML = '<option value="">All</option>' +
        browsers.map(b => `<option value="${b}">${b}</option>`).join('');

    // Get unique environments
    const environments = [...new Set(allTestHistory.map(h => h.environment || 'QA'))];
    const envSelect = document.getElementById('filter-environment');
    envSelect.innerHTML = '<option value="">All</option>' +
        environments.map(e => `<option value="${e}">${e}</option>`).join('');
}

// Apply filters to test results
function applyTestFilters() {
    const statusFilter = document.getElementById('filter-status').value;
    const browserFilter = document.getElementById('filter-browser').value;
    const envFilter = document.getElementById('filter-environment').value;
    const dateRangeFilter = document.getElementById('filter-date-range').value;

    // Start with all history
    let filtered = [...allTestHistory];

    // Apply status filter
    if (statusFilter) {
        filtered = filtered.filter(h => h.status === statusFilter);
    }

    // Apply browser filter
    if (browserFilter) {
        filtered = filtered.filter(h => (h.browser || 'Chrome') === browserFilter);
    }

    // Apply environment filter
    if (envFilter) {
        filtered = filtered.filter(h => (h.environment || 'QA') === envFilter);
    }

    // Apply date range filter
    if (dateRangeFilter !== 'all') {
        const now = new Date();
        let startDate;

        switch (dateRangeFilter) {
            case 'today':
                startDate = new Date(now.getFullYear(), now.getMonth(), now.getDate());
                break;
            case 'last7days':
                startDate = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
                break;
            case 'last30days':
                startDate = new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000);
                break;
        }

        if (startDate) {
            filtered = filtered.filter(h => new Date(h.executedAt) >= startDate);
        }
    }

    filteredTestHistory = filtered;

    // Update statistics
    updateTestStatistics(filtered);

    // Display filtered results
    displayFilteredTestResults(filtered);
}

// Update statistics cards
function updateTestStatistics(history) {
    const totalExecutions = history.length;
    const passedTests = history.filter(h => h.status === 'Passed').length;
    const failedTests = history.filter(h => h.status === 'Failed').length;

    // Calculate average duration
    const totalDuration = history.reduce((sum, h) => sum + (parseFloat(h.duration) || 0), 0);
    const avgDuration = totalExecutions > 0 ? (totalDuration / totalExecutions).toFixed(2) : 0;

    // Update stats
    document.getElementById('results-total-passed').textContent = passedTests;
    document.getElementById('results-total-failed').textContent = failedTests;
    document.getElementById('results-total-executions').textContent = totalExecutions;
    document.getElementById('results-avg-duration').textContent = `${avgDuration}s`;
}

// Display filtered test results with pagination
function displayFilteredTestResults(history) {
    const container = document.getElementById('results-history-container');

    if (history.length === 0) {
        container.innerHTML = `
            <div class="empty-state">
                <i class="fas fa-filter"></i>
                <h3>No results match your filters</h3>
                <p>Try adjusting your filter criteria or clear all filters</p>
                <button class="btn btn-primary mt-20" onclick="clearAllTestFilters()">
                    <i class="fas fa-times"></i> Clear Filters
                </button>
            </div>
        `;
        return;
    }

    // Reset to page 1 when filters change
    currentPage = 1;
    displayResultsHistory(history);
}

// Clear all filters
function clearAllTestFilters() {
    document.getElementById('filter-status').selectedIndex = 0;
    document.getElementById('filter-browser').selectedIndex = 0;
    document.getElementById('filter-environment').selectedIndex = 0;
    document.getElementById('filter-date-range').value = 'last7days';
    applyTestFilters();
}

// Refresh test results
async function refreshTestResults() {
    showInfo('Refreshing test results...');
    await loadAndDisplayTestResults();
    showSuccess('Test results refreshed');
}

// Export test results to CSV
function exportTestResults() {
    if (filteredTestHistory.length === 0) {
        showWarning('No data to export');
        return;
    }

    // CSV headers
    const headers = ['Test Name', 'Status', 'Duration (s)', 'Browser', 'Environment', 'Executed At', 'Module'];

    // CSV rows
    const rows = filteredTestHistory.map(item => [
        item.scenarioName || 'Unknown',
        item.status || 'Unknown',
        item.duration || '0',
        item.browser || 'Chrome',
        item.environment || 'QA',
        item.executedAt ? new Date(item.executedAt).toISOString() : '',
        item.module || 'N/A'
    ]);

    // Combine headers and rows
    const csvContent = [
        headers.join(','),
        ...rows.map(row => row.map(cell => `"${cell}"`).join(','))
    ].join('\n');

    // Create download link
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const link = document.createElement('a');
    const url = URL.createObjectURL(blob);

    link.setAttribute('href', url);
    link.setAttribute('download', `test-results-${new Date().toISOString().split('T')[0]}.csv`);
    link.style.visibility = 'hidden';

    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);

    showSuccess(`Exported ${filteredTestHistory.length} test results to CSV`);
}

// Show empty state
function showEmptyTestResults() {
    const container = document.getElementById('results-history-container');
    container.innerHTML = `
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

// Show error state
function showTestResultsError(message) {
    const container = document.getElementById('results-history-container');
    container.innerHTML = `
        <div class="empty-state">
            <i class="fas fa-exclamation-triangle"></i>
            <h3>Failed to load test results</h3>
            <p>${escapeHtml(message)}</p>
            <button class="btn btn-primary mt-20" onclick="refreshTestResults()">
                <i class="fas fa-sync"></i> Try Again
            </button>
        </div>
    `;
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

    // Calculate pagination
    const totalRecords = historyList.length;
    const totalPages = Math.ceil(totalRecords / recordsPerPage);
    const startIndex = (currentPage - 1) * recordsPerPage;
    const endIndex = startIndex + recordsPerPage;
    const paginatedHistory = historyList.slice(startIndex, endIndex);

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
                ${paginatedHistory.map((item, paginatedIndex) => {
        // Calculate original index in the full list
        const index = startIndex + paginatedIndex;
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
                                    <input type="checkbox" class="result-checkbox" 
                                           value="${index}" 
                                           data-test-id="${item.scenarioName}_${item.executedAt}"
                                           onchange="handleCheckboxChange()">
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
        
        <!-- Pagination Controls -->
        <div style="display: flex; justify-content: space-between; align-items: center; margin-top: 20px; padding: 15px; background: #f9fafb; border-radius: 8px;">
            <div style="color: #6b7280; font-size: 14px;">
                Showing ${paginatedHistory.length} of ${totalRecords} records
            </div>
            <div style="display: flex; gap: 5px;">
                <button onclick="goToPage(1)" 
                        ${currentPage === 1 ? 'disabled' : ''}
                        style="padding: 8px 12px; border: 1px solid #d1d5db; background: white; border-radius: 4px; cursor: pointer; ${currentPage === 1 ? 'opacity: 0.5; cursor: not-allowed;' : ''}">
                    <i class="fas fa-angle-double-left"></i>
                </button>
                <button onclick="goToPage(${currentPage - 1})" 
                        ${currentPage === 1 ? 'disabled' : ''}
                        style="padding: 8px 12px; border: 1px solid #d1d5db; background: white; border-radius: 4px; cursor: pointer; ${currentPage === 1 ? 'opacity: 0.5; cursor: not-allowed;' : ''}">
                    <i class="fas fa-angle-left"></i> Previous
                </button>
                
                ${generatePageButtons(currentPage, totalPages)}
                
                <button onclick="goToPage(${currentPage + 1})" 
                        ${currentPage === totalPages ? 'disabled' : ''}
                        style="padding: 8px 12px; border: 1px solid #d1d5db; background: white; border-radius: 4px; cursor: pointer; ${currentPage === totalPages ? 'opacity: 0.5; cursor: not-allowed;' : ''}">
                    Next <i class="fas fa-angle-right"></i>
                </button>
                <button onclick="goToPage(${totalPages})" 
                        ${currentPage === totalPages ? 'disabled' : ''}
                        style="padding: 8px 12px; border: 1px solid #d1d5db; background: white; border-radius: 4px; cursor: pointer; ${currentPage === totalPages ? 'opacity: 0.5; cursor: not-allowed;' : ''}">
                    <i class="fas fa-angle-double-right"></i>
                </button>
            </div>
        </div>
    `;

    container.innerHTML = html;

    // Store history data globally for access by view functions
    window.testResultsHistory = historyList;
}

// Generate page number buttons
function generatePageButtons(currentPage, totalPages) {
    let buttons = '';
    const maxButtons = 5;
    let startPage = Math.max(1, currentPage - Math.floor(maxButtons / 2));
    let endPage = Math.min(totalPages, startPage + maxButtons - 1);

    // Adjust start if we're near the end
    if (endPage - startPage < maxButtons - 1) {
        startPage = Math.max(1, endPage - maxButtons + 1);
    }

    for (let i = startPage; i <= endPage; i++) {
        const isActive = i === currentPage;
        buttons += `
            <button onclick="goToPage(${i})" 
                    style="padding: 8px 12px; border: 1px solid ${isActive ? '#3b82f6' : '#d1d5db'}; 
                           background: ${isActive ? '#3b82f6' : 'white'}; 
                           color: ${isActive ? 'white' : '#374151'};
                           border-radius: 4px; cursor: pointer; font-weight: ${isActive ? '600' : '400'};">
                ${i}
            </button>
        `;
    }

    return buttons;
}

// Navigate to specific page
function goToPage(page) {
    const totalPages = Math.ceil(filteredTestHistory.length / recordsPerPage);

    if (page < 1 || page > totalPages) {
        return;
    }

    currentPage = page;
    displayResultsHistory(filteredTestHistory);
}

function toggleSelectAllResults(checkbox) {
    const checkboxes = document.querySelectorAll('.result-checkbox');
    checkboxes.forEach(cb => cb.checked = checkbox.checked);
    handleCheckboxChange();
}

function handleCheckboxChange() {
    // Delete button is always visible, so no need to toggle display
    // This function can be used for other UI updates if needed in the future
}

async function deleteSelectedTests() {
    const checkboxes = document.querySelectorAll('.result-checkbox:checked');
    const count = checkboxes.length;

    if (count === 0) {
        showWarning('Please select at least one test to delete');
        return;
    }

    // Get the test names
    const indices = Array.from(checkboxes).map(cb => parseInt(cb.value));
    const testNames = indices.map(index => {
        const test = window.testResultsHistory[index];
        return test.scenarioName;
    });

    // Create display text for test names
    let testNamesDisplay;
    if (count === 1) {
        testNamesDisplay = `<strong>${escapeHtml(testNames[0])}</strong>`;
    } else if (count <= 3) {
        testNamesDisplay = testNames.map(name => `<strong>${escapeHtml(name)}</strong>`).join(', ');
    } else {
        testNamesDisplay = `${testNames.slice(0, 2).map(name => `<strong>${escapeHtml(name)}</strong>`).join(', ')} and <strong>${count - 2}</strong> more`;
    }

    // Show confirmation modal - simple style matching reference screenshot
    const confirmModal = document.createElement('div');
    confirmModal.id = 'delete-confirmation-modal';
    confirmModal.className = 'modal-overlay';
    confirmModal.style.cssText = `
        position: fixed;
        top: 0;
        left: 0;
        width: 100%;
        height: 100%;
        background: rgba(0, 0, 0, 0.5);
        display: flex;
        align-items: center;
        justify-content: center;
        z-index: 10000;
    `;

    confirmModal.innerHTML = `
        <div class="modal-content" onclick="event.stopPropagation()" style="background: white; border-radius: 6px; max-width: 400px; width: 90%; padding: 0; overflow: hidden; box-shadow: 0 2px 10px rgba(0,0,0,0.1);">
            <div style="padding: 20px; border-bottom: 1px solid #e5e7eb;">
                <div style="display: flex; justify-content: space-between; align-items: center;">
                    <h3 style="margin: 0; font-size: 1.1em; color: #1f2937;">Confirm Deletion</h3>
                    <button onclick="closeDeleteConfirmation()" style="background: none; border: none; color: #6b7280; cursor: pointer; padding: 4px; font-size: 18px;" title="Close">
                        <i class="fas fa-times"></i>
                    </button>
                </div>
            </div>
            <div style="padding: 20px;">
                <p style="margin: 0 0 20px 0; color: #374151; line-height: 1.6;">
                    Are you sure you want to delete ${testNamesDisplay}?
                </p>
                <div style="display: flex; gap: 10px; justify-content: flex-end;">
                    <button onclick="closeDeleteConfirmation()" style="background: #9ca3af; color: white; border: none; padding: 8px 16px; border-radius: 4px; cursor: pointer; font-size: 0.9em; min-width: 80px;">
                        Cancel
                    </button>
                    <button onclick="confirmDelete()" style="background: #ef4444; color: white; border: none; padding: 8px 16px; border-radius: 4px; cursor: pointer; font-size: 0.9em; min-width: 80px;">
                        Delete
                    </button>
                </div>
            </div>
        </div>
    `;

    // Click on overlay background to close
    confirmModal.onclick = function (e) {
        if (e.target === confirmModal) {
            closeDeleteConfirmation();
        }
    };

    document.body.appendChild(confirmModal);
}

function closeDeleteConfirmation() {
    const modal = document.getElementById('delete-confirmation-modal');
    if (modal) {
        modal.remove();
    }
}

async function confirmDelete() {
    const checkboxes = document.querySelectorAll('.result-checkbox:checked');
    const indices = Array.from(checkboxes).map(cb => parseInt(cb.value));

    // Close confirmation modal
    closeDeleteConfirmation();

    // Show loading
    showInfo('Deleting test results...');

    try {
        // Get the test IDs to delete
        const testsToDelete = indices.map(index => {
            const test = window.testResultsHistory[index];
            return {
                scenarioName: test.scenarioName,
                executedAt: test.executedAt
            };
        });

        // Get test names for success message
        const testNames = indices.map(index => {
            const test = window.testResultsHistory[index];
            return test.scenarioName;
        });

        // Call API to delete tests
        const response = await fetch(`${API_BASE_URL}/history/delete`, {
            method: 'DELETE',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ tests: testsToDelete })
        });

        const data = await response.json();

        if (data.success) {
            // Build success message with scenario names
            let successMessage;
            if (testNames.length === 1) {
                successMessage = `Successfully deleted "${testNames[0]}"`;
            } else if (testNames.length <= 3) {
                successMessage = `Successfully deleted: ${testNames.join(', ')}`;
            } else {
                successMessage = `Successfully deleted ${testNames.length} test results: ${testNames.slice(0, 2).join(', ')} and ${testNames.length - 2} more`;
            }

            showSuccess(successMessage);

            // Reload the test results
            await loadAndDisplayTestResults();

            // Note: Delete button stays visible (always shown)
        } else {
            showError(data.error || 'Failed to delete test results');
        }
    } catch (error) {
        console.error('Error deleting tests:', error);
        showError('Failed to delete test results: ' + error.message);
    }
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
    modal.onclick = function (e) {
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
    modal.onclick = function (e) {
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
        
        <h3 style="margin-bottom: 20px; color: var(--dark);">Recent Test Executions</h3>
        <table>
            <thead>
                <tr>
                    <th>Test Name</th>
                    <th>Module</th>
                    <th>Status</th>
                    <th>Duration</th>
                    <th>Executed At</th>
                </tr>
            </thead>
            <tbody>
                ${history.slice(0, 20).map(item => {
        const statusIcon = item.status === 'Passed' ? '?' : '?';
        const statusClass = item.status === 'Passed' ? 'success' : 'danger';
        const executedDate = item.executedAt ? new Date(item.executedAt).toLocaleString() : 'Unknown';

        return `
                    <tr>
                        <td><strong>${escapeHtml(item.scenarioName || 'Unknown Test')}</strong></td>
                        <td><span class="badge badge-primary">${escapeHtml(item.module || 'N/A')}</span></td>
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

    // Click on overlay background to close
    modal.onclick = function (e) {
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

// ═══════════════════════════════════════════════════════════
// DATA-DRIVEN EXECUTION — JavaScript Functions
// ═══════════════════════════════════════════════════════════

/** Populate dd-scenario dropdown when module changes */
async function loadDDScenarios() {
    const module = document.getElementById('dd-module')?.value;
    if (!module) return;
    try {
        const r = await fetch(`${API_BASE_URL}/scenarios/module/${module}`);
        const d = await r.json();
        if (d.success) {
            document.getElementById('dd-scenario').innerHTML =
                '<option value="">Select Scenario</option>' +
                d.scenarios.map(s => `<option value="${s.name}">${s.name}</option>`).join('');
        }
    } catch (e) { console.error('loadDDScenarios:', e); }
}

/** Handle file upload for data-driven test */
function handleDDFileUpload(event) {
    const file = event.target.files[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = function (e) {
        document.getElementById('dd-data').value = e.target.result;
        // Auto-select format based on extension
        const fmtSelect = document.getElementById('dd-format');
        if (file.name.toLowerCase().endsWith('.json')) {
            fmtSelect.value = 'JSON';
        } else if (file.name.toLowerCase().endsWith('.csv')) {
            fmtSelect.value = 'CSV';
        }
        showSuccess(`Loaded file: ${file.name}`);
    };
    reader.onerror = function () {
        showError('Error reading file');
    };
    reader.readAsText(file);

    // Reset file input so same file can be selected again
    event.target.value = '';
}

/** Load sample CSV into the textarea */
function loadSampleData() {
    const fmt = document.getElementById('dd-format')?.value;
    if (fmt === 'JSON') {
        document.getElementById('dd-data').value =
            JSON.stringify([
                { username: 'standard_user', password: 'secret_sauce', expectedResult: 'success' },
                { username: 'locked_out_user', password: 'secret_sauce', expectedResult: 'failure' }
            ], null, 2);
    } else {
        document.getElementById('dd-data').value =
            'username,password,expectedResult\nstandard_user,secret_sauce,success\nlocked_out_user,secret_sauce,failure';
    }
}

/** Preview the data set columns + row count */
async function previewDataSet() {
    const dataContent = document.getElementById('dd-data')?.value?.trim();
    const dataFormat = document.getElementById('dd-format')?.value || 'CSV';

    if (!dataContent) { showError('Please enter some data first.'); return; }

    try {
        const r = await fetch(`${API_BASE_URL}/datadriven/preview`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ dataFormat, dataContent })
        });
        const d = await r.json();

        const area = document.getElementById('dd-preview-area');
        const content = document.getElementById('dd-preview-content');

        if (d.success) {
            area.classList.remove('hidden');
            content.innerHTML = `
                <div style="margin-bottom:10px;">
                    <strong style="color:#0369a1;">${d.rowCount} row(s)</strong> &nbsp;|&nbsp;
                    Columns: ${d.columns.map(c => `<code style="background:#dbeafe;padding:2px 6px;border-radius:4px;margin:0 2px;">${escapeHtml(c)}</code>`).join(' ')}
                </div>
                <div style="overflow-x:auto;">
                    <table style="font-size:0.85em;border-collapse:collapse;width:100%;">
                        <thead style="background:#bfdbfe;">
                            <tr>${d.columns.map(c => `<th style="padding:6px 10px;text-align:left;border:1px solid #93c5fd;">${escapeHtml(c)}</th>`).join('')}</tr>
                        </thead>
                        <tbody>
                            ${(d.preview || []).map(row => `
                                <tr style="background:#fff;">
                                    ${d.columns.map(c => `<td style="padding:6px 10px;border:1px solid #e2e8f0;">${escapeHtml(row[c] || '')}</td>`).join('')}
                                </tr>`).join('')}
                        </tbody>
                    </table>
                </div>`;
        } else {
            showError('Preview failed: ' + d.error);
        }
    } catch (e) {
        showError('Preview error: ' + e.message);
    }
}

/** Execute data-driven test and display per-row results */
async function executeDataDriven() {
    const module = document.getElementById('dd-module')?.value;
    const scenario = document.getElementById('dd-scenario')?.value;
    const dataContent = document.getElementById('dd-data')?.value?.trim();
    const dataFormat = document.getElementById('dd-format')?.value || 'CSV';

    if (!module || !scenario) { showError('Please select a module and scenario.'); return; }
    if (!dataContent) { showError('Please enter test data.'); return; }

    const btn = document.getElementById('dd-execute-btn');
    btn.disabled = true;
    btn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Running...';

    try {
        addConsoleLog(`[Data-Driven] Starting "${scenario}" with ${dataFormat} data...`, 'info');

        const r = await fetch(`${API_BASE_URL}/datadriven/execute`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ scenarioName: scenario, module, dataFormat, dataContent })
        });
        const d = await r.json();

        if (d.success) {
            addConsoleLog(`[Data-Driven] Completed. Passed: ${d.passed}, Failed: ${d.failed}`, d.failed > 0 ? 'warning' : 'success');
            renderDataDrivenResults(d);
        } else {
            addConsoleLog('[Data-Driven] Execution failed: ' + d.error, 'error');
            showError('Data-driven execution failed: ' + d.error);
        }
    } catch (e) {
        addConsoleLog('[Data-Driven] Error: ' + e.message, 'error');
        showError('Error: ' + e.message);
    } finally {
        btn.disabled = false;
        btn.innerHTML = '<i class="fas fa-play"></i> Execute Data-Driven Test';
    }
}

/** Render per-row results table */
function renderDataDrivenResults(data) {
    const area = document.getElementById('dd-results-area');
    const summary = document.getElementById('dd-summary-bar');
    const table = document.getElementById('dd-results-table');

    area.classList.remove('hidden');

    const allPassed = data.failed === 0;
    summary.style.background = allPassed ? 'rgba(16,185,129,0.12)' : 'rgba(239,68,68,0.1)';
    summary.style.borderLeft = `4px solid ${allPassed ? 'var(--success-color)' : 'var(--danger-color)'}`;
    summary.style.borderRadius = '8px';
    summary.innerHTML = `
        <strong style="font-size:1.05em;">${allPassed ? '✅' : '⚠️'} Data-Driven Results: ${data.scenarioName}</strong>
        &nbsp;&nbsp;
        <span class="badge badge-success">✅ ${data.passed} Passed</span>
        <span class="badge badge-danger" style="margin-left:6px;">❌ ${data.failed} Failed</span>
        <span class="badge badge-info" style="margin-left:6px;">Total: ${data.totalRows} rows</span>`;

    const cols = data.results.length > 0 ? Object.keys(data.results[0].dataRow) : [];
    table.innerHTML = `
        <table style="width:100%;border-collapse:collapse;font-size:0.88em;">
            <thead style="background:#f9fafb;">
                <tr>
                    <th style="padding:10px;border-bottom:2px solid var(--border);text-align:left;">Row #</th>
                    ${cols.map(c => `<th style="padding:10px;border-bottom:2px solid var(--border);text-align:left;">${escapeHtml(c)}</th>`).join('')}
                    <th style="padding:10px;border-bottom:2px solid var(--border);text-align:left;">Status</th>
                    <th style="padding:10px;border-bottom:2px solid var(--border);text-align:left;">Duration</th>
                    <th style="padding:10px;border-bottom:2px solid var(--border);text-align:left;">Error</th>
                </tr>
            </thead>
            <tbody>
                ${data.results.map(r => {
        const passed = r.status === 'Passed';
        const rowBg = passed ? '' : 'background:rgba(239,68,68,0.04);';
        return `<tr style="${rowBg}">
                        <td style="padding:10px;border-bottom:1px solid var(--border);">${r.rowNumber}</td>
                        ${cols.map(c => `<td style="padding:10px;border-bottom:1px solid var(--border);">${escapeHtml(r.dataRow[c] || '')}</td>`).join('')}
                        <td style="padding:10px;border-bottom:1px solid var(--border);">
                            <span class="badge badge-${passed ? 'success' : 'danger'}">${passed ? '✅ Passed' : '❌ Failed'}</span>
                        </td>
                        <td style="padding:10px;border-bottom:1px solid var(--border);">${escapeHtml(r.duration)}</td>
                        <td style="padding:10px;border-bottom:1px solid var(--border);color:var(--danger-color);font-size:0.85em;">${escapeHtml(r.errorMessage || '')}</td>
                    </tr>`;
    }).join('')}
            </tbody>
        </table>`;
}
