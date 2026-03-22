// AI Test Generator View Loader
function loadAITestGenView() {
    const view = document.getElementById('aitestgen-view');
    view.innerHTML = `
        <div class="header">
            <h2><i class="fas fa-robot"></i> AI Test Generator</h2>
        </div>
        <div class="card">
            <div class="card-header">
                <div class="card-title">Generate Tests from URL</div>
            </div>
            <div class="form-group">
                <label for="ai-url-input">Website URL</label>
                <input type="text" class="form-control" id="ai-url-input" placeholder="Enter website URL...">
            </div>
            <button class="btn btn-primary" id="ai-analyze-btn"><i class="fas fa-search"></i> Analyze</button>
            <button class="btn btn-secondary" id="ai-generate-btn" disabled><i class="fas fa-magic"></i> Generate Test Cases</button>
            <div id="ai-progress-loader" style="display:none;margin-top:10px;">
                <div class="spinner"></div>
                <span>Analyzing...</span>
            </div>
            <div id="ai-results-panel" style="margin-top:20px;"></div>
        </div>
    `;

    // Event listeners and backend integration
    const analyzeBtn = document.getElementById('ai-analyze-btn');
    const generateBtn = document.getElementById('ai-generate-btn');
    const urlInput = document.getElementById('ai-url-input');
    const loader = document.getElementById('ai-progress-loader');
    const resultsPanel = document.getElementById('ai-results-panel');

    let extractedElements = [];

    analyzeBtn.onclick = async () => {
        const url = urlInput.value.trim();
        if (!url) {
            showWarning('Please enter a website URL.');
            return;
        }
        loader.style.display = 'block';
        resultsPanel.innerHTML = '';
        generateBtn.disabled = true;
        try {
            const response = await fetch('http://localhost:8000/aitestgen/analyze-url', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ url })
            });
            const data = await response.json();
            loader.style.display = 'none';
            if (data.success && data.elements) {
                extractedElements = data.elements;
                resultsPanel.innerHTML = `<pre>${JSON.stringify(data.elements, null, 2)}</pre>`;
                generateBtn.disabled = false;
            } else {
                resultsPanel.innerHTML = '<span class="text-danger">Failed to analyze URL.</span>';
            }
        } catch (err) {
            loader.style.display = 'none';
            resultsPanel.innerHTML = '<span class="text-danger">Error analyzing URL.</span>';
        }
    };

    generateBtn.onclick = async () => {
        loader.style.display = 'block';
        resultsPanel.innerHTML = '';
        try {
            const response = await fetch('http://localhost:8000/aitestgen/generate-tests', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ elements: extractedElements })
            });
            const data = await response.json();
            loader.style.display = 'none';
            if (data.success && data.test_cases) {
                // Format test cases in a friendly way
                let html = '';
                data.test_cases.forEach(tc => {
                    html += `<div class="test-case-card">
                        <h5>${tc.name || tc.id}</h5>
                        <ul>`;
                    tc.steps.forEach(step => {
                        html += `<li>${step}</li>`;
                    });
                    html += `</ul>
                        <div><b>Expected:</b> ${tc.expected}</div>
                    </div><hr/>`;
                });
                resultsPanel.innerHTML = html;
            } else {
                resultsPanel.innerHTML = '<span class="text-danger">Failed to generate test cases.</span>';
            }
        } catch (err) {
            loader.style.display = 'none';
            resultsPanel.innerHTML = '<span class="text-danger">Error generating test cases.</span>';
        }
    };
}
// API Configuration
const API_BASE_URL = '/api';

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
        .withUrl("/testExecutionHub", {
            transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.ServerSentEvents | signalR.HttpTransportType.LongPolling
        })
        .withAutomaticReconnect([0, 2000, 5000, 10000])
        .configureLogging(signalR.LogLevel.None)
        .build();

    connection.on("ReceiveRecordedAction", (action) => {
        console.log("?? Recorded Action Captured:", action);
        updateRecordingTable(action);
    });

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

    connection.onreconnecting(() => {
        console.warn('SignalR reconnecting...');
    });

    connection.onreconnected(() => {
        console.log('? SignalR reconnected');
    });

    connection.onclose(() => {
        console.warn('SignalR disconnected. Retrying in background...');
        window.setTimeout(() => {
            if (connection && connection.state === signalR.HubConnectionState.Disconnected) {
                connection.start().catch(() => {
                    console.warn('SignalR retry failed. UI continues without real-time updates.');
                });
            }
        }, 3000);
    });

    try {
        await connection.start();
        console.log("? SignalR Connected");
    } catch (err) {
        console.warn("SignalR not connected at startup:", err?.message || err);
        showWarning('Real-time updates unavailable. Some features may not work.');
    }
}

// View Management
window.showView = function(viewName) {
    // Hide all views
    document.querySelectorAll('.view-content').forEach(view => {
        view.classList.add('hidden');
    });

    // Show selected view
    const targetView = document.getElementById(`${viewName}-view`);
    if (!targetView) {
        console.error(`View not found: ${viewName}-view`);
        const dashboardView = document.getElementById('dashboard-view');
        if (dashboardView) {
            dashboardView.classList.remove('hidden');
            loadDashboard();
        }
        showError(`View '${viewName}' is not available.`);
        return;
    }

    targetView.classList.remove('hidden');

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

    try {
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
            case 'aitestgen':
                loadAITestGenView();
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
            case 'cicd':
                loadCICDView();
                break;
            case 'datadriven':
                initDataDrivenTestingView();
                break;
            case 'export':
                loadExportView();
                break;
            case 'documentation':
                loadDocumentationView();
                break;
        }
    } catch (error) {
        console.error(`Failed to load view '${viewName}':`, error);
        showError(`Failed to load ${viewName} view: ${error.message}`);
    }
};  // End of window.showView

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

            // Safely update stats only if elements exist
            const totalScenariosEl = document.getElementById('total-scenarios');
            if (totalScenariosEl) {
                totalScenariosEl.textContent = data.count;
            }

            // Get modules
            const modules = [...new Set(scenarios.map(s => s.module))];
            const totalModulesEl = document.getElementById('total-modules');
            if (totalModulesEl) {
                totalModulesEl.textContent = modules.length;
            }

            // Calculate execution statistics from history
            const history = historyData.history || [];
            const passedTests = history.filter(h => h.status === 'Passed').length;
            const failedTests = history.filter(h => h.status === 'Failed').length;
            const skippedTests = history.filter(h => h.status === 'Skipped').length;

            // Update execution stats
            const totalPassedEl = document.getElementById('total-passed');
            if (totalPassedEl) {
                totalPassedEl.textContent = passedTests;
            }
            
            const totalFailedEl = document.getElementById('total-failed');
            if (totalFailedEl) {
                totalFailedEl.textContent = failedTests;
            }

            // Display pie chart with test results
            displayTestResultsPieChart({ passed: passedTests, failed: failedTests, skipped: skippedTests });
        }
    } catch (error) {
        hideLoading();
        console.error('Error loading dashboard:', error);
        handleError(error, 'Failed to load dashboard data');
    }
}

// Display pie chart with test results
let testResultsChart = null;

function displayTestResultsPieChart(stats) {
    const canvas = document.getElementById('testResultsChart');
    if (!canvas) {
        console.warn('Chart canvas not found, skipping chart render');
        return;
    }
    
    const ctx = canvas.getContext('2d');
    
    // Destroy existing chart if it exists
    if (testResultsChart) {
        testResultsChart.destroy();
    }
    
    // Calculate total
    const total = stats.passed + stats.failed + stats.skipped;
    
    // Safely update legend with counts
    const legendPassedEl = document.getElementById('legend-passed');
    if (legendPassedEl) {
        legendPassedEl.textContent = stats.passed;
    }
    
    const legendFailedEl = document.getElementById('legend-failed');
    if (legendFailedEl) {
        legendFailedEl.textContent = stats.failed;
    }
    
    const legendSkippedEl = document.getElementById('legend-skipped');
    if (legendSkippedEl) {
        legendSkippedEl.textContent = stats.skipped;
    }
    
    // Hide skipped legend if no skipped tests
    const skippedContainer = document.getElementById('legend-skipped-container');
    if (skippedContainer) {
        skippedContainer.style.display = stats.skipped === 0 ? 'none' : 'flex';
    }
    
    // If no data, show empty state
    if (total === 0) {
        ctx.clearRect(0, 0, canvas.width, canvas.height);
        ctx.font = '13px Segoe UI';
        ctx.fillStyle = '#94a3b8';
        ctx.textAlign = 'center';
        ctx.fillText('No test data available', canvas.width / 2, canvas.height / 2);
        return;
    }
    
    // Prepare chart data - only include non-zero values
    const labels = [];
    const chartData = [];
    const colors = [];
    
    if (stats.passed > 0) {
        labels.push('Passed');
        chartData.push(stats.passed);
        colors.push('#10b981');
    }
    
    if (stats.failed > 0) {
        labels.push('Failed');
        chartData.push(stats.failed);
        colors.push('#ef4444');
    }
    
    if (stats.skipped > 0) {
        labels.push('Skipped');
        chartData.push(stats.skipped);
        colors.push('#f59e0b');
    }
    
    // Create pie chart
    testResultsChart = new Chart(ctx, {
        type: 'doughnut',
        data: {
            labels: labels,
            datasets: [{
                data: chartData,
                backgroundColor: colors
            }]
        },
        options: {
            plugins: {
                legend: {
                    display: false
                },
                tooltip: {
                    callbacks: {
                        label: function(context) {
                            const label = context.label || '';
                            const value = context.parsed || 0;
                            const percentage = ((value / total) * 100).toFixed(1);
                            return `${label}: ${value} (${percentage}%)`;
                        }
                    },
                    backgroundColor: 'rgba(30, 41, 59, 0.95)',
                    titleFont: { size: 11, weight: '600' },
                    bodyFont: { size: 10 },
                    padding: 8,
                    cornerRadius: 6
                }
            },
            cutout: '65%',
            animation: {
                animateScale: true,
                animateRotate: true
            }
        }
    });
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


// Global variables for scenarios pagination
let allScenarios = [];
let filteredScenarios = [];
let currentScenariosPage = 1;
const scenariosPerPage = 10;

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
            allScenarios = data.scenarios;
            filteredScenarios = data.scenarios;

            // Populate filters
            const modules = [...new Set(scenarios.map(s => s.module))];
            const tags = [...new Set(scenarios.flatMap(s => s.tags))];

            document.getElementById('filter-module').innerHTML +=
                modules.map(m => `<option value="${m}">${m}</option>`).join('');

            document.getElementById('filter-tag').innerHTML +=
                tags.map(t => `<option value="${t}">${t}</option>`).join('');

            // Reset to page 1
            currentScenariosPage = 1;

            // Display scenarios with pagination
            displayAllScenarios(filteredScenarios);
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

    // Calculate pagination
    const totalRecords = scenariosList.length;
    const totalPages = Math.ceil(totalRecords / scenariosPerPage);
    const startIndex = (currentScenariosPage - 1) * scenariosPerPage;
    const endIndex = startIndex + scenariosPerPage;
    const paginatedScenarios = scenariosList.slice(startIndex, endIndex);

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
    paginatedScenarios.forEach(scenario => {
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

        const actionsWrapper = document.createElement('div');
        actionsWrapper.style.display = 'flex';
        actionsWrapper.style.gap = '8px';
        actionsWrapper.style.alignItems = 'center';
        actionsWrapper.style.flexWrap = 'nowrap';
        actionsWrapper.style.justifyContent = 'flex-start';

        // Base styling for modern, premium minimalist icon buttons
        const baseIconStyle = 'width: 28px; height: 28px; display: inline-flex; align-items: center; justify-content: center; border-radius: 6px; border: none; cursor: pointer; transition: all 0.2s ease; font-size: 13px; background: transparent;';

        // Execute button (Green)
        const executeBtn = document.createElement('button');
        executeBtn.title = 'Execute Scenario';
        executeBtn.style.cssText = baseIconStyle + 'color: #10b981;';
        executeBtn.onmouseover = () => { executeBtn.style.background = '#d1fae5'; };
        executeBtn.onmouseout = () => { executeBtn.style.background = 'transparent'; };
        executeBtn.innerHTML = '<i class="fas fa-play"></i>';
        executeBtn.onclick = () => executeScenario(scenario.module, scenario.name);
        actionsWrapper.appendChild(executeBtn);

        // View button (Gray/Blue)
        const viewBtn = document.createElement('button');
        viewBtn.title = 'View Details';
        viewBtn.style.cssText = baseIconStyle + 'color: #64748b;';
        viewBtn.onmouseover = () => { viewBtn.style.background = '#f1f5f9'; viewBtn.style.color = '#334155'; };
        viewBtn.onmouseout = () => { viewBtn.style.background = 'transparent'; viewBtn.style.color = '#64748b'; };
        viewBtn.innerHTML = '<i class="fas fa-eye"></i>';
        viewBtn.onclick = () => viewScenario(scenario.module, scenario.name);
        actionsWrapper.appendChild(viewBtn);

        // Delete button (Red)
        const deleteBtn = document.createElement('button');
        deleteBtn.title = 'Delete Scenario';
        deleteBtn.style.cssText = baseIconStyle + 'color: #ef4444;';
        deleteBtn.onmouseover = () => { deleteBtn.style.background = '#fee2e2'; };
        deleteBtn.onmouseout = () => { deleteBtn.style.background = 'transparent'; };
        deleteBtn.innerHTML = '<i class="fas fa-trash"></i>';
        deleteBtn.onclick = () => deleteScenario(scenario.module, scenario.name);
        actionsWrapper.appendChild(deleteBtn);

        actionsCell.appendChild(actionsWrapper);

        row.appendChild(actionsCell);
        tbody.appendChild(row);
    });

    // Create pagination controls
    const paginationHTML = `
        <div style="display: flex; justify-content: space-between; align-items: center; margin-top: 20px; padding: 15px; background: #f9fafb; border-radius: 8px;">
            <div style="color: #6b7280; font-size: 14px;">
                Showing ${paginatedScenarios.length} of ${totalRecords} records
            </div>
            <div style="display: flex; gap: 5px;">
                <button onclick="goToScenariosPage(1)" 
                        ${currentScenariosPage === 1 ? 'disabled' : ''}
                        style="padding: 8px 12px; border: 1px solid #d1d5db; background: white; border-radius: 4px; cursor: pointer; ${currentScenariosPage === 1 ? 'opacity: 0.5; cursor: not-allowed;' : ''}">
                    <i class="fas fa-angle-double-left"></i>
                </button>
                <button onclick="goToScenariosPage(${currentScenariosPage - 1})" 
                        ${currentScenariosPage === 1 ? 'disabled' : ''}
                        style="padding: 8px 12px; border: 1px solid #d1d5db; background: white; border-radius: 4px; cursor: pointer; ${currentScenariosPage === 1 ? 'opacity: 0.5; cursor: not-allowed;' : ''}">
                    <i class="fas fa-angle-left"></i> Previous
                </button>
                
                ${generateScenariosPageButtons(currentScenariosPage, totalPages)}
                
                <button onclick="goToScenariosPage(${currentScenariosPage + 1})" 
                        ${currentScenariosPage === totalPages ? 'disabled' : ''}
                        style="padding: 8px 12px; border: 1px solid #d1d5db; background: white; border-radius: 4px; cursor: pointer; ${currentScenariosPage === totalPages ? 'opacity: 0.5; cursor: not-allowed;' : ''}">
                    Next <i class="fas fa-angle-right"></i>
                </button>
                <button onclick="goToScenariosPage(${totalPages})" 
                        ${currentScenariosPage === totalPages ? 'disabled' : ''}
                        style="padding: 8px 12px; border: 1px solid #d1d5db; background: white; border-radius: 4px; cursor: pointer; ${currentScenariosPage === totalPages ? 'opacity: 0.5; cursor: not-allowed;' : ''}">
                    <i class="fas fa-angle-double-right"></i>
                </button>
            </div>
        </div>
    `;

    container.innerHTML = '';
    container.appendChild(table);
    
    // Add pagination controls
    const paginationDiv = document.createElement('div');
    paginationDiv.innerHTML = paginationHTML;
    container.appendChild(paginationDiv);
}

// Generate page number buttons for scenarios
function generateScenariosPageButtons(currentPage, totalPages) {
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
            <button onclick="goToScenariosPage(${i})" 
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

// Navigate to specific scenarios page
function goToScenariosPage(page) {
    const totalPages = Math.ceil(filteredScenarios.length / scenariosPerPage);

    if (page < 1 || page > totalPages) {
        return;
    }

    currentScenariosPage = page;
    displayAllScenarios(filteredScenarios);
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

    let filtered = allScenarios;

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

    filteredScenarios = filtered;
    
    // Reset to page 1 when filters change
    currentScenariosPage = 1;

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
                            <button type="button" class="btn btn-primary btn-icon" onclick="addAction('Check')">
                                <i class="fas fa-check-square"></i> Check
                            </button>
                            <button type="button" class="btn btn-primary btn-icon" onclick="addAction('Uncheck')">
                                <i class="fas fa-square"></i> Uncheck
                            </button>
                            <button type="button" class="btn btn-primary btn-icon" onclick="addAction('Hover')">
                                <i class="fas fa-hand-pointer"></i> Hover
                            </button>
                            <button type="button" class="btn btn-primary btn-icon" onclick="addAction('Scroll')">
                                <i class="fas fa-arrows-alt-v"></i> Scroll
                            </button>
                            <button type="button" class="btn btn-primary btn-icon" onclick="addAction('Select')">
                                <i class="fas fa-list-ul"></i> Select
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
                            <button type="button" class="btn btn-success btn-icon" onclick="addAssertion('ElementNotVisible')">
                                <i class="fas fa-eye-slash"></i> Not Visible
                            </button>
                            <button type="button" class="btn btn-success btn-icon" onclick="addAssertion('ElementExists')">
                                <i class="fas fa-plus-square"></i> Exists
                            </button>
                            <button type="button" class="btn btn-success btn-icon" onclick="addAssertion('ElementNotExists')">
                                <i class="fas fa-minus-square"></i> Not Exists
                            </button>
                            <button type="button" class="btn btn-success btn-icon" onclick="addAssertion('TextEquals')">
                                <i class="fas fa-equals"></i> Text Equals
                            </button>
                            <button type="button" class="btn btn-success btn-icon" onclick="addAssertion('TextContains')">
                                <i class="fas fa-font"></i> Contains
                            </button>
                            <button type="button" class="btn btn-success btn-icon" onclick="addAssertion('TextNotContains')">
                                <i class="fas fa-strikethrough"></i> Not Contains
                            </button>
                            <button type="button" class="btn btn-success btn-icon" onclick="addAssertion('ValueEquals')">
                                <i class="fas fa-keyboard"></i> Value Equals
                            </button>
                            <button type="button" class="btn btn-success btn-icon" onclick="addAssertion('TitleEquals')">
                                <i class="fas fa-window-maximize"></i> Title Equals
                            </button>
                            <button type="button" class="btn btn-success btn-icon" onclick="addAssertion('TitleContains')">
                                <i class="fas fa-window-restore"></i> Title Contains
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

function addAction(type) {
    if (!window.currentActions) window.currentActions = [];
    window.currentActions.push({
        actionType: type,
        locator: '',
        value: ''
    });
    renderActions();
}

function addAssertion(type) {
    if (!window.currentAssertions) window.currentAssertions = [];
    window.currentAssertions.push({
        type: type,
        locator: '',
        expectedValue: '',
        description: ''
    });
    renderAssertions();
}

function renderActions() {
    const list = document.getElementById('actions-list');
    if (!list) return;
    list.innerHTML = window.currentActions.map((action, index) => {
        const noValueActions = ['Hover', 'Scroll', 'Click'];
        const isNoValue = noValueActions.includes(action.actionType);
        const placeholder = action.actionType === 'Select' ? 'Option text or value' : 'Value (optional)';

        return `
            <div class="action-item" style="margin-bottom: 10px; border: 1px solid #e5e7eb; padding: 15px; border-radius: 8px;">
                <div style="flex: 1;">
                    <div class="action-type" style="font-weight: 600; color: var(--primary-color); margin-bottom: 8px;">${action.actionType}</div>
                    <div class="${isNoValue ? 'grid-1' : 'grid-2'}">
                        <input type="text" class="form-control" placeholder="Locator (ID, XPath, CSS)" value="${action.locator || ''}" 
                               onchange="window.currentActions[${index}].locator = this.value">
                        ${!isNoValue ? `
                            <input type="text" class="form-control" placeholder="${placeholder}" value="${action.value || ''}" 
                                   onchange="window.currentActions[${index}].value = this.value">
                        ` : ''}
                    </div>
                </div>
                <button type="button" class="btn btn-danger btn-icon" style="margin-left: 15px;" onclick="removeAction(${index})">
                    <i class="fas fa-trash"></i>
                </button>
            </div>
        `;
    }).join('');
}

function renderAssertions() {
    const list = document.getElementById('assertions-list');
    if (!list) return;
    list.innerHTML = window.currentAssertions.map((assertion, index) => `
        <div class="action-item" style="margin-bottom: 10px; border: 1px solid #e5e7eb; padding: 15px; border-radius: 8px;">
            <div style="flex: 1;">
                <div class="action-type" style="font-weight: 600; color: var(--success-color); margin-bottom: 8px;">${assertion.type}</div>
                <div class="grid-3">
                    <input type="text" class="form-control" placeholder="Locator" value="${assertion.locator || ''}" 
                           onchange="window.currentAssertions[${index}].locator = this.value">
                    <input type="text" class="form-control" placeholder="Expected Value" value="${assertion.expectedValue || ''}" 
                           onchange="window.currentAssertions[${index}].expectedValue = this.value">
                    <input type="text" class="form-control" placeholder="Description" value="${assertion.description || ''}" 
                           onchange="window.currentAssertions[${index}].description = this.value">
                </div>
            </div>
            <button type="button" class="btn btn-danger btn-icon" style="margin-left: 15px;" onclick="removeAssertion(${index})">
                <i class="fas fa-trash"></i>
            </button>
        </div>
    `).join('');
}

function removeAction(index) {
    window.currentActions.splice(index, 1);
    renderActions();
}

function removeAssertion(index) {
    window.currentAssertions.splice(index, 1);
    renderAssertions();
}

async function saveScenario(event) {
    if (event) event.preventDefault();

    const name = document.getElementById('scenario-name').value;
    const module = document.getElementById('scenario-module').value;
    const description = document.getElementById('scenario-description').value;
    const startUrl = document.getElementById('scenario-url').value;
    const tagsStr = document.getElementById('scenario-tags').value;
    const tags = tagsStr ? tagsStr.split(',').map(t => t.trim()).filter(t => t) : [];

    if (!name || !module || !startUrl) {
        showError('Please fill in all required fields');
        return;
    }

    const scenario = {
        name,
        module,
        description,
        startUrl,
        tags,
        actions: window.currentActions,
        assertions: window.currentAssertions
    };

    try {
        showLoading('Saving scenario...');
        const response = await fetch(`${API_BASE_URL}/scenarios`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(scenario)
        });
        const data = await response.json();
        hideLoading();

        if (data.success) {
            showSuccess('Scenario saved successfully!');
            showView('scenarios');
        } else {
            showError(data.error || 'Failed to save scenario');
        }
    } catch (error) {
        hideLoading();
        showError('Error saving scenario: ' + error.message);
    }
}

function clearCreateForm() {
    const form = document.getElementById('create-scenario-form');
    if (form) form.reset();
    window.currentActions = [];
    window.currentAssertions = [];
    renderActions();
    renderAssertions();
}


let currentActions = [];
let currentAssertions = [];
let isRecording = false;
let exportScenarioCache = [];

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

        <div class="card">
            <div class="card-header">
                <div class="card-title" style="display: flex; align-items: center; gap: 10px;">
                    <i class="fas fa-circle-dot" style="color: var(--primary-color);"></i>
                    <span>Record Your Test</span>
                </div>
            </div>

            <div style="margin-bottom: 14px; padding: 10px 12px; border-radius: 8px; background: rgba(59, 130, 246, 0.08); border-left: 3px solid var(--primary-color); color: #1f2937; font-size: 0.92rem;">
                Enter scenario details, click Start Recording, perform actions in the opened browser, then click Stop Recording to save your scenario.
            </div>

            <form id="assisted-record-form" onsubmit="startAssistedRecording(event)">
                <div class="grid-2" style="gap: 10px;">
                    <div class="form-group" style="margin-bottom: 10px;">
                        <label>Scenario Name *</label>
                        <input type="text" class="form-control" id="record-name" 
                               placeholder="e.g., Login_Test" required
                               oninput="window.validateScenarioNameRealtime()">
                        <div id="scenario-name-validation" style="display: none; color: #dc3545; font-size: 0.875rem; margin-top: 6px; line-height: 1.4;"></div>
                    </div>
                    <div class="form-group" style="margin-bottom: 10px;">
                        <label>Module *</label>
                        <input type="text" class="form-control" id="record-module" 
                               placeholder="e.g., Authentication" required>
                    </div>
                </div>

                <div class="form-group" style="margin-bottom: 10px;">
                    <label>Description</label>
                    <textarea class="form-control" id="record-description" rows="2"
                              placeholder="What does this test do?"></textarea>
                </div>

                <div class="form-group" style="margin-bottom: 10px;">
                    <label>Start URL *</label>
                    <input type="url" class="form-control" id="record-url" 
                           placeholder="https://your-application-url.com" required>
                </div>

                <div class="form-group" style="margin-bottom: 12px;">
                    <label>Tags (comma-separated)</label>
                    <input type="text" class="form-control" id="record-tags" 
                           placeholder="smoke, regression">
                </div>

                <div style="display: flex; gap: 10px; align-items: center; flex-wrap: wrap;">
                    <button type="submit" class="btn btn-success" id="start-recording-btn" ${isRecording ? 'disabled' : ''}>
                        <i class="fas fa-circle-dot"></i> Start Recording
                    </button>
                    <button type="button" class="btn btn-danger" id="stop-recording-btn" 
                            onclick="stopRecording()" ${!isRecording ? 'disabled' : ''}>
                        <i class="fas fa-stop-circle"></i> Stop Recording
                    </button>
                </div>
            </form>

            <div id="recording-status" class="hidden" style="margin-top: 12px; padding: 12px; background: rgba(239, 68, 68, 0.1); border-radius: 8px; border-left: 4px solid var(--danger-color);">
                <div style="display: flex; align-items: center; gap: 10px;">
                    <div class="spinner" style="width: 20px; height: 20px; margin: 0;"></div>
                    <strong style="color: var(--danger-color);">Recording in progress...</strong>
                </div>
                <p style="margin-top: 8px; color: #6b7280;">
                    Perform your test actions in the opened browser window and click Stop Recording when done.
                </p>
            </div>

            <div id="recording-script-tools" class="hidden" style="margin-top:12px;padding:14px;border:1px solid var(--border);border-radius:8px;background:#f8fafc;">
                <div style="display:flex;justify-content:space-between;align-items:center;gap:10px;flex-wrap:wrap;">
                    <div style="font-weight:600;color:#1f2937;"><i class="fas fa-code"></i> Generate Scripts from Recorded Scenario</div>
                    <span class="badge badge-info" id="recorded-scenario-badge">No scenario selected</span>
                </div>

                <div class="grid-2" style="margin-top:10px;">
                    <div class="form-group">
                        <label>Framework</label>
                        <select class="form-control" id="script-framework" onchange="updateScriptLanguageOptions()">
                            <option value="playwright">Playwright</option>
                            <option value="selenium">Selenium</option>
                            <option value="cypress">Cypress</option>
                        </select>
                    </div>
                    <div class="form-group">
                        <label>Language</label>
                        <select class="form-control" id="script-language">
                            <option value="csharp">C#</option>
                            <option value="python">Python</option>
                            <option value="javascript">JavaScript</option>
                            <option value="typescript">TypeScript</option>
                        </select>
                    </div>
                </div>

                <div style="display:flex;gap:10px;flex-wrap:wrap;">
                    <button type="button" class="btn btn-primary" onclick="generateRecordedScenarioScript()">
                        <i class="fas fa-wand-magic-sparkles"></i> Generate Script
                    </button>
                    <button type="button" class="btn btn-secondary" onclick="downloadGeneratedScript()">
                        <i class="fas fa-download"></i> Download Script
                    </button>
                </div>

                <div id="generated-script-panel" class="hidden" style="margin-top:10px;">
                    <div style="font-weight:600;margin-bottom:6px;color:#334155;"><i class="fas fa-file-code"></i> Generated Test Script</div>
                    <pre id="generated-script-content" style="max-height:220px;overflow:auto;background:#0f172a;color:#e2e8f0;padding:12px;border-radius:6px;font-size:12px;line-height:1.4;"></pre>
                </div>

                <div id="advanced-concepts-panel" class="hidden" style="margin-top:10px;">
                    <div style="font-weight:600;margin-bottom:6px;color:#334155;"><i class="fas fa-lightbulb"></i> Advanced Concepts & Derived Test Cases</div>
                    <div id="advanced-concepts-content" style="display:grid;gap:8px;"></div>
                </div>
            </div>
        </div>

        <div id="recording-live-actions" class="hidden mt-12">
            <div class="card">
                <div class="card-header">
                    <div class="card-title"><i class="fas fa-list"></i> Recorded Actions (Live)</div>
                    <span class="badge badge-primary" id="live-action-count">0 steps</span>
                </div>
                <div style="max-height: 340px; overflow-y: auto;">
                    <table>
                        <thead>
                            <tr>
                                <th style="width: 44px;">#</th>
                                <th>Step</th>
                                <th>Locator</th>
                                <th>Value</th>
                            </tr>
                        </thead>
                        <tbody id="live-actions-body">
                            <!-- Actions will be added here in real-time -->
                        </tbody>
                    </table>
                </div>
            </div>
        </div>
    `;

    // Reset live actions
    window.recordedActions = [];
    window.liveAutoAssertions = [];
    generatedScriptCache = null;
    updateScriptLanguageOptions();
}

let recordedActions = [];
let liveAutoAssertions = [];
let lastRecordedScenario = null;
let generatedScriptCache = null;

function updateScriptLanguageOptions() {
    const framework = document.getElementById('script-framework')?.value;
    const languageSelect = document.getElementById('script-language');
    if (!languageSelect) return;

    let options = [];
    if (framework === 'selenium') {
        options = [{ value: 'csharp', label: 'C#' }];
    } else if (framework === 'cypress') {
        options = [
            { value: 'javascript', label: 'JavaScript' },
            { value: 'typescript', label: 'TypeScript' }
        ];
    } else {
        options = [
            { value: 'csharp', label: 'C#' },
            { value: 'python', label: 'Python' },
            { value: 'javascript', label: 'JavaScript' },
            { value: 'typescript', label: 'TypeScript' }
        ];
    }

    languageSelect.innerHTML = options.map(o => `<option value="${o.value}">${o.label}</option>`).join('');
}

function setRecordedScenarioForGeneration(name, module) {
    if (!name || !module) return;

    lastRecordedScenario = { name, module };
    const badge = document.getElementById('recorded-scenario-badge');
    if (badge) {
        badge.textContent = `${name} (${module})`;
    }

    const toolsPanel = document.getElementById('recording-script-tools');
    if (toolsPanel) {
        toolsPanel.classList.remove('hidden');
    }
}

function updateRecordingTable(action) {
    if (!window.recordedActions) window.recordedActions = [];
    window.recordedActions.push(action);
    if (!window.liveAutoAssertions) window.liveAutoAssertions = [];

    const rowIndex = window.recordedActions.length - 1;
    const autoAssertions = inferAutoAssertionsFromAction(action, rowIndex);
    window.liveAutoAssertions[rowIndex] = autoAssertions;

    const container = document.getElementById('recording-live-actions');
    if (container) container.classList.remove('hidden');

    const countEl = document.getElementById('live-action-count');
    if (countEl) countEl.textContent = `${window.recordedActions.length} steps`;

    const body = document.getElementById('live-actions-body');
    if (body) {
        const row = document.createElement('tr');
        const actionType = getRecordedActionField(action, 'actionType');
        const locator = getRecordedActionField(action, 'locator') || '-';
        const value = getRecordedActionField(action, 'value') || '-';

        const stepCell = autoAssertion
            ? `<div style="display:flex; flex-direction:column; gap:4px;">
                    <div><span class="badge badge-info">${escapeHtml(actionType || 'Action')}</span></div>
                    <div id="auto-assertion-preview-${rowIndex}" style="display:flex; flex-direction:column; gap:4px; padding:6px 8px; border-radius:6px; background:#f8fafc; border:1px solid #e2e8f0;">
                        <span style="font-size:12px; color:#475569;" title="${escapeHtml(formatAutoAssertionPreview(autoAssertion))}">Assert: ${escapeHtml(formatAutoAssertionPreview(autoAssertion))}</span>
                        <div style="display:flex; gap:8px; align-items:center;">
                            <button class="btn btn-secondary btn-small" style="padding:4px 8px; font-size:11px;" onclick="editLiveAutoAssertion(${rowIndex})">Edit</button>
                            <label style="display:flex; align-items:center; gap:4px; margin:0; font-size:11px; color:#475569;">
                                <input type="checkbox" ${autoAssertion.enabled ? 'checked' : ''} onchange="toggleLiveAutoAssertion(${rowIndex}, this.checked)"> Use
                            </label>
                        </div>
                    </div>
               </div>`
            : `<span class="badge badge-info">${escapeHtml(actionType || 'Action')}</span>`;

        row.innerHTML = `
            <td>${window.recordedActions.length}</td>
            <td style="min-width: 220px;">${stepCell}</td>
            <td style="font-family: monospace; font-size: 0.85em; max-width: 300px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;" title="${escapeHtml(locator)}">${escapeHtml(locator)}</td>
            <td>${escapeHtml(value)}</td>
        `;
        body.appendChild(row);

        // Auto-scroll to bottom
        const scrollContainer = body.parentElement.parentElement;
        scrollContainer.scrollTop = scrollContainer.scrollHeight;
    }
}

function getRecordedActionField(action, fieldName) {
    if (!action || !fieldName) return '';
    const camel = fieldName.charAt(0).toLowerCase() + fieldName.slice(1);
    const pascal = fieldName.charAt(0).toUpperCase() + fieldName.slice(1);
    return action[camel] ?? action[pascal] ?? '';
}

function inferAutoAssertionsFromAction(action, actionIndex) {
    const assertions = [];
    const actionTypeRaw = (getRecordedActionField(action, 'actionType') || '').trim();
    const actionType = actionTypeRaw.toLowerCase();
    const locator = getRecordedActionField(action, 'locator') || '';
    const value = getRecordedActionField(action, 'value') || '';
    const metadata = action?.metadata || action?.Metadata || {};
    const parameterName = metadata.ParameterName || metadata.parameterName || '';

    // Skip parametrization steps - they shouldn't have auto-assertions
    // Any action with ParameterName metadata is a data-driven parameterization step
    if (parameterName) {
        // This is a parametrization value step, skip assertion
        return assertions;
    }

    if (actionType === 'navigate') {
        if (value) {
            assertions.push({
                type: 'UrlContains',
                locator: '',
                expectedValue: getStableUrlToken(value),
                description: 'Auto-verify navigation completed',
                executeAfterActionIndex: actionIndex,
                enabled: true
            });
        }
        return assertions;
    }

    if (['type', 'input', 'fill'].includes(actionType)) {
        if (!locator) return assertions;
        const expectedValue = parameterName ? `{{${parameterName}}}` : value;

        if (!expectedValue) {
            assertions.push({
                type: 'ElementVisible',
                locator,
                expectedValue: '',
                description: `Auto-verify ${actionTypeRaw || 'input'} target is visible`,
                executeAfterActionIndex: actionIndex,
                enabled: true
            });
        } else {
            assertions.push({
                type: 'ValueEquals',
                locator,
                expectedValue,
                description: `Auto-verify value entered for ${actionTypeRaw || 'input'}`,
                executeAfterActionIndex: actionIndex,
                enabled: true
            });
        }
        return assertions;
    }

    if (actionType === 'select') {
        if (locator) {
            assertions.push({
                type: 'ElementVisible',
                locator,
                expectedValue: '',
                description: 'Auto-verify dropdown is visible after selection',
                executeAfterActionIndex: actionIndex,
                enabled: true
            });
        }
        return assertions;
    }

    if (actionType === 'click') {
        // For click actions, verify element EXISTS before clicking
        // Use ElementExists instead of ElementVisible because click will auto-scroll to element
        if (locator) {
            assertions.push({
                type: 'ElementExists',
                locator,
                expectedValue: '',
                description: 'Auto-verify element exists before click',
                executeBeforeActionIndex: actionIndex,
                enabled: true
            });
        }
        return assertions;
    }

    if (actionType === 'submit') {
        // For submit actions: verify element EXISTS before submitting
        // Use ElementExists instead of ElementVisible because submit will auto-scroll to element
        if (locator) {
            assertions.push({
                type: 'ElementExists',
                locator,
                expectedValue: '',
                description: 'Auto-verify element exists before submit',
                executeBeforeActionIndex: actionIndex,
                enabled: true
            });
        }
        return assertions;
    }

    if (['wait', 'waitforelement', 'switchtoframe', 'switchtodefaultcontent'].includes(actionType)) {
        return assertions;
    }

    if (locator) {
        assertions.push({
            type: 'ElementExists',
            locator,
            expectedValue: '',
            description: `Auto-verify target exists after ${actionTypeRaw || 'action'}`,
            executeAfterActionIndex: actionIndex,
            enabled: true
        });
    }
    
    return assertions;
}

function getStableUrlToken(url) {
    try {
        const parsed = new URL(url);
        const path = (parsed.pathname || '').replace(/\/$/, '');
        return !path || path === '/' ? parsed.host : `${parsed.host}${path}`;
    } catch {
        return url;
    }
}

function formatAutoAssertionPreview(assertion) {
    if (!assertion) return '-';
    if (assertion.type === 'UrlContains') {
        return `${assertion.type}(${assertion.expectedValue || '-'})`;
    }

    if (assertion.expectedValue) {
        return `${assertion.type}(${assertion.locator || '-'}, ${assertion.expectedValue})`;
    }

    return `${assertion.type}(${assertion.locator || '-'})`;
}

function toggleLiveAutoAssertion(index, enabled) {
    if (!window.liveAutoAssertions || !window.liveAutoAssertions[index]) return;
    window.liveAutoAssertions[index].enabled = !!enabled;
}

function editLiveAutoAssertion(index) {
    if (!window.liveAutoAssertions || !window.liveAutoAssertions[index]) return;

    const assertion = window.liveAutoAssertions[index];
    const updatedLocator = prompt('Edit assertion locator (leave empty for URL assertions):', assertion.locator || '');
    if (updatedLocator === null) return;

    let updatedExpectedValue = assertion.expectedValue || '';
    const lowerType = (assertion.type || '').toLowerCase();
    if (lowerType !== 'elementvisible' && lowerType !== 'elementexists') {
        const expectedPrompt = prompt('Edit expected value:', assertion.expectedValue || '');
        if (expectedPrompt === null) return;
        updatedExpectedValue = expectedPrompt;
    }

    const updatedType = prompt('Edit assertion type (ElementExists, ElementVisible, ValueEquals, UrlContains):', assertion.type || '');
    if (updatedType === null) return;

    assertion.locator = updatedLocator || '';
    assertion.expectedValue = updatedExpectedValue || '';
    assertion.type = updatedType || assertion.type;

    const preview = document.getElementById(`auto-assertion-preview-${index}`);
    if (preview) {
        const previewLabel = preview.querySelector('span');
        if (previewLabel) {
            const text = formatAutoAssertionPreview(assertion);
            previewLabel.textContent = text;
            previewLabel.title = text;
        }
    }
}

async function syncLiveAutoAssertionsToRecorder() {
    if (!window.liveAutoAssertions || window.liveAutoAssertions.length === 0) return;

    const assertionsToSync = window.liveAutoAssertions.filter(a => a && a.enabled);
    for (const assertion of assertionsToSync) {
        try {
            await fetch(`${API_BASE_URL}/recorder/assertion`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    type: assertion.type,
                    locator: assertion.locator || '',
                    expectedValue: assertion.expectedValue || null,
                    description: assertion.description || null,
                    executeAfterActionIndex: assertion.executeAfterActionIndex
                })
            });
        } catch (error) {
            console.warn('Failed to sync live auto-assertion:', error);
        }
    }
}

// Real-time validation for scenario name uniqueness
let validateScenarioTimeout;
let isDuplicateScenarioName = false;

window.validateScenarioNameRealtime = async function() {
    clearTimeout(validateScenarioTimeout);
    
    const nameInput = document.getElementById('record-name');
    const validationMsg = document.getElementById('scenario-name-validation');
    const startBtn = document.getElementById('start-recording-btn');
    const scenarioName = nameInput.value.trim();
    
    if (!scenarioName) {
        validationMsg.style.display = 'none';
        nameInput.style.borderColor = '';
        isDuplicateScenarioName = false;
        if (startBtn && !isRecording) {
            startBtn.disabled = false;
        }
        return;
    }
    
    // Debounce validation by 300ms (reduced for faster feedback)
    validateScenarioTimeout = setTimeout(async () => {
        try {
            const response = await fetch(`${API_BASE_URL}/scenarios`);
            if (response.ok) {
                const data = await response.json();
                if (data.success && data.scenarios && Array.isArray(data.scenarios)) {
                    const duplicateScenario = data.scenarios.find(s => {
                        const name = s.Name || s.name;
                        return name && name.toLowerCase() === scenarioName.toLowerCase();
                    });
                    
                    if (duplicateScenario) {
                        const moduleName = duplicateScenario.Module || duplicateScenario.module || 'Unknown';
                        validationMsg.textContent = `Scenario name "${scenarioName}" already exists in module "${moduleName}". Please use a different name.`;
                        validationMsg.style.display = 'block';
                        nameInput.style.borderColor = '#dc3545';
                        isDuplicateScenarioName = true;
                        if (startBtn) {
                            startBtn.disabled = true;
                            startBtn.title = 'Cannot start recording - duplicate scenario name detected';
                        }
                    } else {
                        console.log('No duplicate found - name is unique');
                        validationMsg.style.display = 'none';
                        nameInput.style.borderColor = '';
                        isDuplicateScenarioName = false;
                        if (startBtn && !isRecording) {
                            startBtn.disabled = false;
                            startBtn.title = '';
                        }
                    }
                }
            }
        } catch (error) {
            console.warn('Validation failed (backend will validate)');
            isDuplicateScenarioName = false;
            if (startBtn && !isRecording) {
                startBtn.disabled = false;
            }
        }
    }, 300);
};  // End of window.validateScenarioNameRealtime

async function startAssistedRecording(event) {
    event.preventDefault();

    // Check for duplicate scenario name flag
    if (isDuplicateScenarioName) {
        showError('Cannot start recording - a scenario with this name already exists. Please choose a different name.');
        return;
    }

    const request = {
        scenarioName: document.getElementById('record-name').value,
        module: document.getElementById('record-module').value,
        description: document.getElementById('record-description').value,
        startUrl: document.getElementById('record-url').value,
        tags: document.getElementById('record-tags').value.split(',').map(t => t.trim()).filter(t => t)
    };

    try {
        // Validate: Check if scenario with same name already exists across ALL modules (global uniqueness)
        try {
            const checkResponse = await fetch(`${API_BASE_URL}/scenarios`);
            if (checkResponse.ok) {
                const checkData = await checkResponse.json();
                if (checkData.success && checkData.scenarios && Array.isArray(checkData.scenarios)) {
                    const duplicateScenario = checkData.scenarios.find(s => 
                        s.Name && s.Name.toLowerCase() === request.scenarioName.toLowerCase()
                    );
                    
                    if (duplicateScenario) {
                        showError(`Scenario name "${request.scenarioName}" already exists in module "${duplicateScenario.Module}". Please use a different name.`);
                        console.warn('Duplicate scenario detected:', request.scenarioName, 'in module:', duplicateScenario.Module);
                        return;
                    }
                }
            }
        } catch (validationError) {
            // Log validation check error but continue (backend will also validate)
            console.warn('Frontend validation check failed:', validationError.message);
        }

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
            document.getElementById('recording-live-actions').classList.remove('hidden');

            // Clear previous actions
            window.recordedActions = [];
            window.liveAutoAssertions = [];
            const body = document.getElementById('live-actions-body');
            if (body) body.innerHTML = '';
            const countEl = document.getElementById('live-action-count');
            if (countEl) countEl.textContent = '0 steps';

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

        await syncLiveAutoAssertionsToRecorder();

        const response = await fetch(`${API_BASE_URL}/recorder/stop`, {
            method: 'POST'
        });

        const data = await response.json();

        // Reset button
        stopBtn.innerHTML = originalText;

        if (data.success) {
            isRecording = false;
            window.liveAutoAssertions = [];
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
            const scenarioModule = data.scenario?.module ||
                document.getElementById('record-module')?.value ||
                'Default';

            setRecordedScenarioForGeneration(scenarioName, scenarioModule);

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

async function generateRecordedScenarioScript() {
    if (!lastRecordedScenario?.name || !lastRecordedScenario?.module) {
        showError('Record and save a scenario first, then generate script.');
        return;
    }

    const framework = document.getElementById('script-framework')?.value || 'playwright';
    const language = document.getElementById('script-language')?.value || 'csharp';

    try {
        showLoading('Generating script from recorded scenario...');

        const response = await fetch(`${API_BASE_URL}/scriptgeneration/from-recorded-scenario`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                scenarioName: lastRecordedScenario.name,
                module: lastRecordedScenario.module,
                framework,
                language,
                includeProjectFiles: false
            })
        });

        const data = await response.json();
        hideLoading();

        if (!data.success) {
            showError(data.error || 'Failed to generate script');
            return;
        }

        generatedScriptCache = {
            content: data.outputs?.testScript || '',
            fileName: data.generation?.suggestedFileName || `${lastRecordedScenario.name}.txt`,
            pageObject: data.outputs?.pageObject || '',
            metadata: data.generation
        };

        const panel = document.getElementById('generated-script-panel');
        const content = document.getElementById('generated-script-content');
        if (panel && content) {
            content.textContent = generatedScriptCache.content;
            panel.classList.remove('hidden');
        }

        const advancedPanel = document.getElementById('advanced-concepts-panel');
        const advancedContent = document.getElementById('advanced-concepts-content');
        if (advancedPanel && advancedContent) {
            const testCases = (data.derivedTestCases || []).map(tc => `
                <div style="padding:10px;border:1px solid #cbd5e1;border-radius:6px;background:#ffffff;">
                    <div style="font-weight:600;color:#1e293b;">${tc.name}</div>
                    <div style="font-size:0.88em;color:#475569;">${tc.objective}</div>
                    <div style="font-size:0.8em;color:#64748b;margin-top:4px;">Priority: ${tc.priority} • Type: ${tc.type}</div>
                </div>
            `).join('');

            const concepts = (data.advancedConcepts || []).map(ac => `
                <div style="padding:10px;border:1px solid #e2e8f0;border-radius:6px;background:#f8fafc;">
                    <div style="font-weight:600;color:#0f172a;">${ac.concept}</div>
                    <div style="font-size:0.88em;color:#334155;">${ac.value}</div>
                    <div style="font-size:0.78em;color:#64748b;margin-top:4px;">Impact: ${ac.impact}</div>
                </div>
            `).join('');

            advancedContent.innerHTML = `
                <div style="font-weight:600;color:#1e293b;">Derived Test Cases</div>
                ${testCases || '<div style="color:#64748b;">No derived test cases generated.</div>'}
                <div style="font-weight:600;color:#1e293b;margin-top:8px;">Advanced Concepts</div>
                ${concepts || '<div style="color:#64748b;">No recommendations generated.</div>'}
            `;
            advancedPanel.classList.remove('hidden');
        }

        showSuccess('Script generated successfully from recorded scenario.');
    } catch (error) {
        hideLoading();
        showError('Failed to generate script: ' + error.message);
    }
}

function downloadGeneratedScript() {
    if (!generatedScriptCache?.content) {
        showError('Generate a script first, then download.');
        return;
    }

    const blob = new Blob([generatedScriptCache.content], { type: 'text/plain;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = generatedScriptCache.fileName || 'generated-test-script.txt';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
}

async function loadExportView() {
    const view = document.getElementById('export-view');
    if (!view) return;

    try {
        view.innerHTML = `
        <div class="header">
            <h2><i class="fas fa-file-export"></i> Script Export</h2>
            <p style="color:#6b7280;margin-top:6px;">Export selected scenarios as runnable automation project based on framework.</p>
        </div>

        <div class="card">
            <div class="card-header">
                <div class="card-title"><i class="fas fa-sliders"></i> Export Configuration</div>
            </div>
            <div style="padding:16px;">
                <div class="grid-3" style="gap:12px;">
                    <div class="form-group">
                        <label>Framework</label>
                        <select class="form-control" id="export-framework" onchange="onExportFrameworkChange()">
                            <option value="Playwright" selected>Playwright</option>
                            <option value="Selenium">Selenium</option>
                            <option value="Cypress">Cypress</option>
                        </select>
                    </div>
                    <div class="form-group">
                        <label>Language</label>
                        <select class="form-control" id="export-language"></select>
                    </div>
                    <div class="form-group">
                        <label>Project Name</label>
                        <input class="form-control" id="export-project-name" value="AutomationTests" />
                    </div>
                </div>

                <div style="display:flex;gap:14px;flex-wrap:wrap;margin-top:4px;">
                    <label style="display:flex;align-items:center;gap:6px;cursor:pointer;">
                        <input type="checkbox" id="export-all-frameworks" onchange="toggleExportMode()" />
                        Export all frameworks (Selenium + Playwright + Cypress)
                    </label>
                </div>

                <div style="display:flex;gap:14px;flex-wrap:wrap;margin-top:6px;">
                    <label style="display:flex;align-items:center;gap:6px;cursor:pointer;"><input type="checkbox" id="export-page-objects" checked /> Page Objects</label>
                    <label style="display:flex;align-items:center;gap:6px;cursor:pointer;"><input type="checkbox" id="export-config" checked /> Configuration</label>
                    <label style="display:flex;align-items:center;gap:6px;cursor:pointer;"><input type="checkbox" id="export-readme" checked /> README</label>
                </div>
            </div>
        </div>

        <div class="card mt-20">
            <div class="card-header">
                <div class="card-title"><i class="fas fa-list-check"></i> Select Scenarios</div>
                <div style="display:flex;align-items:center;gap:10px;">
                    <button class="btn btn-secondary btn-sm" onclick="toggleSelectAllExportScenarios(true)">Select All</button>
                    <button class="btn btn-secondary btn-sm" onclick="toggleSelectAllExportScenarios(false)">Clear</button>
                    <span class="badge badge-info" id="export-selection-count">0 selected</span>
                </div>
            </div>
            <div style="padding:16px;max-height:360px;overflow:auto;" id="export-scenarios-list">
                <div class="loading"><i class="fas fa-spinner fa-spin"></i> Loading scenarios...</div>
            </div>
        </div>

        <div class="card mt-20">
            <div style="padding:16px;display:flex;justify-content:space-between;align-items:center;gap:12px;flex-wrap:wrap;">
                <div style="color:#64748b;">Framework-based export creates a downloadable ZIP project.</div>
                <button class="btn btn-success" onclick="exportScriptsByFramework()" id="export-download-btn">
                    <i class="fas fa-download"></i> Export ZIP
                </button>
            </div>
        </div>
    `;

        await onExportFrameworkChange();
        await loadExportScenarios();
        toggleExportMode();
    } catch (error) {
        console.error('Failed to render Script Export view:', error);
        view.innerHTML = `
            <div class="header">
                <h2><i class="fas fa-file-export"></i> Script Export</h2>
            </div>
            <div class="card">
                <div style="padding:16px;color:#dc2626;">
                    <strong>Unable to load Script Export view.</strong>
                    <div style="margin-top:8px;color:#7f1d1d;">${error.message || 'Unexpected error occurred while rendering export view.'}</div>
                </div>
            </div>
        `;
        showError('Script Export view failed to load. Check browser console for details.');
    }
}

function toggleExportMode() {
    const exportAll = document.getElementById('export-all-frameworks')?.checked === true;
    const frameworkEl = document.getElementById('export-framework');
    const languageEl = document.getElementById('export-language');
    const button = document.getElementById('export-download-btn');

    if (frameworkEl) frameworkEl.disabled = exportAll;
    if (languageEl) languageEl.disabled = exportAll;
    if (button) {
        button.innerHTML = exportAll
            ? '<i class="fas fa-download"></i> Export All Frameworks ZIP'
            : '<i class="fas fa-download"></i> Export ZIP';
    }
}

async function onExportFrameworkChange() {
    const framework = document.getElementById('export-framework')?.value;
    const languageSelect = document.getElementById('export-language');
    if (!framework || !languageSelect) return;

    languageSelect.innerHTML = '<option value="">Loading...</option>';

    try {
        const response = await fetch(`${API_BASE_URL}/export/languages/${framework}`);
        const data = await response.json();
        const languages = data.languages || [];

        languageSelect.innerHTML = languages
            .map(l => `<option value="${l}">${l}</option>`)
            .join('');
    } catch (error) {
        languageSelect.innerHTML = '<option value="CSharp">CSharp</option>';
        console.error('Failed to load export languages:', error);
    }
}

async function loadExportScenarios() {
    const container = document.getElementById('export-scenarios-list');
    if (!container) return;

    try {
        const response = await fetch(`${API_BASE_URL}/scenarios`);
        const data = await response.json();
        exportScenarioCache = data.scenarios || [];

        if (!exportScenarioCache.length) {
            container.innerHTML = '<div style="color:#64748b;">No scenarios found. Record or create scenarios first.</div>';
            updateExportSelectionCount();
            return;
        }

        container.innerHTML = exportScenarioCache.map((scenario, index) => `
            <label style="display:flex;gap:10px;align-items:flex-start;padding:10px;border:1px solid #e2e8f0;border-radius:8px;margin-bottom:8px;cursor:pointer;">
                <input type="checkbox" class="export-scenario-check" value="${index}" onchange="updateExportSelectionCount()" />
                <div style="flex:1;">
                    <div style="font-weight:600;color:#1e293b;">${scenario.name || 'Unnamed'}</div>
                    <div style="font-size:0.85em;color:#64748b;">Module: ${scenario.module || 'Default'} • Actions: ${scenario.actionCount || scenario.actions?.length || 0}</div>
                </div>
            </label>
        `).join('');

        updateExportSelectionCount();
    } catch (error) {
        container.innerHTML = '<div style="color:#dc2626;">Failed to load scenarios for export.</div>';
        console.error('Failed to load export scenarios:', error);
    }
}

function toggleSelectAllExportScenarios(selectAll) {
    document.querySelectorAll('.export-scenario-check').forEach(cb => {
        cb.checked = selectAll;
    });
    updateExportSelectionCount();
}

function updateExportSelectionCount() {
    const selectedCount = document.querySelectorAll('.export-scenario-check:checked').length;
    const label = document.getElementById('export-selection-count');
    if (label) {
        label.textContent = `${selectedCount} selected`;
    }
}

function getSelectedExportScenarios() {
    const selectedIndexes = Array.from(document.querySelectorAll('.export-scenario-check:checked'))
        .map(cb => parseInt(cb.value, 10))
        .filter(idx => !Number.isNaN(idx));

    return selectedIndexes
        .map(idx => exportScenarioCache[idx])
        .filter(Boolean);
}

async function exportScriptsByFramework() {
    const selectedScenarios = getSelectedExportScenarios();
    if (!selectedScenarios.length) {
        showWarning('Select at least one scenario to export.');
        return;
    }

    const exportAllFrameworks = document.getElementById('export-all-frameworks')?.checked === true;
    const framework = document.getElementById('export-framework')?.value || 'Playwright';
    const language = document.getElementById('export-language')?.value || 'CSharp';
    const projectName = document.getElementById('export-project-name')?.value?.trim() || 'AutomationTests';

    const button = document.getElementById('export-download-btn');
    const originalHtml = button?.innerHTML;
    if (button) {
        button.disabled = true;
        button.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Exporting...';
    }

    try {
        const endpoint = exportAllFrameworks
            ? `${API_BASE_URL}/export/export-multiple`
            : `${API_BASE_URL}/export/export-zip`;

        const payload = exportAllFrameworks
            ? {
                scenarios: selectedScenarios,
                language
            }
            : {
                scenarios: selectedScenarios,
                format: framework,
                language,
                projectName,
                includePageObjects: document.getElementById('export-page-objects')?.checked ?? true,
                includeConfiguration: document.getElementById('export-config')?.checked ?? true,
                includeReadme: document.getElementById('export-readme')?.checked ?? true
            };

        const response = await fetch(endpoint, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });

        if (!response.ok) {
            const err = await response.json().catch(() => ({ error: 'Export failed' }));
            throw new Error(err.error || 'Export failed');
        }

        const blob = await response.blob();
        const downloadUrl = window.URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = downloadUrl;
        anchor.download = exportAllFrameworks
            ? `${projectName}_AllFrameworks_${new Date().toISOString().split('T')[0]}.zip`
            : `${projectName}_${framework}_${new Date().toISOString().split('T')[0]}.zip`;
        document.body.appendChild(anchor);
        anchor.click();
        anchor.remove();
        window.URL.revokeObjectURL(downloadUrl);

        showSuccess(exportAllFrameworks
            ? 'Export complete: all framework projects downloaded.'
            : `Export complete: ${framework} project downloaded.`);
    } catch (error) {
        showError('Export failed: ' + error.message);
    } finally {
        if (button) {
            button.disabled = false;
            button.innerHTML = originalHtml || '<i class="fas fa-download"></i> Export ZIP';
        }
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

        const response = await fetch(`${API_BASE_URL}/scenarios/execute/${encodeURIComponent(module)}/${encodeURIComponent(name)}`, {
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
                addConsoleLog(`✓ Test completed: ${data.result.status}`, statusMessage);
                displayTestResult(data.result);
            }

            // Show appropriate notification based on actual test result
            if (testPassed) {
                showSuccess(`✓ Test passed: ${data.result.status}`);
            } else {
                showError(`✗ Test failed: ${data.result.status}${data.result.errorMessage ? ' - ' + data.result.errorMessage : ''}`);
            }
        } else {
            if (typeof updateExecutionStatus === 'function') {
                updateExecutionStatus('failed', 'Execution failed');
            }
            if (typeof addConsoleLog === 'function') {
                addConsoleLog(`✗ Test failed: ${data.error}`, 'error');
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

        const response = await fetch(`${API_BASE_URL}/scenarios/execute/module/${encodeURIComponent(module)}`, {
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
        // Always fetch latest from server to ensure we have current saved state
        const response = await fetch(`${API_BASE_URL}/configuration`);
        const data = await response.json();

        console.log('🔍 Load Configuration Debug:');
        console.log('📥 Server response:', data);

        if (data.success) {
            configuration = data.configuration;
            
            console.log('✅ Loaded configuration:', configuration);
            console.log('✅ Parallel Browsers:', configuration.parallelBrowsers);
            console.log('✅ Cross-Browser Enabled:', configuration.crossBrowserParallelExecution);
            console.log('✅ Execution Mode:', configuration.executionMode);
            
            // Update cache
            try {
                localStorage.setItem('agenticai-config', JSON.stringify(configuration));
            } catch (e) {
                console.warn('Could not update cache:', e);
            }

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
                            <label>Base URL (Application Under Test)</label>
                            <input type="url" class="form-control" id="config-base-url" 
                                   value="${configuration.baseUrl || ''}" 
                                   placeholder="https://your-application-url.com">
                            <small style="color: #6b7280; display: block; margin-top: 5px;">
                                The default URL for your test scenarios. Tests can override this with their own Start URL. Leave empty if not needed.
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
                                <select class="form-control" id="config-execution-mode" onchange="toggleCrossBrowserOptions()">
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

                        <!-- Cross-Browser Parallel Execution Options -->
                        <div id="cross-browser-options" class="${configuration.executionMode === 'Parallel' ? '' : 'hidden'}" style="margin-top: 20px;">
                            <div class="card" style="border: 2px solid #3b82f6; background: rgba(59, 130, 246, 0.05);">
                                <div class="card-header" style="background: rgba(59, 130, 246, 0.1);">
                                    <div class="card-title">
                                        <i class="fas fa-globe"></i> Cross-Browser Parallel Execution
                                    </div>
                                </div>
                                <div style="padding: 20px;">
                                    <div class="form-group">
                                        <label style="display: flex; align-items: center; gap: 10px; cursor: pointer;">
                                            <input type="checkbox" id="config-cross-browser" ${configuration.crossBrowserParallelExecution ? 'checked' : ''}> 
                                            <span>Enable Cross-Browser Parallel Execution</span>
                                        </label>
                                        <small style="color: #6b7280; display: block; margin-top: 5px;">
                                            Distribute tests across multiple browsers simultaneously (e.g., Test 1→Chrome, Test 2→Firefox, Test 3→Edge)
                                        </small>
                                    </div>

                                    <div id="browser-selection" style="margin-top: 20px;">
                                        <label style="font-weight: 600; margin-bottom: 10px; display: block;">
                                            Select Browsers for Parallel Execution:
                                        </label>
                                        <div style="display: grid; grid-template-columns: repeat(2, 1fr); gap: 10px;">
                                            <label style="display: flex; align-items: center; gap: 10px; cursor: pointer; padding: 10px; border: 1px solid #e5e7eb; border-radius: 6px; transition: all 0.2s;">
                                                <input type="checkbox" class="browser-checkbox" value="Chrome" ${(configuration.parallelBrowsers?.includes('Chrome') || configuration.parallelBrowsers?.includes(0)) ? 'checked' : ''}> 
                                                <i class="fab fa-chrome" style="color: #4285F4; font-size: 1.2em;"></i>
                                                <span>Chrome</span>
                                            </label>
                                            <label style="display: flex; align-items: center; gap: 10px; cursor: pointer; padding: 10px; border: 1px solid #e5e7eb; border-radius: 6px; transition: all 0.2s;">
                                                <input type="checkbox" class="browser-checkbox" value="Firefox" ${(configuration.parallelBrowsers?.includes('Firefox') || configuration.parallelBrowsers?.includes(1)) ? 'checked' : ''}> 
                                                <i class="fab fa-firefox" style="color: #FF7139; font-size: 1.2em;"></i>
                                                <span>Firefox</span>
                                            </label>
                                            <label style="display: flex; align-items: center; gap: 10px; cursor: pointer; padding: 10px; border: 1px solid #e5e7eb; border-radius: 6px; transition: all 0.2s;">
                                                <input type="checkbox" class="browser-checkbox" value="Edge" ${(configuration.parallelBrowsers?.includes('Edge') || configuration.parallelBrowsers?.includes(2)) ? 'checked' : ''}> 
                                                <i class="fab fa-edge" style="color: #0078D7; font-size: 1.2em;"></i>
                                                <span>Edge</span>
                                            </label>
                                            <label style="display: flex; align-items: center; gap: 10px; cursor: pointer; padding: 10px; border: 1px solid #e5e7eb; border-radius: 6px; transition: all 0.2s;">
                                                <input type="checkbox" class="browser-checkbox" value="Safari" ${(configuration.parallelBrowsers?.includes('Safari') || configuration.parallelBrowsers?.includes(3)) ? 'checked' : ''}> 
                                                <i class="fab fa-safari" style="color: #006CFF; font-size: 1.2em;"></i>
                                                <span>Safari</span>
                                            </label>
                                        </div>
                                    </div>

                                    <div class="alert alert-info" style="margin-top: 20px; background: rgba(59, 130, 246, 0.1); border-left: 4px solid #3b82f6; padding: 15px; border-radius: 6px;">
                                        <strong><i class="fas fa-info-circle"></i> How it works:</strong><br>
                                        Tests will be distributed across selected browsers in round-robin fashion.<br>
                                        <strong>Example:</strong> Test1→Chrome, Test2→Firefox, Test3→Edge, Test4→Chrome...
                                    </div>
                                </div>
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
    const executionMode = document.getElementById('config-execution-mode').value;
    const crossBrowserEnabled = document.getElementById('config-cross-browser')?.checked || false;
    
    // Get selected browsers
    const browserCheckboxes = document.querySelectorAll('.browser-checkbox:checked');
    const selectedBrowsers = Array.from(browserCheckboxes).map(cb => cb.value);
    
    console.log('🔍 Save Configuration Debug:');
    console.log('- Execution Mode:', executionMode);
    console.log('- Cross-Browser Enabled:', crossBrowserEnabled);
    console.log('- Selected Browsers:', selectedBrowsers);
    
    const updatedConfig = {
        automationFramework: document.getElementById('config-framework').value,
        browser: document.getElementById('config-browser').value,
        environment: document.getElementById('config-environment').value,
        executionMode: executionMode,
        baseUrl: document.getElementById('config-base-url').value,
        timeoutInSeconds: parseInt(document.getElementById('config-timeout').value),
        maxRetryCount: parseInt(document.getElementById('config-retry').value),
        headless: document.getElementById('config-headless').checked,
        enableVideo: document.getElementById('config-video').checked,
        enableScreenshots: document.getElementById('config-screenshots').checked,
        enableSelfHealing: document.getElementById('config-self-healing').checked,
        // Cross-browser settings
        crossBrowserParallelExecution: executionMode === 'Parallel' && crossBrowserEnabled,
        parallelBrowsers: selectedBrowsers.length > 0 ? selectedBrowsers : ['Chrome'],
        // Keep other settings from original config
        operatingSystem: configuration.operatingSystem || 'Windows',
        reportPath: configuration.reportPath || 'TestReports',
        screenshotPath: configuration.screenshotPath || 'Screenshots',
        videoPath: configuration.videoPath || 'Videos',
        logPath: configuration.logPath || 'Logs',
        parallelWorkers: configuration.parallelWorkers || 4,
        enableTracing: configuration.enableTracing || false,
        enableAccessibilityTesting: configuration.enableAccessibilityTesting || false,
        enableVisualRegression: configuration.enableVisualRegression || false,
        enablePerformanceMetrics: configuration.enablePerformanceMetrics || false
    };

    console.log('📤 Sending configuration to server:', updatedConfig);

    try {
        showLoading('Saving configuration...');
        
        const response = await fetch(`${API_BASE_URL}/configuration`, {
            method: 'PUT',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(updatedConfig)
        });

        const data = await response.json();
        
        console.log('📥 Server response:', data);
        console.log('📥 Returned configuration:', data.configuration);
        
        hideLoading();

        if (data.success) {
            // Update global configuration variable
            configuration = data.configuration;
            
            console.log('✅ Updated global configuration:', configuration);
            console.log('✅ Parallel Browsers in config:', configuration.parallelBrowsers);
            console.log('✅ Cross-Browser Enabled:', configuration.crossBrowserParallelExecution);
            
            // Store in localStorage for persistence
            try {
                localStorage.setItem('agenticai-config', JSON.stringify(configuration));
                console.log('✅ Saved to localStorage');
            } catch (e) {
                console.warn('Could not save to localStorage:', e);
            }
            
            showSuccess('✅ Configuration saved! Changes will apply to all test executions.');
            
            // Add visual feedback
            const saveBtn = document.querySelector('.header-actions .btn-success');
            if (saveBtn) {
                const originalText = saveBtn.innerHTML;
                saveBtn.innerHTML = '<i class="fas fa-check"></i> Saved!';
                saveBtn.style.background = '#10b981';
                setTimeout(() => {
                    saveBtn.innerHTML = originalText;
                    saveBtn.style.background = '';
                }, 2000);
            }
        } else {
            console.error('❌ Save failed:', data.error);
            showError('❌ Failed to save: ' + data.error);
        }
    } catch (error) {
        console.error('❌ Exception during save:', error);
        hideLoading();
        showError('❌ Failed to save configuration: ' + error.message);
    }
}

// Toggle cross-browser options visibility
function toggleCrossBrowserOptions() {
    const executionMode = document.getElementById('config-execution-mode')?.value;
    const crossBrowserDiv = document.getElementById('cross-browser-options');
    
    if (crossBrowserDiv) {
        if (executionMode === 'Parallel') {
            crossBrowserDiv.classList.remove('hidden');
        } else {
            crossBrowserDiv.classList.add('hidden');
        }
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
            <div style="padding: 0 10px 10px 10px;">
                <div class="grid-4" style="gap: 10px;">
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
                    <span class="badge badge-info" id="selected-results-count">0 selected</span>
                    <button class="btn btn-secondary" id="export-html-btn" onclick="exportSelectedResultsAsHtml()" disabled>
                        <i class="fas fa-file-code"></i> Export as HTML
                    </button>
                    <button class="btn btn-primary" id="export-extent-btn" onclick="exportSelectedResultsAsExtent()" disabled>
                        <i class="fas fa-chart-bar"></i> Extent Report
                    </button>
                    <button class="btn btn-danger" id="delete-selected-btn" onclick="deleteSelectedTests()" disabled>
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

function getProfessionalResultName(rawName) {
    const name = (rawName || '').trim();
    if (!name) return 'Unknown';

    // Already in modern DDT format
    if (/\[\s*DDT\s*[•\-]?\s*Iteration\s*\d+\s*\]/i.test(name)) {
        return name;
    }

    // Convert legacy names like:
    // "test9 [Row 2: key=value, key2=value2]" or "test9 [Row 2]"
    // to "test9 [DDT • Iteration 2]"
    const legacyRowMatch = name.match(/^(.*?)\s*\[\s*row\s*(\d+)\s*(?::[^\]]*)?\]\s*$/i);
    if (legacyRowMatch) {
        const scenarioName = (legacyRowMatch[1] || '').trim();
        const iteration = legacyRowMatch[2];
        return `${scenarioName} [DDT • Iteration ${iteration}]`;
    }

    // If no explicit row marker, keep original name and append DDT tag for clarity
    return `${name} [DDT]`;
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
                                <td>
                                    <div style="font-weight:600;color:#111827;line-height:1.35;">${escapeHtml(getProfessionalResultName(item.scenarioName))}</div>
                                </td>
                                <td>
                                    <span class="badge" style="background-color: ${statusBadgeColor}; color: white; padding: 3px 9px; border-radius: 4px; font-size: 11px; font-weight: 600;">
                                        ${escapeHtml(item.status.toUpperCase())}
                                    </span>
                                </td>
                                <td>${item.duration ? item.duration + 's' : 'N/A'}</td>
                                <td>${browser}</td>
                                <td>${environment}</td>
                                <td style="font-size: 0.9em;">${formattedDate}</td>
                                <td>
                                    ${hasEvidence ? `
                                        <button class="btn btn-primary btn-sm" onclick="viewEvidence(${index})" style="padding: 4px 10px; font-size: 11px;">
                                            <i class="fas fa-eye"></i> View
                                        </button>
                                    ` : '-'}
                                </td>
                                <td>
                                    ${hasLogs ? `
                                        <button class="btn btn-primary btn-sm" onclick="viewLogs(${index})" style="padding: 4px 10px; font-size: 11px;">
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
        <div style="display: flex; justify-content: space-between; align-items: center; margin-top: 10px; padding: 8px; background: #f9fafb; border-radius: 8px;">
            <div style="color: #6b7280; font-size: 14px;">
                Showing ${paginatedHistory.length} of ${totalRecords} records
            </div>
            <div style="display: flex; gap: 5px;">
                <button onclick="goToPage(1)" 
                        ${currentPage === 1 ? 'disabled' : ''}
                        style="padding: 6px 9px; border: 1px solid #d1d5db; background: white; border-radius: 4px; cursor: pointer; ${currentPage === 1 ? 'opacity: 0.5; cursor: not-allowed;' : ''}">
                    <i class="fas fa-angle-double-left"></i>
                </button>
                <button onclick="goToPage(${currentPage - 1})" 
                        ${currentPage === 1 ? 'disabled' : ''}
                        style="padding: 6px 9px; border: 1px solid #d1d5db; background: white; border-radius: 4px; cursor: pointer; ${currentPage === 1 ? 'opacity: 0.5; cursor: not-allowed;' : ''}">
                    <i class="fas fa-angle-left"></i> Previous
                </button>
                
                ${generatePageButtons(currentPage, totalPages)}
                
                <button onclick="goToPage(${currentPage + 1})" 
                        ${currentPage === totalPages ? 'disabled' : ''}
                        style="padding: 6px 9px; border: 1px solid #d1d5db; background: white; border-radius: 4px; cursor: pointer; ${currentPage === totalPages ? 'opacity: 0.5; cursor: not-allowed;' : ''}">
                    Next <i class="fas fa-angle-right"></i>
                </button>
                <button onclick="goToPage(${totalPages})" 
                        ${currentPage === totalPages ? 'disabled' : ''}
                        style="padding: 6px 9px; border: 1px solid #d1d5db; background: white; border-radius: 4px; cursor: pointer; ${currentPage === totalPages ? 'opacity: 0.5; cursor: not-allowed;' : ''}">
                    <i class="fas fa-angle-double-right"></i>
                </button>
            </div>
        </div>
    `;

    container.innerHTML = html;

    // Store history data globally for access by view functions
    window.testResultsHistory = historyList;
    handleCheckboxChange();
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
    const checked = document.querySelectorAll('.result-checkbox:checked').length;
    const countLabel = document.getElementById('selected-results-count');
    if (countLabel) {
        countLabel.textContent = `${checked} selected`;
    }

    const exportHtmlBtn = document.getElementById('export-html-btn');
    const exportExtentBtn = document.getElementById('export-extent-btn');
    const deleteBtn = document.getElementById('delete-selected-btn');

    if (exportHtmlBtn) exportHtmlBtn.disabled = checked === 0;
    if (exportExtentBtn) exportExtentBtn.disabled = checked === 0;
    if (deleteBtn) deleteBtn.disabled = checked === 0;
}

function getSelectedResultItems() {
    const checkboxes = document.querySelectorAll('.result-checkbox:checked');
    const indices = Array.from(checkboxes)
        .map(cb => parseInt(cb.value, 10))
        .filter(idx => !Number.isNaN(idx));

    return indices
        .map(index => window.testResultsHistory?.[index])
        .filter(item => !!item);
}

function getSelectedTestIdentifiers() {
    return getSelectedResultItems().map(item => ({
        scenarioName: item.scenarioName,
        executedAt: item.executedAt
    }));
}

async function downloadFromResponse(response, fallbackName) {
    const disposition = response.headers.get('Content-Disposition') || '';
    const match = disposition.match(/filename\*=UTF-8''([^;]+)|filename="?([^";]+)"?/i);
    const fileName = decodeURIComponent((match?.[1] || match?.[2] || fallbackName).trim());
    const blob = await response.blob();
    const url = window.URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    window.URL.revokeObjectURL(url);
}

async function exportSelectedResultsAsHtml() {
    const selectedTests = getSelectedTestIdentifiers();
    if (!selectedTests.length) {
        showWarning('Select one or more test results to export as HTML.');
        return;
    }

    const button = document.getElementById('export-html-btn');
    const originalText = button?.innerHTML;
    try {
        if (button) {
            button.disabled = true;
            button.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Exporting...';
        }

        const response = await fetch(`${API_BASE_URL}/history/export-html`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ tests: selectedTests })
        });

        if (!response.ok) {
            let errorText = 'Failed to export HTML report.';
            try {
                const err = await response.json();
                errorText = err.error || errorText;
            } catch { }
            throw new Error(errorText);
        }

        await downloadFromResponse(response, `test-results-${new Date().toISOString().slice(0, 10)}.html`);
        showSuccess(`HTML report exported for ${selectedTests.length} selected result${selectedTests.length > 1 ? 's' : ''}.`);
    } catch (error) {
        showError(error.message || 'Failed to export HTML report.');
    } finally {
        if (button) {
            button.innerHTML = originalText || '<i class="fas fa-file-code"></i> Export as HTML';
        }
        handleCheckboxChange();
    }
}

async function exportSelectedResultsAsExtent() {
    const selectedTests = getSelectedTestIdentifiers();
    if (!selectedTests.length) {
        showWarning('Select one or more test results to export as Extent Report.');
        return;
    }

    const button = document.getElementById('export-extent-btn');
    const originalText = button?.innerHTML;
    try {
        if (button) {
            button.disabled = true;
            button.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Exporting...';
        }

        const response = await fetch(`${API_BASE_URL}/history/export-extent`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ tests: selectedTests })
        });

        if (!response.ok) {
            let errorText = 'Failed to export Extent report.';
            try {
                const err = await response.json();
                errorText = err.error || errorText;
            } catch { }
            throw new Error(errorText);
        }

        await downloadFromResponse(response, `extent-report-${new Date().toISOString().slice(0, 10)}.html`);
        showSuccess(`Extent report exported for ${selectedTests.length} selected result${selectedTests.length > 1 ? 's' : ''}.`);
    } catch (error) {
        showError(error.message || 'Failed to export Extent report.');
    } finally {
        if (button) {
            button.innerHTML = originalText || '<i class="fas fa-chart-bar"></i> Extent Report';
        }
        handleCheckboxChange();
    }
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
        return getProfessionalResultName(test.scenarioName);
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
            return getProfessionalResultName(test.scenarioName);
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
                        <i class="fas fa-images"></i> Evidence - ${escapeHtml(getProfessionalResultName(item.scenarioName))}
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
                            <i class="fas fa-file-alt"></i> Execution Logs - ${escapeHtml(getProfessionalResultName(item.scenarioName))}
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
        const response = await fetch(`${API_BASE_URL}/scenarios/${encodeURIComponent(module)}/${encodeURIComponent(name)}`, {
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
