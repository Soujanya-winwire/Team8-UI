// Advanced Data-Driven Testing Module - WinUITest Edition
// Integrates with Data-Driven Testing Engine (20 features)

let ddtUIState = {
    selectedDataSource: null,
    currentDatasets: [],
    selectedStrategy: 'run-all',
    executionResults: [],
    executionLogs: [],
    lastExecutionTime: null,
    isExecuting: false,
    currentTab: 'execution'
};

/**
 * Add log to execution logs array
 */
function addExecutionLog(message, type = 'info') {
    const timestamp = new Date().toLocaleTimeString();
    ddtUIState.executionLogs.push({
        timestamp,
        message,
        type // 'info', 'success', 'warning', 'error'
    });
}

/**
 * Clear execution logs
 */
function clearExecutionLogs() {
    ddtUIState.executionLogs = [];
}

/**
 * Initialize the Advanced Data-Driven Testing view
 */
async function initDataDrivenTestingView() {
    const viewContent = document.getElementById('datadriven-view');
    
    const html = `
        <div class="header">
            <h2><i class="fas fa-brain"></i> Advanced Data-Driven Testing Engine</h2>
            <p style="color:#666;font-size:0.88em;margin-block-start:4px;">WinUITest data-driven execution with CSV/JSON data injection, validation, and parallel runs.</p>
        </div>

        <!-- TAB NAVIGATION -->
        <div style="display:flex;gap:8px;margin:14px 0 0 0;flex-wrap:wrap;border-block-end:2px solid var(--border);padding-block-end:0;">
            <button class="tab-btn" data-tab="execution" onclick="showDDTTab('execution')" style="padding:9px 14px;background:#f3f4f6;border:none;border-block-end:3px solid transparent;cursor:pointer;color:#666;transition:all 0.3s;">
                <i class="fas fa-play"></i> Execution
            </button>
            <button class="tab-btn" data-tab="generation" onclick="showDDTTab('generation')" style="padding:9px 14px;background:#f3f4f6;border:none;border-block-end:3px solid transparent;cursor:pointer;color:#666;transition:all 0.3s;">
                <i class="fas fa-wand-magic-sparkles"></i> AI Generation (6-7)
            </button>
        </div>

        <!-- EXECUTION TAB -->
        <div id="ddt-execution-tab" class="ddt-tab" style="display:block;">
            <div class="card" style="border:2px solid var(--info-color);box-shadow:0 6px 16px rgba(59,130,246,0.15);margin-block-start:14px;">
                <div class="card-header" style="background:linear-gradient(135deg,rgba(59,130,246,0.08),rgba(37,99,235,0.04));">
                    <div class="card-title" style="display:flex;align-items:center;gap:10px;color:var(--info-color);">
                        <i class="fas fa-rocket"></i> Intelligent Test Execution
                    </div>
                    <span class="badge badge-info"><i class="fas fa-brain"></i> AI-Powered</span>
                </div>
                <div style="padding:14px;">

                    <div class="grid-2" style="gap:12px;">
                        <div class="form-group">
                            <label>Scenario Module</label>
                            <select class="form-control" id="dd-module" onchange="loadDDScenarios(); loadDataSourcesForModule();">
                                <option value="">Select Module</option>
                            </select>
                        </div>
                        <div class="form-group">
                            <label>Test Scenario</label>
                            <select class="form-control" id="dd-scenario" onchange="autoLoadScenarioData();">
                                <option value="">Select Scenario</option>
                            </select>
                        </div>
                    </div>

                    <div class="grid-2" style="gap:12px;">
                        <div class="form-group">
                            <label>Data Source</label>
                            <select class="form-control" id="dd-data-source" onchange="selectDataSource();">
                                <option value="">Select Data Source (JSON/CSV/API)</option>
                                <option value="inline">Inline Data Entry</option>
                            </select>
                        </div>
                        <div class="form-group">
                            <label>Execution Strategy</label>
                            <select class="form-control" id="dd-strategy" onchange="updateStrategyParams();">
                                <option value="run-all">Run All Datasets</option>
                                <option value="random">Random Selection</option>
                                <option value="first-n">First N Datasets</option>
                                <option value="custom-index">Custom Indices</option>
                                <option value="tag-based">Filter by Tags</option>
                            </select>
                        </div>
                    </div>

                    <!-- Strategy Parameters -->
                    <div id="strategy-params" style="margin-block-end:12px;">
                        <div class="form-group" id="param-count" style="display:none;">
                            <label>Number of Datasets</label>
                            <input type="number" class="form-control" id="dd-count" value="10" min="1">
                        </div>
                        <div class="form-group" id="param-tags" style="display:none;">
                            <label>Filter Tags (comma-separated)</label>
                            <input type="text" class="form-control" id="dd-tags" placeholder="e.g., smoke,regression">
                        </div>
                    </div>

                    <!-- Data Format Selection -->
                    <div class="form-group" style="margin-block-end:12px;">
                        <label>Data Format</label>
                        <select class="form-control" id="dd-format">
                            <option value="CSV" selected>CSV (comma-separated values)</option>
                            <option value="JSON">JSON (array of objects)</option>
                        </select>
                    </div>

                    <!-- Data Upload & Table Display -->
                    <div style="margin:14px 0;">
                        <label style="display:block;margin-block-end:10px;font-weight:600;">Test Data (Automatic Table Format)</label>
                        <div style="display:flex;gap:8px;margin-block-end:10px;">
                            <input type="file" id="dd-file-upload" accept=".csv,.json" style="display:none;" onchange="handleDDFileUpload(event);">
                            <button class="btn btn-secondary" style="flex:1;" onclick="document.getElementById('dd-file-upload').click();">
                                <i class="fas fa-file-upload"></i> Upload CSV/JSON File
                            </button>
                            <button class="btn btn-secondary" style="flex:1;" onclick="loadSampleData();">
                                <i class="fas fa-magic"></i> Load Sample Data
                            </button>
                            <button class="btn btn-secondary" style="flex:1;" onclick="clearTableData();">
                                <i class="fas fa-trash"></i> Clear Data
                            </button>
                        </div>

                        <!-- Data Table Display -->
                        <div id="dd-data-table-container" class="hidden" style="margin-block-start:10px;padding:12px;background:#f9fafb;border:1px solid var(--border);border-radius:8px;max-block-size:360px;overflow:auto;">
                            <div style="display:flex;justify-content:space-between;align-items:center;margin-block-end:10px;padding-block-end:10px;border-block-end:1px solid var(--border);">
                                <div style="font-weight:600;color:var(--info-color);"><i class="fas fa-table"></i> <span id="dd-table-record-count">0</span> Records Loaded</div>
                                <button class="btn btn-sm" style="background:#e0f2fe;color:#0369a1;border:none;padding:4px 10px;font-size:0.85em;" onclick="editTableData();">
                                    <i class="fas fa-pencil"></i> Edit
                                </button>
                            </div>
                            <div id="dd-data-table" style="overflow-x:auto;font-size:0.9em;">
                                <!-- Table will be rendered here -->
                            </div>
                        </div>

                        <!-- Hidden textarea for data storage (used for execution) -->
                        <textarea class="form-control" id="dd-data" rows="1" style="display:none;" placeholder="Data storage"></textarea>
                    </div>

                    <!-- Execute Button -->
                    <div style="display:flex;gap:10px;flex-wrap:wrap;">
                        <button class="btn btn-primary" id="dd-execute-btn" onclick="executeDataDriven();" style="flex:1;min-inline-size:250px;">
                            <i class="fas fa-play"></i> Execute Data-Driven Test
                        </button>
                        <button class="btn" style="background:#92400e;color:#white;border:1px solid #b45309;min-inline-size:160px;" onclick="executeDataDrivenParallel();">
                            <i class="fas fa-bolt"></i> Parallel Execution
                        </button>
                    </div>

                    <!-- Results Area -->
                    <div id="dd-results-area" class="hidden" style="margin-block-start:14px;">
                        <div id="dd-summary-bar" style="padding:10px;border-radius:8px;margin-block-end:10px;"></div>
                        <div id="dd-results-table"></div>
                    </div>
                </div>
            </div>

            <!-- Console Output -->
            <div class="card" style="margin-block-start:14px;">
                <div class="card-header">
                    <div class="card-title" style="display:flex;align-items:center;gap:8px;">
                        <i class="fas fa-terminal"></i> Execution Console
                    </div>
                    <button class="btn btn-sm btn-secondary" onclick="clearDDConsole();">
                        <i class="fas fa-eraser"></i> Clear
                    </button>
                </div>
                <div class="console" id="dd-console-output">
                    <div class="console-line">🚀 Data-Driven Testing Engine ready...</div>
                </div>
            </div>
        </div>

        <!-- DATA GENERATION TAB -->
        <div id="ddt-generation-tab" class="ddt-tab" style="display:none;margin-block-start:14px;">
            <div class="card">
                <div class="card-header">
                    <div class="card-title">
                        <i class="fas fa-wand-magic-sparkles"></i> Intelligent Test Data Generation (Features 6-7)
                    </div>
                </div>
                <div style="padding:14px;">
                    <p style="color:#666;font-size:0.88em;margin-block-end:12px;">
                        <strong>Feature 6️⃣:</strong> Generate test data: email, phone, address, name, credit card, UUID, random numbers
                        | <strong>Feature 7️⃣:</strong> AI-assisted generation for edge cases and missing scenarios
                    </p>

                    <div style="display:grid;grid-template-columns:1fr 1fr;gap:12px;margin-block-end:12px;">
                        <div class="form-group">
                            <label>Number of Records</label>
                            <input type="number" class="form-control" id="gen-count" value="10" min="1" max="1000">
                        </div>
                        <div class="form-group">
                            <label>Generation Type</label>
                            <select class="form-control" id="gen-type" onchange="updateGenerationFields();">
                                <option value="custom">Custom Fields</option>
                                <option value="login">Login Test Data</option>
                                <option value="checkout">Checkout Test Data</option>
                                <option value="user">User Profile Data</option>
                            </select>
                        </div>
                    </div>

                    <div id="gen-fields-container" style="display:grid;grid-template-columns:repeat(2,1fr);gap:10px;margin-block-end:12px;">
                        <!-- Custom fields will be added here -->
                    </div>

                    <button class="btn btn-success" onclick="generateTestData();" style="inline-size:100%;margin-block-end:12px;">
                        <i class="fas fa-sparkles"></i> Generate Test Data
                    </button>

                    <div id="generated-data-preview" class="hidden" style="padding:14px;background:#f0fdf4;border-radius:8px;border-inline-start:4px solid var(--success-color);margin-block-start:16px;">
                        <div style="font-weight:600;color:#15803d;margin-block-end:8px;">Generated Data Preview</div>
                        <div id="generated-data-content"></div>
                    </div>
                </div>
            </div>
        </div>


    `;

    viewContent.innerHTML = html;
    viewContent.classList.remove('hidden');

    // Hide other views
    document.querySelectorAll('.view-content').forEach(v => {
        if (v.id !== 'datadriven-view') v.classList.add('hidden');
    });

    // Setup event listeners for tab switching
    setupDDTTabSwitching();

    // Load modules
    await loadModulesForDDT();

    // Refresh analytics
    refreshAnalyticsDashboard();
}

/**
 * Tab switching functionality
 */
function showDDTTab(tabName) {
    ddtUIState.currentTab = tabName;
    
    // Hide all tabs
    document.querySelectorAll('.ddt-tab').forEach(tab => {
        tab.style.display = 'none';
    });

    // Update button styles
    document.querySelectorAll('.tab-btn').forEach(btn => {
        btn.style.borderBottom = 'none';
        btn.style.borderBottomColor = 'transparent';
        btn.style.color = '#666';
    });

    // Show selected tab
    const tabElement = document.getElementById(`ddt-${tabName}-tab`);
    if (tabElement) {
        tabElement.style.display = 'block';
    }

    // Highlight active button
    const activeBtn = document.querySelector(`[data-tab="${tabName}"]`);
    if (activeBtn) {
        activeBtn.style.borderBottom = '3px solid var(--info-color)';
        activeBtn.style.color = 'var(--info-color)';
    }
}

function setupDDTTabSwitching() {
    const style = document.createElement('style');
    style.textContent = `
        .tab-btn {
            padding: 9px 14px;
            background: #f3f4f6;
            border: none;
            border-block-end: 3px solid transparent;
            cursor: pointer;
            color: #666;
            font-size: 0.9em;
            transition: all 0.3s;
        }
        .tab-btn:hover {
            background: #e5e7eb;
            color: #374151;
        }
        .ddt-tab {
            animation: fadeIn 0.3s ease-in;
        }
        @keyframes fadeIn {
            from { opacity: 0; }
            to { opacity: 1; }
        }
    `;
    if (!document.querySelector('style[data-ddt-styles]')) {
        style.setAttribute('data-ddt-styles', 'true');
        document.head.appendChild(style);
    }
}

/**
 * Load modules for DDT
 */
async function loadModulesForDDT() {
    try {
        const modulesResponse = await fetch(`${API_BASE_URL}/scenarios/modules`);
        const modulesData = await modulesResponse.json();
        if (modulesData.success) {
            const moduleSelect = document.getElementById('dd-module');
            moduleSelect.innerHTML = '<option value="">Select Module</option>' +
                modulesData.modules.map(m => `<option value="${m}">${m}</option>`).join('');
        }
    } catch (e) {
        console.error('Error loading modules:', e);
        addDDConsoleLog('Error loading modules: ' + e.message, 'error');
    }
}

/**
 * Load scenarios for selected module
 */
async function loadDDScenarios() {
    const module = document.getElementById('dd-module')?.value;
    if (!module) {
        console.warn('[loadDDScenarios] No module selected');
        return;
    }
    
    console.log('[loadDDScenarios] Loading scenarios for module:', module);
    
    try {
        const r = await fetch(`${API_BASE_URL}/scenarios/module/${module}`);
        
        if (!r.ok) {
            console.error('[loadDDScenarios] API returned status:', r.status);
            throw new Error('API returned ' + r.status);
        }
        
        const d = await r.json();
        console.log('[loadDDScenarios] Received response:', d);
        
        if (d.success && d.scenarios && d.scenarios.length > 0) {
            const scenarioSelect = document.getElementById('dd-scenario');
            const options = '<option value="">Select Scenario</option>' +
                d.scenarios.map(s => `<option value="${s.name}">${s.name}</option>`).join('');
            scenarioSelect.innerHTML = options;
            console.log('[loadDDScenarios] Loaded ' + d.scenarios.length + ' scenarios');
            if (typeof addDDConsoleLog === 'function') {
                addDDConsoleLog('Loaded ' + d.scenarios.length + ' scenarios for ' + module, 'success');
            }
        } else {
            console.warn('[loadDDScenarios] No scenarios returned or success is false');
            const scenarioSelect = document.getElementById('dd-scenario');
            scenarioSelect.innerHTML = '<option value="">No scenarios found</option>';
            if (typeof addDDConsoleLog === 'function') {
                addDDConsoleLog('No scenarios found for module: ' + module, 'warning');
            }
        }
    } catch (e) {
        console.error('[loadDDScenarios] Error:', e);
        const scenarioSelect = document.getElementById('dd-scenario');
        scenarioSelect.innerHTML = '<option value="">Error loading scenarios</option>';
        if (typeof addDDConsoleLog === 'function') {
            addDDConsoleLog('Error loading scenarios: ' + e.message, 'error');
        }
    }
}

/**
 * Data source selection and loading
 */
function loadDataSourcesForModule() {
    const sources = [];
    // This would load available data sources for the module
    refreshDataSourcesList(sources);
}

function selectDataSource() {
    const source = document.getElementById('dd-data-source').value;
    const inlineSection = document.getElementById('inline-data-section');
    
    if (source === 'inline') {
        inlineSection.style.display = 'block';
    } else {
        inlineSection.style.display = 'none';
        if (source) {
            addDDConsoleLog(`Loading data source: ${source}`, 'info');
        }
    }
}

/**
 * Strategy parameters
 */
function updateStrategyParams() {
    const strategy = document.getElementById('dd-strategy').value;
    const countParam = document.getElementById('param-count');
    const tagsParam = document.getElementById('param-tags');

    countParam.style.display = ['random', 'first-n'].includes(strategy) ? 'block' : 'none';
    tagsParam.style.display = strategy === 'tag-based' ? 'block' : 'none';
}

/**
 * Handle file upload - automatically parse and display as table
 */
function handleDDFileUpload(event) {
    const file = event.target.files[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = function (e) {
        const content = e.target.result;
        const isJSON = file.name.toLowerCase().endsWith('.json');
        
        try {
            let data = [];
            if (isJSON) {
                data = JSON.parse(content);
                if (!Array.isArray(data)) data = [data];
                document.getElementById('dd-format').value = 'JSON';
            } else {
                // Parse CSV
                data = parseCSVData(content);
                document.getElementById('dd-format').value = 'CSV';
            }

            // Store as CSV in hidden textarea for execution
            const csv = convertToCSV(data);
            document.getElementById('dd-data').value = csv;
            
            // Display as table
            renderDataTable(data);
            addDDConsoleLog(`✓ Loaded ${file.name}: ${data.length} records`, 'success');
        } catch (error) {
            addDDConsoleLog(`Error parsing file: ${error.message}`, 'error');
        }
    };
    reader.readAsText(file);
    event.target.value = '';
}

/**
 * Parse CSV data
 */
function parseCSVData(csvText) {
    // Handle different line break styles
    const lines = csvText.trim().split(/\r?\n/);
    if (lines.length < 2) return [];
    
    const headers = lines[0].split(',').map(h => h.trim());
    const data = [];
    
    for (let i = 1; i < lines.length; i++) {
        const line = lines[i].trim();
        if (line === '') continue;
        
        // Better CSV parsing that handles quoted values
        const values = [];
        let currentValue = '';
        let inQuotes = false;
        
        for (let j = 0; j < line.length; j++) {
            const char = line[j];
            const nextChar = line[j + 1];
            
            if (char === '"') {
                if (inQuotes && nextChar === '"') {
                    // Escaped quote
                    currentValue += '"';
                    j++; // Skip next quote
                } else {
                    // Toggle quote mode
                    inQuotes = !inQuotes;
                }
            } else if (char === ',' && !inQuotes) {
                // Add value and reset
                values.push(currentValue.trim());
                currentValue = '';
            } else {
                currentValue += char;
            }
        }
        values.push(currentValue.trim());
        
        const row = {};
        headers.forEach((h, idx) => {
            row[h] = values[idx] || '';
        });
        data.push(row);
    }
    
    return data;
}

/**
 * Convert data array to CSV format string
 */
function convertToCSV(dataArray) {
    if (!Array.isArray(dataArray) || dataArray.length === 0) return '';
    
    const headers = Object.keys(dataArray[0]);
    const csv = [
        headers.join(','),
        ...dataArray.map(row => 
            headers.map(header => {
                const value = row[header] || '';
                // Escape quotes and wrap in quotes if contains comma
                if (String(value).includes(',')) {
                    return `"${String(value).replace(/"/g, '""')}"`;
                }
                return value;
            }).join(',')
        )
    ].join('\n');
    
    return csv;
}

/**
 * Render data as interactive table with actions column
 * Dynamically generates table from array of objects
 */
function renderDataTable(data) {
    try {
        if (!Array.isArray(data) || data.length === 0) {
            console.warn('[renderDataTable] No data to display');
            if (typeof addDDConsoleLog === 'function') {
                addDDConsoleLog('No data to display', 'warning');
            }
            return;
        }

        const container = document.getElementById('dd-data-table-container');
        const tableDiv = document.getElementById('dd-data-table');
        const recordCount = document.getElementById('dd-table-record-count');

        if (!tableDiv || !container) {
            console.error('[renderDataTable] Table elements not found in DOM');
            if (typeof addDDConsoleLog === 'function') {
                addDDConsoleLog('Table elements not found in DOM', 'error');
            }
            return;
        }

        console.log('[renderDataTable] Starting render with', data.length, 'rows');

    // Store data for runtime operations
    window.ddtCurrentData = data;
    window.ddtCurrentColumns = Object.keys(data[0]);

    if (recordCount) {
        recordCount.textContent = data.length;
    }
    container.classList.remove('hidden');

    // Get column headers from first element's keys
    const columns = window.ddtCurrentColumns;

    const formatColumnHeader = (columnName) => {
        if (!columnName) return '';
        return String(columnName)
            .replace(/_/g, ' ')
            .replace(/([a-z])([A-Z])/g, '$1 $2')
            .replace(/\s+/g, ' ')
            .trim()
            .replace(/\b\w/g, c => c.toUpperCase());
    };

    // Build professional table HTML
    let tableHTML = '<div style="overflow-x:auto;border-radius:6px;border:1px solid #e5e7eb;box-shadow:0 2px 8px rgba(0,0,0,0.08);">';
    tableHTML += '<table style="inline-size:100%;border-collapse:collapse;background:#fff;table-layout:auto;">';
    tableHTML += '<thead style="background:linear-gradient(135deg, #dbeafe 0%, #bfdbfe 100%);position:sticky;inset-block-start:0;">';
    tableHTML += '<tr>';
    tableHTML += '<th style="padding:12px 10px;border:1px solid #7dd3fc;text-align:center;inline-size:50px;color:#0369a1;font-weight:700;font-size:0.9em;">#</th>';
    
    // Add column headers
    columns.forEach(col => {
        tableHTML += '<th title="' + escapeHtml(col) + '" style="padding:12px 10px;border:1px solid #7dd3fc;text-align:start;color:#0369a1;font-weight:700;font-size:0.9em;white-space:nowrap;">' + escapeHtml(formatColumnHeader(col)) + '</th>';
    });
    
    tableHTML += '<th style="padding:12px 10px;border:1px solid #7dd3fc;text-align:center;color:#0369a1;font-weight:700;font-size:0.9em;inline-size:100px;">Actions</th>';
    tableHTML += '</tr></thead><tbody>';

    // Add data rows
    data.forEach((row, idx) => {
        const bgColor = idx % 2 === 0 ? '#fff' : '#f0f9ff';
        const hoverColor = idx % 2 === 0 ? '#f5f5f5' : '#e0f2fe';
        tableHTML += '<tr style="background:' + bgColor + ';transition:background 0.15s;" data-row-idx="' + idx + '" onmouseover="this.style.background=\'' + hoverColor + '\'" onmouseout="this.style.background=\'' + bgColor + '\'">';
        tableHTML += '<td style="padding:10px;border:1px solid #e5e7eb;text-align:center;color:#666;font-size:0.85em;font-weight:600;vertical-align:middle;">' + (idx + 1) + '</td>';
        
        // Add column values
        columns.forEach(col => {
            const cellValue = row[col] || '';
            tableHTML += '<td style="padding:10px;border:1px solid #e5e7eb;color:#374151;font-size:0.9em;min-inline-size:140px;max-inline-size:320px;white-space:normal;word-break:break-word;vertical-align:top;" title="' + escapeHtml(cellValue) + '">' + escapeHtml(cellValue) + '</td>';
        });
        
        tableHTML += '<td style="padding:10px;border:1px solid #e5e7eb;text-align:center;vertical-align:middle;">';
        tableHTML += '<button class="btn btn-xs btn-danger" onclick="deleteDataRow(' + idx + ')" title="Delete row" style="padding:6px 10px;font-size:0.75em;cursor:pointer;">';
        tableHTML += '<i class="fas fa-trash"></i> Delete</button></td></tr>';
    });

    tableHTML += '</tbody></table></div>';
    tableHTML += '<div style="margin-block-start:16px;display:flex;gap:8px;flex-wrap:wrap;">';
    tableHTML += '<button class="btn btn-sm btn-success" onclick="insertDataRow()" style="padding:10px 14px;font-weight:600;cursor:pointer;">';
    tableHTML += '<i class="fas fa-plus-circle"></i> Insert New Row</button>';
    tableHTML += '<button class="btn btn-sm" onclick="editTableData()" style="padding:10px 14px;font-weight:600;background:#3b82f6;color:#fff;border:none;border-radius:4px;cursor:pointer;">';
    tableHTML += '<i class="fas fa-pencil"></i> Edit Data</button>';
    tableHTML += '<button class="btn btn-sm" onclick="clearTableData()" style="padding:10px 14px;font-weight:600;background:#ef4444;color:#fff;border:none;border-radius:4px;cursor:pointer;">';
    tableHTML += '<i class="fas fa-trash-alt"></i> Clear All</button>';
    tableHTML += '</div>';

    tableDiv.innerHTML = tableHTML;
    console.log('[renderDataTable] Table rendered successfully');
    } catch (e) {
        console.error('[renderDataTable] Error rendering table:', e);
        if (typeof addDDConsoleLog === 'function') {
            addDDConsoleLog('Error rendering table: ' + e.message, 'error');
        }
    }
}

/**
 * Build insert row UI with direct input fields based on columns
 */
function buildInsertRowUI(columns) {
    const rowId = 'row-' + Date.now();
    let formHTML = '<div id="' + rowId + '" style="background:#f0f9ff;border:2px solid #3b82f6;padding:16px;border-radius:8px;margin:16px 0;">';
    formHTML += '<h4 style="margin:0 0 12px 0;color:#1e40af;">New Row - Insert Details</h4>';
    formHTML += '<div style="display:grid;grid-template-columns:repeat(auto-fit,minmax(250px,1fr));gap:12px;margin-block-end:12px;">';
    
    columns.forEach((col, idx) => {
        const isFirst = idx === 0;
        formHTML += '<div style="display:flex;flex-direction:column;">';
        formHTML += '<label style="font-weight:600;color:#374151;font-size:0.9em;margin-block-end:4px;">' + escapeHtml(col) + '</label>';
        formHTML += '<input type="text" id="insert-' + col + '" placeholder="Enter ' + col + '" style="padding:8px;border:1px solid #d1d5db;border-radius:4px;font-family:inherit;font-size:0.95em;" ' + (isFirst ? 'autofocus' : '') + ' />';
        formHTML += '</div>';
    });
    
    formHTML += '</div>';
    formHTML += '<div style="display:flex;gap:8px;justify-content:flex-end;">';
    formHTML += '<button type="button" onclick="cancelInsertRow()" style="padding:8px 16px;background:#e5e7eb;border:1px solid #d1d5db;border-radius:4px;cursor:pointer;font-weight:500;"><i class="fas fa-times"></i> Cancel</button>';
    formHTML += '<button type="button" onclick="confirmInsertRow(\'' + rowId + '\', [' + columns.map(c => '\'' + c + '\'').join(',') + '])" style="padding:8px 16px;background:#10b981;color:#fff;border:none;border-radius:4px;cursor:pointer;font-weight:500;"><i class="fas fa-plus"></i> Add Row</button>';
    formHTML += '</div></div>';
    return formHTML;
}

/**
 * Cancel row insertion
 */
function cancelInsertRow() {
    const container = document.getElementById('dd-insert-row-container');
    if (container) {
        container.innerHTML = '';
        container.style.display = 'none';
    }
    addDDConsoleLog('Row insertion cancelled', 'info');
}

/**
 * Confirm and add new row with validation
 */
function confirmInsertRow(rowId, columns) {
    // Collect values from inputs
    const newRow = {};
    let hasAnyValue = false;
    
    columns.forEach(col => {
        const input = document.getElementById('insert-' + col);
        const value = (input?.value || '').trim();
        newRow[col] = value;
        if (value) {
            hasAnyValue = true;
        }
    });
    
    // Validation: prevent completely empty rows
    if (!hasAnyValue) {
        addDDConsoleLog('Cannot add empty row - please enter at least one value', 'error');
        return;
    }
    
    // Ensure data structures exist
    if (!window.ddtCurrentData) {
        window.ddtCurrentData = [];
    }
    if (!window.ddtCurrentColumns) {
        window.ddtCurrentColumns = columns;
    }
    
    // Add row
    const previousRowCount = window.ddtCurrentData.length;
    window.ddtCurrentData.push(newRow);
    
    // Verify insert
    if (window.ddtCurrentData.length !== previousRowCount + 1) {
        window.ddtCurrentData.pop();
        addDDConsoleLog('Failed to insert row', 'error');
        return;
    }
    
    // Sync to textarea
    const textarea = document.getElementById('dd-data');
    if (textarea) {
        textarea.value = convertToCSV(window.ddtCurrentData);
        textarea.dispatchEvent(new Event('change', { bubbles: true }));
    }
    
    // Re-render table
    renderDataTable(window.ddtCurrentData);
    
    // Clear insert form
    const container = document.getElementById('dd-insert-row-container');
    if (container) {
        container.innerHTML = '';
        container.style.display = 'none';
    }
    
    const rowNum = window.ddtCurrentData.length;
    const filledFields = Object.values(newRow).filter(v => v).length;
    addDDConsoleLog('Row #' + rowNum + ' added successfully with ' + filledFields + ' field(s)', 'success');
}

/**
 * Insert a new row with interactive form
 */
function insertDataRow() {
    // Check if data is loaded
    if (!window.ddtCurrentData || !window.ddtCurrentColumns || window.ddtCurrentColumns.length === 0) {
        const textarea = document.getElementById('dd-data');
        if (textarea && textarea.value.trim()) {
            // Try to recover data from textarea
            const recovered = parseCSVData(textarea.value);
            if (recovered && recovered.length > 0) {
                window.ddtCurrentColumns = Object.keys(recovered[0]);
                window.ddtCurrentData = recovered;
                addDDConsoleLog('Data recovered from textarea: ' + recovered.length + ' rows', 'info');
            } else {
                addDDConsoleLog('No valid data found. Please load or upload a dataset first.', 'warning');
                return;
            }
        } else {
            addDDConsoleLog('No data loaded. Please select a scenario or upload a file.', 'warning');
            return;
        }
    }
    
    // Create insert form container if not exists
    let container = document.getElementById('dd-insert-row-container');
    if (!container) {
        container = document.createElement('div');
        container.id = 'dd-insert-row-container';
        const tableDiv = document.getElementById('dd-data-table');
        if (tableDiv && tableDiv.parentNode) {
            tableDiv.parentNode.insertBefore(container, tableDiv.nextSibling);
        }
    }
    
    // Show insert form
    container.innerHTML = buildInsertRowUI(window.ddtCurrentColumns);
    container.style.display = 'block';
    
    // Auto-focus first input
    setTimeout(() => {
        const firstInput = container.querySelector('input[type="text"]');
        if (firstInput) {
            firstInput.focus();
            firstInput.select();
        }
    }, 100);
    
    addDDConsoleLog('Insert Row form displayed - Enter values for each column', 'info');
}

/**
 * Get scenario to data file mapping
 */
function getScenarioToDataMapping() {
    return {
        'Practice_Test_01': 'TestData/Test_Form_01_Data.json',
        'Practice Test 01': 'TestData/Test_Form_01_Data.json',
        'practise test01': 'TestData/Test_Form_01_Data.json',
        'practise_test01': 'TestData/Test_Form_01_Data.json',
        'Practice_Test_02': 'TestData/SampleUsers.json',
        'Practice_Login': 'TestData/SampleLogin.csv',
        'Login_Test': 'TestData/SampleLogin.csv',
        'User_Management': 'TestData/SampleUsers.json',
        'Checkout_Test': 'TestData/Test_Form_01_Data.json',
        'Form_Validation': 'TestData/TestParameterMapping.csv'
    };
}

function normalizeScenarioKey(name) {
    return String(name || '')
        .toLowerCase()
        .replace(/practice/g, 'practise')
        .replace(/[^a-z0-9]/g, '');
}

function normalizeColumnName(name) {
    let value = String(name || '').trim();
    if (!value) return '';

    const lower = value.toLowerCase();
    if (lower.includes('@') || lower.includes('example.com')) {
        if (lower.includes('name@example.com') || lower.includes('email')) {
            return 'email';
        }
    }

    value = value
        .replace(/[^a-zA-Z0-9_]+/g, '_')
        .replace(/^_+|_+$/g, '')
        .toLowerCase();

    if (value === 'firstname') return 'first_name';
    if (value === 'lastname') return 'last_name';

    return value;
}

function resolveMappedDataFile(scenario) {
    if (!scenario) return null;

    const mapping = getScenarioToDataMapping();
    if (mapping[scenario]) return mapping[scenario];

    const normalizedScenario = normalizeScenarioKey(scenario);
    const matchedKey = Object.keys(mapping).find(key => normalizeScenarioKey(key) === normalizedScenario);
    return matchedKey ? mapping[matchedKey] : null;
}

function getRequiredColumnsForScenario(scenario, module) {
    return [];
}

async function getScenarioColumnsFromApi(module, scenario) {
    if (!module || !scenario) return [];

    try {
        const response = await fetch(`${API_BASE_URL}/scenarios/${encodeURIComponent(module)}/${encodeURIComponent(scenario)}/testdata`);
        if (!response.ok) return [];

        const data = await response.json();
        if (!data?.success || !Array.isArray(data.columns)) return [];

        return data.columns
            .map(c => normalizeColumnName(c))
            .filter(Boolean)
            .filter((column, index, array) => array.findIndex(c => c.toLowerCase() === column.toLowerCase()) === index);
    } catch (error) {
        console.warn('[DDT] Could not fetch scenario column structure:', error);
        return [];
    }
}

function mergeScenarioColumnStructure(data, scenarioColumns, scenario, module) {
    const normalized = normalizeScenarioDataColumns(data, scenario, module);
    if (!Array.isArray(normalized) || normalized.length === 0) {
        return normalized;
    }

    const firstRowKeys = Object.keys(normalized[0]);

    const orderedColumns = (Array.isArray(scenarioColumns) && scenarioColumns.length > 0)
        ? [...scenarioColumns]
        : [...firstRowKeys];

    const dedupedColumns = orderedColumns
        .filter((column, index, array) => array.findIndex(c => String(c).toLowerCase() === String(column).toLowerCase()) === index);

    return normalized.map(row => {
        const orderedRow = {};

        dedupedColumns.forEach(col => {
            const exactKey = Object.keys(row).find(k => k.toLowerCase() === col.toLowerCase()) || col;
            orderedRow[col] = row[exactKey] ?? '';
        });

        return orderedRow;
    });
}

function normalizeScenarioDataColumns(data, scenario, module) {
    if (!Array.isArray(data) || data.length === 0) {
        return data;
    }

    return data.map(row => {
        const normalizedRow = { ...row };

        const fullName = normalizedRow.fullName || normalizedRow.full_name || normalizedRow.name || '';
        if ('first_name' in normalizedRow && !normalizedRow.first_name && fullName) {
            normalizedRow.first_name = String(fullName).trim().split(/\s+/)[0] || '';
        }
        if ('last_name' in normalizedRow && !normalizedRow.last_name && fullName) {
            const parts = String(fullName).trim().split(/\s+/);
            normalizedRow.last_name = parts.length > 1 ? parts.slice(1).join(' ') : '';
        }

        if ('email' in normalizedRow && !normalizedRow.email && normalizedRow.nameexamplecom) {
            normalizedRow.email = normalizedRow.nameexamplecom;
        }

        if ('current_address' in normalizedRow && !normalizedRow.current_address && normalizedRow.address) {
            normalizedRow.current_address = normalizedRow.address;
        }

        return normalizedRow;
    });
}

function resetDDTTableState() {
    window.ddtCurrentColumns = [];
    window.ddtCurrentData = [];

    const textarea = document.getElementById('dd-data');
    const container = document.getElementById('dd-data-table-container');
    const table = document.getElementById('dd-data-table');
    const recordCount = document.getElementById('dd-table-record-count');

    if (textarea) textarea.value = '';
    if (table) table.innerHTML = '';
    if (container) container.classList.add('hidden');
    if (recordCount) recordCount.textContent = '0';
}

function inferColumnFromLocator(locator) {
    if (!locator) return '';

    function toCamel(s) {
        return s.replace(/[-_\s]+(.)/g, (_, c) => c.toUpperCase())
                .replace(/^(input|field|txt|user)[-_]?/i, '')
                .replace(/[-_]?(input|field|txt)$/i, '')
                .replace(/^./, c => c.toLowerCase());
    }

    const idMatch = locator.match(/#([a-zA-Z][\w-]*)/);
    if (idMatch?.[1]) return toCamel(idMatch[1]);

    const nameMatch = locator.match(/name\s*=\s*['"]?([a-zA-Z][\w-]*)['"]?/i);
    if (nameMatch?.[1]) return toCamel(nameMatch[1]);

    const dataIdMatch = locator.match(/data-(?:test(?:id)?|qa)\s*=\s*['"]?([a-zA-Z][\w-]*)['"]?/i);
    if (dataIdMatch?.[1]) return toCamel(dataIdMatch[1]);

    const typeMatch = locator.match(/type\s*=\s*['"]?([a-zA-Z]+)['"]?/i);
    if (typeMatch?.[1]) {
        const t = typeMatch[1].toLowerCase();
        if (['email', 'password', 'tel', 'url', 'search', 'number'].includes(t)) return t;
    }

    const ariaMatch = locator.match(/aria-label\s*=\s*['"]([^'"]+)['"]/i);
    if (ariaMatch?.[1]) return toCamel(ariaMatch[1]);

    const placeholderMatch = locator.match(/placeholder\s*=\s*['"]?([^'"]+)['"]?/i);
    if (placeholderMatch?.[1]) {
        const wordMatch = placeholderMatch[1].match(/\b(email|password|username|name|phone|address|city|zip|postal|first|last|confirm)\b/i);
        if (wordMatch) return wordMatch[1].toLowerCase();
        return toCamel(placeholderMatch[1].trim().split(/\s+/)[0]);
    }

    const lc = locator.toLowerCase();
    if (lc.includes('email')) return 'email';
    if (lc.includes('password')) return 'password';
    if (lc.includes('phone')) return 'phone';
    if (lc.includes('address')) return 'address';
    if (lc.includes('username')) return 'username';
    if (lc.includes('firstname') || lc.includes('first_name') || lc.includes('first-name')) return 'firstName';
    if (lc.includes('lastname') || lc.includes('last_name') || lc.includes('last-name')) return 'lastName';

    return '';
}

async function getRecordedScenarioSeedData(module, scenario, preferredColumns) {
    if (!module || !scenario) return null;

    try {
        const response = await fetch(`${API_BASE_URL}/scenarios/${encodeURIComponent(module)}/${encodeURIComponent(scenario)}`);
        if (!response.ok) return null;

        const payload = await response.json();
        if (!payload?.success || !payload?.scenario) return null;

        const scenarioData = payload.scenario;
        const actions = Array.isArray(scenarioData.steps) && scenarioData.steps.length > 0
            ? scenarioData.steps
                .filter(step => step?.stepType?.toLowerCase() === 'action' && step.action)
                .map(step => step.action)
            : (scenarioData.actions || []);

        const dataEntryTypes = new Set(['type', 'input', 'fill', 'select']);
        const seedRow = {};

        actions.forEach(action => {
            const actionType = (action?.actionType || '').toLowerCase();
            if (!dataEntryTypes.has(actionType)) return;

            const metadataColumn = action?.metadata?.ParameterName || action?.metadata?.parameterName;
            const inferredColumn = inferColumnFromLocator(action?.locator || '');
            const columnName = normalizeColumnName((metadataColumn || inferredColumn || '').trim());

            if (!columnName) return;

            if (!(columnName in seedRow)) {
                seedRow[columnName] = '';
            }

            const value = action?.value ?? '';
            if (value !== null && value !== undefined && String(value).trim() !== '') {
                seedRow[columnName] = String(value);
            }
        });

        // Always ensure preferred columns are present, even if values are blank
        if (Array.isArray(preferredColumns) && preferredColumns.length > 0) {
            const blankRow = {};
            preferredColumns.forEach(col => {
                blankRow[col] = seedRow[col] !== undefined ? seedRow[col] : '';
            });
            // Add any extra columns from seedRow
            Object.keys(seedRow).forEach(col => {
                if (!(col in blankRow)) blankRow[col] = seedRow[col];
            });
            return [blankRow];
        }
        if (Object.keys(seedRow).length === 0) return null;

        const normalizedPreferred = (Array.isArray(preferredColumns) ? preferredColumns : [])
            .map(c => normalizeColumnName(c))
            .filter(Boolean)
            .filter((column, index, array) => array.findIndex(c => c.toLowerCase() === column.toLowerCase()) === index);

        const seedKeys = Object.keys(seedRow);
        const hasSpecificSeedColumns = seedKeys.some(k => !/^field_\d+$/i.test(k));
        const preferredWithoutGeneric = hasSpecificSeedColumns
            ? normalizedPreferred.filter(c => !/^field_\d+$/i.test(c))
            : normalizedPreferred;

        const orderedColumns = preferredWithoutGeneric.length > 0
            ? [...preferredWithoutGeneric, ...seedKeys.filter(k => !preferredWithoutGeneric.some(c => String(c).toLowerCase() === String(k).toLowerCase()))]
            : seedKeys;

        const alignedRow = {};
        orderedColumns.forEach(col => {
            const matchKey = Object.keys(seedRow).find(k => k.toLowerCase() === col.toLowerCase());
            alignedRow[col] = matchKey ? seedRow[matchKey] : '';
        });

        return [alignedRow];
    } catch (error) {
        console.warn('[DDT] Unable to build recorded seed data:', error);
        return null;
    }
}

/**
 * Load sample test data based on scenario selection
 */
function loadSampleData() {
    const scenario = document.getElementById('dd-scenario')?.value || '';
    const module = document.getElementById('dd-module')?.value || '';
    
    if (!scenario || !module) {
        addDDConsoleLog('Please select both Module and Test Scenario first', 'warning');
        return;
    }
    
    if (!window.ddtCurrentColumns || window.ddtCurrentColumns.length === 0) {
        addDDConsoleLog('No columns defined. Please load test data structure first.', 'warning');
        return;
    }
    
    addDDConsoleLog('Loading sample data for: ' + module + ' > ' + scenario, 'info');
    
    // Try to find and load scenario-specific data file
    const mappedFile = resolveMappedDataFile(scenario);
    
    if (mappedFile) {
        fetch(mappedFile)
            .then(response => {
                if (!response.ok) throw new Error('File not found');
                return response.text();
            })
            .then(content => {
                try {
                    let data = [];
                    if (mappedFile.endsWith('.json')) {
                        data = JSON.parse(content);
                        if (!Array.isArray(data)) data = [data];
                    } else if (mappedFile.endsWith('.csv')) {
                        data = parseCSVData(content);
                    }
                    
                    if (data && data.length > 0) {
                        data = normalizeScenarioDataColumns(data, scenario, module);
                        window.ddtCurrentData = data;
                        const csvContent = convertToCSV(data);
                        const textarea = document.getElementById('dd-data');
                        if (textarea) {
                            textarea.value = csvContent;
                            textarea.dispatchEvent(new Event('change', { bubbles: true }));
                        }
                        renderDataTable(data);
                        addDDConsoleLog('Loaded from ' + mappedFile + ': ' + data.length + ' records', 'success');
                        return;
                    }
                } catch (e) {
                    addDDConsoleLog('Could not parse ' + mappedFile + ': ' + e.message, 'warning');
                }
                // Fallback if parsing failed
                loadDefaultSampleData();
            })
            .catch(() => {
                // File not found, use fallback
                loadDefaultSampleData();
            });
    } else {
        // No mapping found, use default
        loadDefaultSampleData();
    }
    
    function loadDefaultSampleData() {
        const recordedSteps = JSON.parse(localStorage.getItem('scenario-' + module + '-' + scenario) || '[]');
        let sampleData = [];
        
        if (recordedSteps.length > 0) {
            sampleData = extractDataFromSteps(recordedSteps, window.ddtCurrentColumns);
        }
        
        if (!sampleData || sampleData.length === 0) {
            sampleData = generateDefaultSampleData(window.ddtCurrentColumns, scenario);
        }

        sampleData = normalizeScenarioDataColumns(sampleData, scenario, module);
        
        if (sampleData && sampleData.length > 0) {
            window.ddtCurrentData = sampleData;
            const csvContent = convertToCSV(sampleData);
            const textarea = document.getElementById('dd-data');
            if (textarea) {
                textarea.value = csvContent;
                textarea.dispatchEvent(new Event('change', { bubbles: true }));
            }
            renderDataTable(sampleData);
            addDDConsoleLog('Loaded sample data: ' + sampleData.length + ' record(s)', 'success');
        }
    }
}

/**
 * Extract test data from recorded scenario steps
 */
function extractDataFromSteps(steps, columns) {
    const extractedData = [];
    try {
        steps.forEach(step => {
            const testCase = {};
            columns.forEach(col => {
                // Look for data in step attributes
                testCase[col] = step[col] || step.value || step.text || '';
            });
            // Only add if at least one column has data
            if (Object.values(testCase).some(v => v)) {
                extractedData.push(testCase);
            }
        });
    } catch (e) {
        console.log('Could not extract data from steps:', e.message);
    }
    return extractedData;
}

/**
 * Delete a row from the data table
 */
function deleteDataRow(rowIndex) {
    if (!window.ddtCurrentData || rowIndex < 0 || rowIndex >= window.ddtCurrentData.length) {
        addDDConsoleLog('Invalid row index', 'error');
        return;
    }

    // Preserve the row being deleted for potential rollback
    const deletedRow = window.ddtCurrentData[rowIndex];
    const previousRowCount = window.ddtCurrentData.length;

    // Remove the row from data array
    window.ddtCurrentData.splice(rowIndex, 1);
    
    // Verify deletion was successful
    if (window.ddtCurrentData.length !== previousRowCount - 1) {
        addDDConsoleLog('Error: Failed to delete row properly', 'error');
        // Rollback
        window.ddtCurrentData.splice(rowIndex, 0, deletedRow);
        return;
    }

    // Convert to CSV and update both textarea and data store
    const csvContent = convertToCSV(window.ddtCurrentData);
    
    // Update textarea with new CSV
    const textarea = document.getElementById('dd-data');
    if (textarea) {
        textarea.value = csvContent;
        // Also trigger change event to ensure browser recognizes the change
        textarea.dispatchEvent(new Event('change', { bubbles: true }));
    }

    // Re-render the table
    renderDataTable(window.ddtCurrentData);
    addDDConsoleLog(`✓ Row ${rowIndex + 1} deleted (Remaining: ${window.ddtCurrentData.length} rows)`, 'success');
}

/**
 * Auto-load test data structure and sample data from selected scenario
 * This function is called when a scenario is selected
 */
async function autoLoadScenarioData() {
    const module = document.getElementById('dd-module')?.value;
    const scenario = document.getElementById('dd-scenario')?.value;
    resetDDTTableState();
    const scenarioColumns = await getScenarioColumnsFromApi(module, scenario);
    
    console.log('[AutoLoad] Selected module:', module, 'scenario:', scenario);
    
    if (!scenario || !module) {
        console.warn('[AutoLoad] Module and scenario must be selected');
        if (typeof addDDConsoleLog === 'function') {
            addDDConsoleLog('Module and scenario must be selected', 'warning');
        }
        return;
    }

    console.log('[AutoLoad] Loading scenario data structure: ' + module + ' > ' + scenario);
    if (typeof addDDConsoleLog === 'function') {
        addDDConsoleLog('Loading scenario data structure: ' + module + ' > ' + scenario, 'info');
    }
    
    // Use our scenario mapping system which works reliably
    const mappedFile = resolveMappedDataFile(scenario);
    
    console.log('[AutoLoad] Mapped file:', mappedFile);

    // Primary source: selected scenario recording (actions + metadata)
    const recordedSeedData = await getRecordedScenarioSeedData(module, scenario, scenarioColumns);
    if (recordedSeedData && recordedSeedData.length > 0) {
        const data = mergeScenarioColumnStructure(recordedSeedData, scenarioColumns, scenario, module);
        window.ddtCurrentColumns = Object.keys(data[0] || {});
        window.ddtCurrentData = data;

        const textarea = document.getElementById('dd-data');
        if (textarea) {
            textarea.value = convertToCSV(data);
            textarea.dispatchEvent(new Event('change', { bubbles: true }));
        }

        renderDataTable(data);
        const msg = 'Loaded from selected scenario recording: ' + data.length + ' record(s)';
        console.log('[AutoLoad] ' + msg);
        if (typeof addDDConsoleLog === 'function') {
            addDDConsoleLog(msg, 'success');
        }
        return;
    }
    
    if (mappedFile) {
        try {
            const response = await fetch(mappedFile);
            if (!response.ok) throw new Error('File not found: ' + response.status);
            const content = await response.text();
            
            let data = [];
            if (mappedFile.endsWith('.json')) {
                data = JSON.parse(content);
                if (!Array.isArray(data)) data = [data];
            } else if (mappedFile.endsWith('.csv')) {
                data = parseCSVData(content);
            }
            
            console.log('[AutoLoad] Loaded data:', data.length, 'records');
            
            if (data && data.length > 0) {
                data = mergeScenarioColumnStructure(data, scenarioColumns, scenario, module);
                // Extract columns from first row
                window.ddtCurrentColumns = Object.keys(data[0]);
                window.ddtCurrentData = data;
                
                // Sync to textarea
                const textarea = document.getElementById('dd-data');
                if (textarea) {
                    textarea.value = convertToCSV(data);
                    textarea.dispatchEvent(new Event('change', { bubbles: true }));
                }
                
                // Render table with extracted columns
                renderDataTable(data);
                const msg = 'Loaded from ' + mappedFile + ': ' + data.length + ' records, ' + window.ddtCurrentColumns.length + ' columns';
                console.log('[AutoLoad] ' + msg);
                if (typeof addDDConsoleLog === 'function') {
                    addDDConsoleLog(msg, 'success');
                }
                return;
            }
        } catch (e) {
            console.error('[AutoLoad] Error loading file:', e);
            if (typeof addDDConsoleLog === 'function') {
                addDDConsoleLog('Could not load ' + mappedFile + ': ' + e.message, 'warning');
            }
        }
    }
    
    // Fallback: Try to load from recorded steps or generate template
    console.log('[AutoLoad] Using fallback data generation');
    let data = [];
    let columns = [];
    
    if (scenario.toLowerCase().includes('login')) {
        columns = scenarioColumns.length > 0 ? scenarioColumns : ['username', 'password', 'tag'];
        data = generateDefaultSampleData(columns, scenario);
    } else if (scenario.toLowerCase().includes('checkout')) {
        columns = scenarioColumns.length > 0 ? scenarioColumns : ['product', 'quantity', 'amount', 'tag'];
        data = generateDefaultSampleData(columns, scenario);
    } else if (scenario.toLowerCase().includes('register')) {
        columns = scenarioColumns.length > 0 ? scenarioColumns : ['first_name', 'last_name', 'email', 'phone', 'tag'];
        data = generateDefaultSampleData(columns, scenario);
    } else if (scenario.toLowerCase().includes('form') || scenario.toLowerCase().includes('test')) {
        columns = scenarioColumns.length > 0 ? scenarioColumns : ['first_name', 'last_name', 'email', 'phone', 'tag'];
        data = generateDefaultSampleData(columns, scenario);
    } else {
        columns = scenarioColumns.length > 0 ? scenarioColumns : ['test_data', 'tag'];
        data = generateDefaultSampleData(columns, scenario);
    }

    console.log('[AutoLoad] Generated fallback data with columns:', columns);
    
    if (!data || data.length === 0) {
        data = generateDefaultSampleData(columns, scenario);
    }

    data = mergeScenarioColumnStructure(data, scenarioColumns, scenario, module);

    window.ddtCurrentColumns = Object.keys(data[0] || {}).length > 0 ? Object.keys(data[0]) : columns;
    window.ddtCurrentData = data;
    
    const textarea = document.getElementById('dd-data');
    if (textarea) {
        textarea.value = convertToCSV(data);
    }
    
    renderDataTable(data);
    const msg = '✓ Using fallback structure: [' + columns.join(', ') + ']';
    console.log('[AutoLoad] ' + msg);
    if (typeof addDDConsoleLog === 'function') {
        addDDConsoleLog(msg, 'info');
    }
}

/**
 * Generate sample data from actual scenario actions
 */
function generateSampleDataFromScenario(scenario, columns) {
    if (!scenario || !scenario.actions || scenario.actions.length === 0) {
        return null;
    }

    const typeActions = scenario.actions.filter(a => a.actionType && a.actionType.toLowerCase() === 'type');
    if (typeActions.length === 0) return null;

    // Extract values from type actions to create sample data
    const sampleRow = {};
    columns.forEach(col => {
        sampleRow[col] = '';
    });

    // Map type action values to columns
    typeActions.forEach((action, idx) => {
        if (idx < columns.length - 1) { // -1 for 'tag' column
            const colName = columns[idx];
            sampleRow[colName] = action.value || '';
        }
    });

    if (!sampleRow.tag) sampleRow.tag = 'smoke';

    return [sampleRow];
}

/**
 * Generate realistic sample data based on column names
 */
function generateDefaultSampleData(columns, scenario) {
    const data = [];
    const samples = [
        {
            first_name: 'John', last_name: 'Doe', email: 'john.doe@test.com', username: 'john_doe', password: 'Test@123', 
            name: 'John Doe', phone: '1234567890', mobile_number: '9876543210', address: '123 Main St', current_address: '123 Main Street',
            product: 'Laptop', quantity: '1', amount: '999.99', age: '25', city: 'New York', state: 'NY', country: 'USA', tag: 'smoke'
        },
        {
            first_name: 'Jane', last_name: 'Smith', email: 'jane.smith@test.com', username: 'jane_smith', password: 'Sec@456', 
            name: 'Jane Smith', phone: '9876543210', mobile_number: '8765432109', address: '456 Oak Ave', current_address: '456 Oak Avenue',
            product: 'Monitor', quantity: '2', amount: '599.98', age: '30', city: 'Los Angeles', state: 'CA', country: 'USA', tag: 'regression'
        },
        {
            first_name: 'Robert', last_name: 'Johnson', email: 'robert.j@test.com', username: 'robert_j', password: 'Pass@789', 
            name: 'Robert Johnson', phone: '5555555555', mobile_number: '4444444444', address: '789 Pine Rd', current_address: '789 Pine Road',
            product: 'Keyboard', quantity: '3', amount: '299.97', age: '28', city: 'Chicago', state: 'IL', country: 'USA', tag: 'edge-case'
        }
    ];

    samples.forEach(sample => {
        const row = {};
        columns.forEach(col => {
            row[col] = sample[col] || '';
        });
        data.push(row);
    });

    return data;
}

/**
 * Clear table data
 */
function clearTableData() {
    const textarea = document.getElementById('dd-data');
    const container = document.getElementById('dd-data-table-container');
    const table = document.getElementById('dd-data-table');
    
    if (textarea) textarea.value = '';
    if (container) container.classList.add('hidden');
    if (table) table.innerHTML = '';
    
    addDDConsoleLog('Data cleared', 'info');
}

/**
 * Edit table data
 */
function editTableData() {
    const table = document.getElementById('dd-data-table');
    const editBtn = document.querySelector('button[onclick="editTableData()"]');
    
    if (!table) return;
    
    const isEditing = table.getAttribute('data-editing') === 'true';
    
    if (!isEditing) {
        // Enter edit mode
        const cells = table.querySelectorAll('tbody td:not(:first-child):not(:last-child)');
        cells.forEach(cell => {
            const originalText = cell.textContent;
            cell.style.cursor = 'text';
            cell.style.backgroundColor = '#fff9e6';
            cell.title = 'Click to edit';
            cell.addEventListener('click', function(e) {
                if (this.querySelector('input')) return;
                const input = document.createElement('input');
                input.type = 'text';
                input.value = this.textContent;
                input.style.width = '100%';
                input.style.padding = '4px';
                input.style.border = '1px solid #fbbf24';
                input.style.borderRadius = '4px';
                this.textContent = '';
                this.appendChild(input);
                input.focus();
                input.select();
                
                const saveEdit = () => {
                    this.textContent = input.value || originalText;
                    this.style.backgroundColor = '#fff9e6';
                };
                
                input.addEventListener('blur', saveEdit);
                input.addEventListener('keypress', (e) => {
                    if (e.key === 'Enter') saveEdit();
                });
            });
        });
        table.setAttribute('data-editing', 'true');
        editBtn.innerHTML = '<i class="fas fa-save"></i> Save Changes';
        editBtn.style.background = '#10b981';
        editBtn.style.color = '#fff';
        addDDConsoleLog('Edit mode enabled - Click cells to edit values', 'info');
    } else {
        // Exit edit mode - collect changes from DOM table (most current state)
        const rows = table.querySelectorAll('tbody tr');
        const headers = Array.from(table.querySelectorAll('thead th')).slice(1).map(h => h.textContent).slice(0, -1);
        const updatedData = [];
        
        rows.forEach(row => {
            const cells = row.querySelectorAll('td');
            const rowData = {};
            headers.forEach((header, idx) => {
                const cellValue = cells[idx + 1]?.textContent || '';
                rowData[header] = cellValue;
            });
            updatedData.push(rowData);
        });
        
        // CRITICAL: Always update BOTH window.ddtCurrentData and textarea to keep them in sync
        // This ensures data doesn't get lost during insert/edit/execute cycles
        window.ddtCurrentData = updatedData;
        window.ddtCurrentColumns = headers;
        
        // CRITICAL: Update hidden textarea with new CSV data
        // This is the data source for API execution - must always be in sync
        const textarea = document.getElementById('dd-data');
        if (textarea && updatedData.length > 0) {
            const csvContent = convertToCSV(updatedData);
            textarea.value = csvContent;
            textarea.dispatchEvent(new Event('change', { bubbles: true }));
            addDDConsoleLog(`✓ Saved and synced ${updatedData.length} rows to data store (Ready for execution)`, 'success');
        } else if (updatedData.length === 0) {
            addDDConsoleLog('Warning: No data in table - please add rows before saving', 'warning');
        }
        
        // Exit edit mode
        const cells = table.querySelectorAll('tbody td');
        cells.forEach(cell => {
            cell.style.cursor = 'default';
            cell.style.backgroundColor = '';
            cell.title = '';
        });
        table.setAttribute('data-editing', 'false');
        editBtn.innerHTML = '<i class="fas fa-pencil"></i> Edit';
        editBtn.style.background = '';
        editBtn.style.color = '';
    }
}

/**
 * Load sample data
 */
function loadSampleData() {
    const sampleData = [
        { username: 'user1@test.com', password: 'TestPass123!', role: 'admin', tag: 'smoke' },
        { username: 'user2@test.com', password: 'SecurePass456!', role: 'user', tag: 'regression' },
        { username: 'user3@test.com', password: 'ComplexPass789!', role: 'guest', tag: 'smoke' }
    ];
    
    // Store as CSV in hidden textarea
    document.getElementById('dd-data').value = convertToCSV(sampleData);
    
    // Display as table
    renderDataTable(sampleData);
    
    addDDConsoleLog('Sample data loaded: 3 records', 'success');
}

/**
 * Validate loaded data (Feature 4️⃣)
 */
async function validateLoadedData() {
    const dataContent = document.getElementById('dd-data')?.value?.trim();
    if (!dataContent) {
        addDDConsoleLog('No data to validate', 'warning');
        return;
    }

    try {
        const formatElement = document.getElementById('dd-format');
        const dataFormat = formatElement?.value || 'CSV';
        
        const r = await fetch(`${API_BASE_URL}/datadriven/preview`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ 
                dataFormat: dataFormat, 
                dataContent 
            })
        });
        const d = await r.json();

        if (d.success) {
            const area = document.getElementById('dd-preview-area');
            const content = document.getElementById('dd-preview-content');
            area.classList.remove('hidden');

            // Use clean column names from window.ddtCurrentColumns if available, otherwise use API columns
            const displayColumns = (window.ddtCurrentColumns && window.ddtCurrentColumns.length > 0) ? window.ddtCurrentColumns : d.columns;

            content.innerHTML = `
                <div style="margin-block-end:10px;">
                    <strong style="color:#0369a1;">${d.rowCount} row(s)</strong> · 
                    <span style="color:#059669;">✓ Valid Schema</span>
                </div>
                <div style="margin-block-end:10px;color:#666;font-size:0.9em;">
                    Columns: ${displayColumns.map(c => `<code style="background:#dbeafe;padding:2px 6px;border-radius:4px;margin:0 2px;">${escapeHtml(c)}</code>`).join(' ')}
                </div>
                <div style="overflow-x:auto;">
                    <table style="font-size:0.85em;border-collapse:collapse;inline-size:100%;">
                        <thead style="background:#bfdbfe;">
                            <tr>${displayColumns.map(c => `<th style="padding:6px 10px;text-align:start;border:1px solid #93c5fd;">${escapeHtml(c)}</th>`).join('')}</tr>
                        </thead>
                        <tbody>
                            ${(d.preview || []).slice(0, 3).map(row => `
                                <tr style="background:#fff;">
                                    ${displayColumns.map(c => `<td style="padding:6px 10px;border:1px solid #e2e8f0;">${escapeHtml(row[c] || '')}</td>`).join('')}
                                </tr>`).join('')}
                            ${d.rowCount > 3 ? `<tr style="background:#f9fafb;"><td colspan="${displayColumns.length}" style="padding:10px;text-align:center;color:#999;">... and ${d.rowCount - 3} more rows</td></tr>` : ''}
                        </tbody>
                    </table>
                </div>`;

            addDDConsoleLog(`✓ Validation passed: ${d.rowCount} rows, ${displayColumns.length} columns`, 'success');
            const transformBtn = document.getElementById('transform-btn');
            if (transformBtn) {
                transformBtn.style.display = 'block';
            }
        } else {
            addDDConsoleLog('Validation failed: ' + d.error, 'error');
        }
    } catch (e) {
        addDDConsoleLog('Validation error: ' + e.message, 'error');
    }
}

/**
 * Show transform options (Feature 5️⃣)
 */
function showTransformOptions() {
    addDDConsoleLog('Data transformation pipeline: trim → validate → transform → execute', 'info');
    // Could open a modal with transformation options
}

/**
 * Generate test data (Features 6️⃣ & 7️⃣)
 */
function generateTestData() {
    const count = parseInt(document.getElementById('gen-count').value) || 10;
    const type = document.getElementById('gen-type').value;

    let fields = {};
    switch (type) {
        case 'login':
            fields = {
                email: 'email',
                password: 'string',
                rememberMe: 'boolean',
                tag: 'string'
            };
            break;
        case 'checkout':
            fields = {
                product: 'string',
                quantity: 'number',
                cardNumber: 'creditcard',
                cvv: 'number',
                tag: 'string'
            };
            break;
        case 'user':
            fields = {
                name: 'name',
                email: 'email',
                phone: 'phone',
                address: 'address',
                tag: 'string'
            };
            break;
        default:
            fields = { field1: 'string', field2: 'string', field3: 'number' };
    }

    const result = dataEngine.generateTestData({ count, fields });
    
    if (result.success) {
        const preview = document.getElementById('generated-data-preview');
        const content = document.getElementById('generated-data-content');
        preview.classList.remove('hidden');

        content.innerHTML = `
            <div style="margin-block-end:12px;display:flex;justify-content:space-between;align-items:center;">
                <div style="color:#059669;font-weight:500;">✓ Generated ${result.count} test records</div>
                <button class="btn btn-sm" style="background:#10b981;color:#fff;border:none;padding:6px 12px;border-radius:4px;cursor:pointer;" onclick="editAIGeneratedData()">
                    <i class="fas fa-pencil"></i> Edit
                </button>
            </div>
            <div id="ai-generated-table-container" style="overflow-x:auto;">
                <table style="font-size:0.85em;border-collapse:collapse;inline-size:100%;" id="ai-generated-table">
                    <thead style="background:#ecfdf5;">
                        <tr>${Object.keys(fields).map(f => `<th style="padding:6px 10px;text-align:start;border:1px solid #a7f3d0;font-weight:600;">${f}</th>`).join('')}</tr>
                    </thead>
                    <tbody>
                        ${result.data.slice(0, 5).map((row, idx) => `
                            <tr style="background:${idx % 2 === 0 ? '#fff' : '#f0fdf4'};">
                                ${Object.keys(fields).map(f => `<td style="padding:6px 10px;border:1px solid #d1fae5;">${escapeHtml(row[f])}</td>`).join('')}
                            </tr>`).join('')}
                        ${result.count > 5 ? `<tr style="background:#f0fdf4;"><td colspan="${Object.keys(fields).length}" style="padding:10px;text-align:center;color:#999;">... and ${result.count - 5} more rows</td></tr>` : ''}
                    </tbody>
                </table>
            </div>
            <div id="ai-generated-data-store" style="display:none;">${JSON.stringify(result.data)}</div>
        `;

        addDDConsoleLog(`AI Generated ${result.count} test records for ${type} scenario`, 'success');
    }
}

/**
 * Edit AI Generated Data
 */
function editAIGeneratedData() {
    const table = document.getElementById('ai-generated-table');
    const editBtn = document.querySelector('button[onclick="editAIGeneratedData()"]');
    const container = document.getElementById('ai-generated-table-container');
    
    if (!table) return;
    
    const isEditing = table.getAttribute('data-editing') === 'true';
    
    if (!isEditing) {
        // Enter edit mode
        const cells = table.querySelectorAll('tbody td');
        cells.forEach(cell => {
            const originalText = cell.textContent;
            cell.style.cursor = 'text';
            cell.style.backgroundColor = '#fff9e6';
            cell.title = 'Click to edit';
            cell.addEventListener('click', function(e) {
                if (this.querySelector('input')) return;
                const input = document.createElement('input');
                input.type = 'text';
                input.value = this.textContent;
                input.style.width = '100%';
                input.style.padding = '4px';
                input.style.border = '1px solid #fbbf24';
                input.style.borderRadius = '4px';
                input.style.boxSizing = 'border-box';
                this.textContent = '';
                this.appendChild(input);
                input.focus();
                input.select();
                
                const saveEdit = () => {
                    this.textContent = input.value || originalText;
                    this.style.backgroundColor = '#fff9e6';
                };
                
                input.addEventListener('blur', saveEdit);
                input.addEventListener('keypress', (e) => {
                    if (e.key === 'Enter') saveEdit();
                });
            });
        });
        table.setAttribute('data-editing', 'true');
        editBtn.innerHTML = '<i class="fas fa-save"></i> Save Changes';
        editBtn.style.background = '#10b981';
        editBtn.style.color = '#fff';
        addDDConsoleLog('AI Data: Edit mode enabled - Click cells to edit', 'info');
    } else {
        // Exit edit mode
        const cells = table.querySelectorAll('tbody td');
        cells.forEach(cell => {
            cell.style.cursor = 'default';
            cell.style.backgroundColor = '';
            cell.title = '';
        });
        table.setAttribute('data-editing', 'false');
        editBtn.innerHTML = '<i class="fas fa-pencil"></i> Edit';
        editBtn.style.background = '';
        editBtn.style.color = '';
        addDDConsoleLog('✓ AI Generated data changes saved', 'success');
    }
}

/**
 * Rebuild dataset from current table DOM
 * This ensures execution uses the latest edited data, not cached data
 * Returns: { columns: [...], data: [{...}, {...}] }
 */
function rebuildDatasetFromDOM() {
    const table = document.getElementById('dd-data-table');
    if (!table) {
        console.warn('Table #dd-data-table not found in DOM');
        return null;
    }
    
    // Step 1: Extract headers from table thead
    const headerCells = table.querySelectorAll('thead th');
    if (headerCells.length === 0) {
        console.warn('No table headers found');
        return null;
    }
    
    // Get header text, skip first (#) and last (Actions)
    const headers = [];
    for (let i = 1; i < headerCells.length - 1; i++) {
        const headerText = headerCells[i].textContent.trim();
        if (headerText && headerText !== 'Actions') {
            headers.push(headerText);
        }
    }
    
    if (headers.length === 0) {
        console.warn('No valid columns extracted from table headers');
        return null;
    }
    
    // Step 2: Extract data rows from table tbody
    const rows = table.querySelectorAll('tbody tr');
    const dataArray = [];
    
    rows.forEach((row, rowIndex) => {
        const cells = row.querySelectorAll('td');
        if (cells.length === 0) return; // Skip empty rows
        
        const rowData = {};
        let hasData = false;
        
        // Extract each cell value and map to column header
        // cells[0] is row number, cells[1..n] are data, cells[last] is actions
        for (let i = 0; i < headers.length; i++) {
            const cellIndex = i + 1; // Offset by 1 to skip row number column
            const cellValue = cells[cellIndex]?.textContent?.trim() || '';
            rowData[headers[i]] = cellValue;
            if (cellValue) hasData = true;
        }
        
        // Only add rows that have at least some data
        if (hasData) {
            dataArray.push(rowData);
        }
    });
    
    console.log('Rebuilt dataset from DOM:', {
        headerCount: headers.length,
        rowCount: dataArray.length,
        headers: headers,
        sampleRow: dataArray[0] || null
    });
    
    return {
        columns: headers,
        data: dataArray
    };
}

/**
 * Sync Latest Data From DOM to Execution
 * CRITICAL: Call this before every execution to ensure latest table data is used
 * Updates: window.ddtCurrentData, window.ddtCurrentColumns, textarea #dd-data
 */
function syncDOMDataBeforeExecution() {
    const rebuilt = rebuildDatasetFromDOM();
    
    if (!rebuilt || !rebuilt.data || rebuilt.data.length === 0) {
        addDDConsoleLog('No data in table. Please load or insert test data.', 'warning');
        return false;
    }
    
    try {
        // Update global state from DOM (3-tier sync)
        const previousRowCount = window.ddtCurrentData?.length || 0;
        window.ddtCurrentColumns = rebuilt.columns;
        window.ddtCurrentData = rebuilt.data;
        
        // Convert to CSV format
        const csvContent = convertToCSV(rebuilt.data);
        
        // Update hidden textarea with CSV (backup source)
        const textarea = document.getElementById('dd-data');
        if (textarea) {
            textarea.value = csvContent;
            textarea.dispatchEvent(new Event('change', { bubbles: true }));
        }
        
        // Log sync completion
        console.log('Synced dataset before execution:', {
            rowsSynced: rebuilt.data.length,
            columnsSynced: rebuilt.columns.length,
            csvLines: csvContent.split('\n').length,
            previousRows: previousRowCount
        });
        
        // Show notification if data changed
        if (previousRowCount !== rebuilt.data.length) {
            addDDConsoleLog('✓ Synced table data: ' + rebuilt.data.length + ' rows (was ' + previousRowCount + ')', 'info');
        }
        
        return true;
    } catch (error) {
        console.error('Error syncing DOM data:', error);
        addDDConsoleLog('Error syncing table data: ' + error.message, 'error');
        return false;
    }
}

/**
 * Execute data-driven test with latest table data
 * CRITICAL FLOW:
 * 1. Sync DOM table → Memory array → CSV
 * 2. Build execution payload with dataset array
 * 3. POST dataset array to /datadriven/execute endpoint
 * 4. Engine processes each dataset row
 */
async function executeDataDriven() {
    const module = document.getElementById('dd-module')?.value;
    const scenario = document.getElementById('dd-scenario')?.value;
    let dataFormat = document.getElementById('dd-format')?.value || 'CSV';

    if (!module || !scenario) {
        addDDConsoleLog('Please select a module and scenario', 'warning');
        return;
    }

    // STEP 1: SYNC DATA FROM TABLE DOM
    // This reads the actual table visible to the user and rebuilds the dataset
    console.log('[Execute] Step 1: Syncing data from table DOM...');
    const syncSuccess = syncDOMDataBeforeExecution();
    if (!syncSuccess) {
        addDDConsoleLog('Failed to extract data from table', 'error');
        return;
    }

    // STEP 2: GET SYNCED DATASET FROM MEMORY
    // After sync, window.ddtCurrentData contains the fresh dataset
    const datasetArray = window.ddtCurrentData;
    if (!datasetArray || datasetArray.length === 0) {
        addDDConsoleLog('No data in table. Please load or insert test data before executing.', 'warning');
        return;
    }

    // Table data is synced as CSV for execution payload; force matching format
    dataFormat = 'CSV';
    const formatSelector = document.getElementById('dd-format');
    if (formatSelector) {
        formatSelector.value = 'CSV';
    }

    const btn = document.getElementById('dd-execute-btn');
    btn.disabled = true;
    btn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Executing...';
    ddtUIState.isExecuting = true;
    
    // Clear previous logs and capture execution start
    clearExecutionLogs();
    const executionStartTime = Date.now();
    ddtUIState.lastExecutionTime = new Date();

    try {
        const rowCount = datasetArray.length;
        const dataContent = document.getElementById('dd-data')?.value?.trim() || '';
        
        console.log('[Execute] Step 2: Building execution payload...', {
            module,
            scenario,
            rowCount,
            columns: window.ddtCurrentColumns,
            datasetArray: datasetArray
        });
        
        addDDConsoleLog('[Execution] Starting "' + scenario + '" with ' + rowCount + ' dataset row(s)...', 'info');
        addExecutionLog('Scenario: ' + scenario, 'info');
        addExecutionLog('Module: ' + module, 'info');
        addExecutionLog('Strategy: ' + (document.getElementById('dd-strategy')?.value || 'run-all'), 'info');
        addExecutionLog('Data Format: ' + dataFormat + ' | Records: ' + rowCount, 'info');

        // STEP 3: BUILD EXECUTION PAYLOAD
        // Send both CSV (for backward compatibility) and JSON array (for new engine)
        const executionPayload = {
            scenarioName: scenario, 
            module: module, 
            dataFormat: dataFormat,
            // CSV format (backup - for legacy endpoints)
            dataContent: dataContent,
            // JSON dataset array (primary - for new engine)
            datasetArray: datasetArray,
            datasetColumns: window.ddtCurrentColumns,
            // Execution info
            strategy: document.getElementById('dd-strategy').value,
            rowCount: rowCount
        };
        
        console.log('[Execute] Step 3: Sending execution payload to API...', executionPayload);
        
        // STEP 4: POST TO EXECUTION ENGINE
        const r = await fetch(API_BASE_URL + '/datadriven/execute', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(executionPayload)
        });
        // STEP 5: HANDLE EXECUTION RESPONSE
        console.log('[Execute] Step 5: Received response from engine');
        const d = await r.json();

        if (d.success) {
            const executionDuration = Date.now() - executionStartTime;
            const avgTimePerRow = rowCount > 0 ? (executionDuration / rowCount).toFixed(2) : 0;
            
            console.log('[Execute] SUCCESS - Execution completed for (' + rowCount + ' rows)', {
                passed: d.passed,
                failed: d.failed,
                totalRows: d.totalRows,
                duration: executionDuration
            });
            
            addDDConsoleLog('✓ Execution completed. Passed: ' + d.passed + '/' + d.totalRows + ', Failed: ' + d.failed, d.failed > 0 ? 'warning' : 'success');
            addExecutionLog('Dataset Rows Executed: ' + rowCount, 'info');
            addExecutionLog('Execution Status: ' + (d.failed === 0 ? 'PASSED' : 'FAILED'), d.failed === 0 ? 'success' : 'warning');
            addExecutionLog('Total Duration: ' + (executionDuration / 1000).toFixed(2) + 's', 'info');
            addExecutionLog('Avg Time/Row: ' + avgTimePerRow + 'ms', 'info');
            addExecutionLog('Passed: ' + d.passed + ' | Failed: ' + d.failed + ' | Total: ' + d.totalRows, 'info');
            
            // Track execution in engine
            if (typeof dataEngine !== 'undefined') {
                dataEngine.trackExecution('exec-' + Date.now(), scenario, { rows: d.totalRows }, d.failed === 0 ? 'PASS' : 'FAIL', {
                    duration: executionDuration,
                    logs: ddtUIState.executionLogs
                });
            }

            // Refresh analytics
            if (typeof refreshAnalyticsDashboard === 'function') {
                refreshAnalyticsDashboard();
            }

            // Pass logs to results renderer
            renderDataDrivenResults({
                success: d.success,
                passed: d.passed,
                failed: d.failed,
                totalRows: d.totalRows,
                results: d.results,
                executionLogs: ddtUIState.executionLogs,
                executionDuration: executionDuration,
                executionTime: ddtUIState.lastExecutionTime,
                datasetUsed: {
                    rows: rowCount,
                    columns: window.ddtCurrentColumns
                }
            });
        } else {
            console.error('[Execute] FAILED - Server returned error', d);
            addDDConsoleLog('[Execution] Failed: ' + d.error, 'error');
            addExecutionLog('Execution Failed: ' + d.error, 'error');
            addExecutionLog('Dataset Rows Attempted: ' + rowCount, 'warning');
        }
    } catch (e) {
        console.error('[Execute] EXCEPTION - Execution failed', e);
        addDDConsoleLog('[Execution] Error: ' + e.message, 'error');
        addExecutionLog('Execution Error: ' + e.message, 'error');
        addExecutionLog('Dataset Rows Attempted: ' + (datasetArray?.length || 0), 'warning');
    } finally {
        btn.disabled = false;
        btn.innerHTML = '<i class="fas fa-play"></i> Execute Data-Driven Test';
        ddtUIState.isExecuting = false;
    }
}

/**
 * Execute data-driven test in parallel
 */
async function executeDataDrivenParallel() {
    const module = document.getElementById('dd-module')?.value;
    const scenario = document.getElementById('dd-scenario')?.value;
    let dataFormat = document.getElementById('dd-format')?.value || 'CSV';

    if (!module || !scenario) {
        addDDConsoleLog('Please select a module and scenario', 'warning');
        return;
    }
    const syncSuccess = syncDOMDataBeforeExecution();
    if (!syncSuccess) {
        addDDConsoleLog('Failed to extract data from table', 'error');
        return;
    }

    const dataContent = document.getElementById('dd-data')?.value?.trim();
    const datasetArray = window.ddtCurrentData || [];
    if (!dataContent) {
        addDDConsoleLog('Please load test data first', 'warning');
        return;
    }

    // Parallel endpoint consumes DataContent; keep format aligned with synced table content
    dataFormat = 'CSV';
    const formatSelector = document.getElementById('dd-format');
    if (formatSelector) {
        formatSelector.value = 'CSV';
    }

    const btn = document.querySelector('button[onclick="executeDataDrivenParallel()"]');
    btn.disabled = true;
    btn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Executing (Parallel)...';
    ddtUIState.isExecuting = true;
    
    clearExecutionLogs();
    const executionStartTime = Date.now();
    ddtUIState.lastExecutionTime = new Date();

    try {
        const rowCount = datasetArray.length;
        addDDConsoleLog(`[ParallelExecution] Starting "${scenario}" with ${dataFormat} data (${rowCount} rows) - Concurrency: 3...`, 'info');
        addExecutionLog(`Parallel Execution Mode`, 'info');
        addExecutionLog(`Scenario: ${scenario}`, 'info');
        addExecutionLog(`Records: ${rowCount}`, 'info');
        addExecutionLog(`Concurrency: 3`, 'info');

        const r = await fetch(`${API_BASE_URL}/datadriven/execute-parallel`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ 
                scenarioName: scenario, 
                module, 
                dataFormat, 
                dataContent,
                concurrency: 3,
                strategy: document.getElementById('dd-strategy').value 
            })
        });
        
        if (!r.ok) {
            const err = await r.json().catch(() => ({ error: `HTTP ${r.status}` }));
            throw new Error(err.error || `Parallel execution failed with status ${r.status}`);
        }
        
        const d = await r.json();

        if (d.success) {
            const executionDuration = Date.now() - executionStartTime;
            const avgTimePerRow = rowCount > 0 ? (executionDuration / rowCount).toFixed(2) : 0;
            
            addDDConsoleLog(`✓ Parallel execution completed. Passed: ${d.passed}/${d.totalRows}, Failed: ${d.failed}`, d.failed > 0 ? 'warning' : 'success');
            addExecutionLog(`Status: ${d.failed === 0 ? 'PASSED' : 'FAILED'}`, d.failed === 0 ? 'success' : 'warning');
            addExecutionLog(`Total Duration: ${(executionDuration / 1000).toFixed(2)}s`, 'info');
            addExecutionLog(`Avg Time/Row: ${avgTimePerRow}ms`, 'info');
            addExecutionLog(`Passed: ${d.passed} | Failed: ${d.failed}`, 'info');

            refreshAnalyticsDashboard();
            renderDataDrivenResults({
                ...d,
                executionLogs: ddtUIState.executionLogs,
                executionDuration: executionDuration,
                executionTime: ddtUIState.lastExecutionTime
            });
        } else {
            addDDConsoleLog('[ParallelExecution] Failed: ' + d.error, 'error');
            addExecutionLog('Execution Failed: ' + d.error, 'error');
        }
    } catch (e) {
        addDDConsoleLog('[ParallelExecution] Error: ' + e.message, 'error');
        addExecutionLog('Execution Error: ' + e.message, 'error');
    } finally {
        btn.disabled = false;
        btn.innerHTML = '<i class="fas fa-bolt"></i> Parallel Execution';
        ddtUIState.isExecuting = false;
    }
}

/**
 * Render results
 */
function renderDataDrivenResults(data) {
    const area = document.getElementById('dd-results-area');
    const summary = document.getElementById('dd-summary-bar');
    const table = document.getElementById('dd-results-table');

    // Null check for elements - wait briefly if elements not ready
    if (!area || !summary || !table) {
        addDDConsoleLog('Error: Results container elements not found', 'error');
        setTimeout(() => {
            const retryArea = document.getElementById('dd-results-area');
            const retrySummary = document.getElementById('dd-summary-bar');
            const retryTable = document.getElementById('dd-results-table');
            if (retryArea && retrySummary && retryTable) {
                renderDataDrivenResults(data);
            }
        }, 500);
        return;
    }

    area.classList.remove('hidden');

    const allPassed = data.failed === 0;
    const executionTime = data.executionTime || new Date();
    const executionDuration = data.executionDuration || 0;
    const formattedTime = typeof executionTime === 'string' ? executionTime : executionTime.toLocaleString();
    const durationSeconds = (executionDuration / 1000).toFixed(2);

    // Professional Summary Header
    summary.style.background = allPassed ? 'rgba(16,185,129,0.12)' : 'rgba(239,68,68,0.1)';
    summary.style.borderLeft = `4px solid ${allPassed ? 'var(--success-color)' : 'var(--danger-color)'}`;
    
    summary.innerHTML = `
        <div style="display:flex;justify-content:space-between;align-items:start;gap:20px;">
            <div>
                <div style="font-size:1.15em;font-weight:600;margin-block-end:8px;">
                    ${allPassed ? '✅' : '⚠️'} Test Execution Summary
                </div>
                <div style="color:#666;font-size:0.9em;margin-block-end:12px;">
                    Scenario: <strong>${escapeHtml(data.scenarioName)}</strong> • Executed: <strong>${formattedTime}</strong>
                </div>
                <div style="display:flex;gap:12px;flex-wrap:wrap;">
                    <span class="badge badge-success">✅ ${data.passed || 0} Passed</span>
                    <span class="badge badge-danger" style="margin-inline-start:0;">❌ ${data.failed || 0} Failed</span>
                    <span class="badge badge-info" style="margin-inline-start:0;">📊 ${data.totalRows || 0} Total</span>
                    <span class="badge" style="background:#e0e7ff;color:#4f46e5;border:none;margin-inline-start:0;">⏱️ ${durationSeconds}s</span>
                </div>
            </div>
            <div style="text-align:end;font-size:1.2em;font-weight:600;color:${allPassed ? 'var(--success-color)' : 'var(--danger-color)'};padding:10px 0;">
                ${allPassed ? 'ALL PASSED' : 'SOME FAILED'}
            </div>
        </div>
        
        <div style="margin-block-start:14px;border-block-start:1px solid rgba(0,0,0,0.1);padding-block-start:14px;">
            <button class="btn btn-sm" style="background:#f3f4f6;color:#374151;border:1px solid #d1d5db;cursor:pointer;padding:6px 12px;border-radius:4px;" onclick="toggleExecutionLogs()">
                <i class="fas fa-chevron-down" id="logs-toggle-icon"></i> Execution Logs & History
            </button>
            <div id="execution-logs-section" style="display:none;margin-block-start:12px;background:rgba(0,0,0,0.03);padding:12px;border-radius:6px;max-block-size:300px;overflow-y:auto;font-family:monospace;font-size:0.85em;line-height:1.5;">
                ${(data.executionLogs || []).map(log => `
                    <div style="margin-block-end:6px;padding:4px 6px;border-radius:3px;background:${log.type === 'error' ? '#fee2e2' : log.type === 'success' ? '#ecfdf5' : log.type === 'warning' ? '#fef3c7' : '#f3f4f6'};color:${log.type === 'error' ? '#991b1b' : log.type === 'success' ? '#065f46' : log.type === 'warning' ? '#92400e' : '#374151'};">
                        <span style="font-weight:500;color:#999;">[${log.timestamp}]</span> ${escapeHtml(log.message)}
                    </div>
                `).join('')}
            </div>
        </div>
    `;

    // Results Table with proper null checking
    const cols = (data.results && data.results.length > 0 ? Object.keys(data.results[0].dataRow || {}) : []).filter(c => c);
    const resultRows = data.results || [];
    
    if (resultRows.length === 0) {
        table.innerHTML = '<div style="padding:20px;text-align:center;color:#999;">No test results to display</div>';
        return;
    }
    
    // Build detailed results table with error details for failed tests
    let resultsHtml = `
        <div style="margin-block-start:20px;">
            <div style="font-weight:600;margin-block-end:12px;font-size:0.95em;color:#374151;">📋 Detailed Test Results</div>
            <div style="overflow-x:auto;">
                <table style="inline-size:100%;border-collapse:collapse;font-size:0.85em;">
                    <thead style="background:#f3f4f6;position:sticky;inset-block-start:0;">
                        <tr>
                            <th style="padding:10px;border:1px solid #e5e7eb;text-align:start;font-weight:600;">#</th>
                            <th style="padding:10px;border:1px solid #e5e7eb;text-align:start;font-weight:600;">Test Case</th>
                            <th style="padding:10px;border:1px solid #e5e7eb;text-align:center;font-weight:600;">Status</th>
                            <th style="padding:10px;border:1px solid #e5e7eb;text-align:start;font-weight:600;">Duration</th>
                            <th style="padding:10px;border:1px solid #e5e7eb;text-align:start;font-weight:600;">Date/Time</th>
                            ${cols.map(c => `<th style="padding:10px;border:1px solid #e5e7eb;text-align:start;font-weight:600; white-space:nowrap;">${escapeHtml(String(c))}</th>`).join('')}
                        </tr>
                    </thead>
                    <tbody>
                        ${resultRows.map((r, idx) => {
                            const passed = r.status === 'Passed';
                            const statusColor = passed ? '#ecfdf5' : '#fee2e2';
                            const textColor = passed ? '#065f46' : '#991b1b';
                            const testCaseTitle = `${data.scenarioName} Row ${r.rowNumber || idx + 1}`;
                            const errorMsg = r.errorMessage ? ` - ${r.errorMessage}` : '';
                            return `<tr style="background:${idx % 2 === 0 ? '#fff' : '#f9fafb'};border-block-end:1px solid #e5e7eb;">
                                <td style="padding:10px;border:1px solid #e5e7eb;color:#666;font-weight:500;">${r.rowNumber || idx + 1}</td>
                                <td style="padding:10px;border:1px solid #e5e7eb;color:#374151;font-size:0.9em;">${escapeHtml(testCaseTitle)}</td>
                                <td style="padding:10px;border:1px solid #e5e7eb;text-align:center;">
                                    <span style="padding:4px 8px;border-radius:4px;background:${statusColor};color:${textColor};font-weight:500;font-size:0.75em;">${passed ? '✅ PASSED' : '❌ FAILED'}</span>
                                </td>
                                <td style="padding:10px;border:1px solid #e5e7eb;color:#666;">${escapeHtml(String(r.duration || 'N/A'))}</td>
                                <td style="padding:10px;border:1px solid #e5e7eb;color:#666;font-size:0.9em;">${formattedTime}</td>
                                ${cols.map(c => `<td style="padding:10px;border:1px solid #e5e7eb;color:#374151;font-size:0.9em;">${escapeHtml(String(r.dataRow && r.dataRow[c] ? r.dataRow[c] : ''))}</td>`).join('')}
                            </tr>`;
                        }).join('')}
                    </tbody>
                </table>
            </div>
        </div>
    `;
    
    // Add error details if any test failed
    const failedTests = resultRows.filter(r => r.status !== 'Passed');
    if (failedTests.length > 0) {
        // Helper function to truncate error message to first line or 100 chars
        const truncateError = (msg) => {
            if (!msg) return '';
            const firstLine = msg.split('\n')[0];
            return firstLine.length > 100 ? firstLine.substring(0, 100) + '...' : firstLine;
        };
        
        resultsHtml += `
            <div style="margin-block-start:20px;border-block-start:2px solid #fee2e2;padding-block-start:16px;">
                <div style="display:flex;justify-content:space-between;align-items:center;margin-block-end:16px;">
                    <div style="font-weight:600;font-size:0.95em;color:#991b1b;">⚠️ Error Details & Diagnostics (${failedTests.length} failed)</div>
                    <div style="display:flex;gap:8px;">
                        <button onclick="expandAllErrors()" style="padding:6px 12px;font-size:0.85em;background:#fee2e2;color:#991b1b;border:1px solid #fecaca;border-radius:4px;cursor:pointer;font-weight:500;transition:all 0.2s;">📂 Expand All</button>
                        <button onclick="collapseAllErrors()" style="padding:6px 12px;font-size:0.85em;background:#f3f4f6;color:#374151;border:1px solid #d1d5db;border-radius:4px;cursor:pointer;font-weight:500;transition:all 0.2s;">📁 Collapse All</button>
                    </div>
                </div>
                ${failedTests.map((r, idx) => {
                    const errorId = 'error-card-' + idx;
                    const stepsId = 'steps-details-' + idx;
                    const errorTextId = 'error-text-' + idx;
                    const firstLineError = truncateError(r.errorMessage);
                    const hasFullError = r.errorMessage && r.errorMessage.length > 100;
                    const hasSteps = r.steps && r.steps.length > 0;
                    
                    return `
                    <div id="${errorId}" style="background:#fee2e2;border:1px solid #fecaca;border-radius:6px;margin-block-end:12px;overflow:hidden;transition:all 0.2s;">
                        <!-- Error Card Header - Clickable -->
                        <div onclick="toggleErrorCard('${errorId}')" style="padding:14px;cursor:pointer;display:flex;justify-content:space-between;align-items:center;background:#fee2e2;transition:background 0.2s;" onmouseover="this.style.background='#fecaca'" onmouseout="this.style.background='#fee2e2'">
                            <div style="display:flex;align-items:center;gap:10px;flex:1;">
                                <span id="${errorId}-icon" style="font-size:1.1em;transition:transform 0.2s;">▶</span>
                                <div style="flex:1;">
                                    <div style="font-weight:600;color:#991b1b;font-size:0.95em;">Row ${r.rowNumber || idx + 1} - ${escapeHtml(r.testCaseName || 'Unknown')}</div>
                                    <div style="font-size:0.8em;color:#7f1d1d;margin-block-start:4px;">Status: <strong>Failed</strong> | Dataset: <strong>Row ${r.rowNumber || idx + 1}</strong></div>
                                </div>
                            </div>
                        </div>
                        
                        <!-- Error Details Container (Initially Hidden) -->
                        <div id="${errorId}-content" style="display:none;padding:14px;border-block-start:1px solid #fecaca;">
                            <!-- Error Summary Section -->
                            ${r.errorMessage ? `
                            <div style="margin-block-end:12px;">
                                <div style="font-weight:600;font-size:0.9em;color:#991b1b;margin-block-end:6px;">📋 Error Summary:</div>
                                <div id="${errorTextId}-truncated" style="color:#7f1d1d;font-family:monospace;font-size:0.85em;background:#fff5f5;padding:10px;border-radius:4px;border-inline-start:3px solid #dc2626;line-height:1.4;">
                                    ${escapeHtml(firstLineError)}
                                </div>
                                ${hasFullError ? `
                                <div id="${errorTextId}-full" style="display:none;color:#7f1d1d;font-family:monospace;font-size:0.85em;background:#fff5f5;padding:10px;border-radius:4px;border-inline-start:3px solid #dc2626;line-height:1.4;margin-block-start:8px;white-space:pre-wrap;word-break:break-word;">
                                    ${escapeHtml(r.errorMessage)}
                                </div>
                                <div style="margin-block-start:8px;">
                                    <a href="javascript:toggleErrorText('${errorTextId}')" style="color:#0066cc;text-decoration:none;font-size:0.85em;font-weight:500;cursor:pointer;">📄 View Full Error</a>
                                </div>
                                ` : ''}
                            </div>
                            ` : ''}
                            
                            <!-- Step Details Section -->
                            ${hasSteps ? `
                            <div style="margin-block-start:12px;border-block-start:1px solid #fecaca;padding-block-start:12px;">
                                <div onclick="toggleStepDetails('${stepsId}')" style="display:flex;align-items:center;gap:8px;cursor:pointer;padding:8px;border-radius:4px;background:#fff5f5;margin-block-end:10px;transition:background 0.2s;" onmouseover="this.style.background='#fee2e2'" onmouseout="this.style.background='#fff5f5'">
                                    <span id="${stepsId}-icon" style="font-size:1em;">▶</span>
                                    <span style="font-weight:600;font-size:0.9em;color:#991b1b;">📍 Step Details (${r.steps.length} steps)</span>
                                </div>
                                
                                <div id="${stepsId}" style="display:none;padding-inline-start:8px;border-inline-start:3px solid #fecaca;">
                                    ${r.steps.map((s, stepIdx) => `
                                        <div style="margin-block-end:10px;padding:10px;background:${s.status === 'Passed' ? '#f0fdf4' : '#fef2f2'};border-inline-start:3px solid ${s.status === 'Passed' ? '#16a34a' : '#dc2626'};border-radius:3px;">
                                            <div style="display:flex;justify-content:space-between;align-items:start;">
                                                <div style="flex:1;">
                                                    <div style="font-weight:500;font-size:0.87em;color:#374151;">Step ${stepIdx + 1}: ${escapeHtml(s.stepName)}</div>
                                                    <div style="font-size:0.8em;color:#666;margin-block-start:2px;">${escapeHtml(s.description || 'No description')}</div>
                                                    <div style="font-size:0.8em;margin-block-start:4px;">
                                                        <span style="padding:2px 6px;border-radius:3px;background:${s.status === 'Passed' ? '#dcfce7' : '#fee2e2'};color:${s.status === 'Passed' ? '#166534' : '#991b1b'};">
                                                            ${s.status === 'Passed' ? '✅ Passed' : '❌ Failed'}
                                                        </span>
                                                    </div>
                                                </div>
                                            </div>
                                            ${s.errorMessage ? `<div style="color:#991b1b;font-size:0.8em;margin-block-start:6px;padding:6px;background:#fff5f5;border-radius:3px;font-family:monospace;"><strong>Error:</strong> ${escapeHtml(s.errorMessage)}</div>` : ''}
                                            ${s.screenshotPath ? `<div style="margin-block-start:6px;"><a href="${escapeHtml(s.screenshotPath)}" target="_blank" style="color:#0066cc;text-decoration:none;font-size:0.8em;font-weight:500;">📸 View Screenshot</a></div>` : ''}
                                        </div>
                                    `).join('')}
                                </div>
                            </div>
                            ` : ''}
                        </div>
                    </div>
                    `;
                }).join('')}
            </div>
        `;
    }
    
    table.innerHTML = resultsHtml;
}

/**
 * Toggle a specific error card (expand/collapse)
 */
function toggleErrorCard(cardId) {
    const card = document.getElementById(cardId);
    const content = document.getElementById(cardId + '-content');
    const icon = document.getElementById(cardId + '-icon');
    
    if (content && icon) {
        const isVisible = content.style.display !== 'none';
        content.style.display = isVisible ? 'none' : 'block';
        icon.style.transform = isVisible ? '' : 'rotate(90deg)';
        icon.style.transition = 'transform 0.2s';
    }
}

/**
 * Toggle step details within an error card
 */
function toggleStepDetails(stepsId) {
    const steps = document.getElementById(stepsId);
    const icon = document.getElementById(stepsId + '-icon');
    
    if (steps && icon) {
        const isVisible = steps.style.display !== 'none';
        steps.style.display = isVisible ? 'none' : 'block';
        icon.style.transform = isVisible ? '' : 'rotate(90deg)';
        icon.style.transition = 'transform 0.2s';
    }
}

/**
 * Toggle between truncated and full error text
 */
function toggleErrorText(errorTextId) {
    const truncated = document.getElementById(errorTextId + '-truncated');
    const full = document.getElementById(errorTextId + '-full');
    const link = event.target;
    
    if (truncated && full) {
        const isShowingTruncated = truncated.style.display !== 'none';
        truncated.style.display = isShowingTruncated ? 'none' : 'block';
        full.style.display = isShowingTruncated ? 'block' : 'none';
        link.textContent = isShowingTruncated ? '📄 Hide Full Error' : '📄 View Full Error';
    }
}

/**
 * Expand all error cards
 */
function expandAllErrors() {
    // Find all error cards
    const errorCards = document.querySelectorAll('[id^="error-card-"]');
    
    errorCards.forEach(card => {
        const cardId = card.id;
        const content = document.getElementById(cardId + '-content');
        const icon = document.getElementById(cardId + '-icon');
        
        if (content && icon) {
            content.style.display = 'block';
            icon.style.transform = 'rotate(90deg)';
            icon.style.transition = 'transform 0.2s';
        }
    });
}

/**
 * Collapse all error cards
 */
function collapseAllErrors() {
    // Find all error cards
    const errorCards = document.querySelectorAll('[id^="error-card-"]');
    
    errorCards.forEach(card => {
        const cardId = card.id;
        const content = document.getElementById(cardId + '-content');
        const icon = document.getElementById(cardId + '-icon');
        
        if (content && icon) {
            content.style.display = 'none';
            icon.style.transform = '';
            icon.style.transition = 'transform 0.2s';
        }
    });
}

/**
 * Toggle execution logs visibility
 */
function toggleExecutionLogs() {
    const logsSection = document.getElementById('execution-logs-section');
    const toggleIcon = document.getElementById('logs-toggle-icon');
    
    if (logsSection) {
        logsSection.style.display = logsSection.style.display === 'none' ? 'block' : 'none';
        if (toggleIcon) {
            toggleIcon.style.transform = logsSection.style.display === 'none' ? '' : 'rotate(180deg)';
            toggleIcon.style.transition = 'transform 0.2s';
        }
    }
}

/**
 * Refresh analytics dashboard
 */
function refreshAnalyticsDashboard() {
    try {
        const obs = dataEngine.getDatasetObservability();
        const flaky = dataEngine.detectFlakyDatasets();

        // Update summary cards with null checks
        const totalElem = document.getElementById('analytics-total');
        const successElem = document.getElementById('analytics-success');
        const failingElem = document.getElementById('analytics-failing');
        const flakyElem = document.getElementById('analytics-flaky');

        if (totalElem) totalElem.textContent = obs.summary.totalExecutions;
        if (successElem) successElem.textContent = obs.summary.successRate;
        if (failingElem) failingElem.textContent = obs.topFailing.length;
        if (flakyElem) flakyElem.textContent = flaky.length;

        // Top failing datasets
        const failingHtml = obs.topFailing.length > 0 ?
        obs.topFailing.map(d => `
            <div style="padding:10px;border-block-end:1px solid var(--border);display:flex;justify-content:space-between;align-items:center;">
                <div>
                    <div style="font-weight:500;font-size:0.9em;margin-block-end:2px;">Dataset ${d.datasetId.slice(0, 8)}</div>
                    <div style="font-size:0.85em;color:#666;">${d.totalFailures} / ${d.totalExecutions} failed</div>
                </div>
                <span class="badge badge-danger">${d.failureRate}</span>
            </div>
        `).join('') :
        '<div style="color:#999;text-align:center;padding:20px;">No failures</div>';

        if (failingElemDiv) failingElemDiv.innerHTML = failingHtml;

        // Top used datasets
        const usedHtml = obs.topUsed.length > 0 ?
            obs.topUsed.slice(0, 5).map(d => `
            <div style="padding:10px;border-block-end:1px solid var(--border);display:flex;justify-content:space-between;align-items:center;">
                <div>
                    <div style="font-weight:500;font-size:0.9em;margin-block-end:2px;">Dataset ${d.datasetId.slice(0, 8)}</div>
                    <div style="font-size:0.85em;color:#666;">${d.executions} executions</div>
                </div>
                <span class="badge badge-info">${d.passRate}</span>
            </div>
        `).join('') :
        '<div style="color:#999;text-align:center;padding:20px;">No data yet</div>';

        const usedElem = document.getElementById('top-used-datasets');
        if (usedElem) usedElem.innerHTML = usedHtml;
    } catch (e) {
        console.error('Error refreshing analytics dashboard:', e);
    }
}

/**
 * Helper functions
 */
function addDDConsoleLog(message, level = 'info') {
    const consoleDiv = document.getElementById('dd-console-output');
    if (!consoleDiv) return;

    const line = document.createElement('div');
    line.className = 'console-line';
    if (level === 'error') line.style.color = 'var(--danger-color)';
    if (level === 'success') line.style.color = 'var(--success-color)';
    if (level === 'warning') line.style.color = 'var(--warning-color)';
    if (level === 'info') line.style.color = 'var(--info-color)';
    
    const timestamp = new Date().toLocaleTimeString();
    line.textContent = `[${timestamp}] ${message}`;
    consoleDiv.appendChild(line);
    consoleDiv.scrollTop = consoleDiv.scrollHeight;
}

function clearDDConsole() {
    const consoleDiv = document.getElementById('dd-console-output');
    if (consoleDiv) {
        consoleDiv.innerHTML = '<div class="console-line">Console cleared at ' + new Date().toLocaleTimeString() + '</div>';
    }
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text || '';
    return div.innerHTML;
}

function refreshDataSourcesList(sources) {
    const container = document.getElementById('data-sources-list');
    if (!container) return;

    if (sources.length === 0) {
        container.innerHTML = '<div style="grid-column:1/-1;text-align:center;color:#999;padding:40px;">No data sources registered. Click buttons below to add one.</div>';
        return;
    }

    container.innerHTML = sources.map(source => `
        <div style="padding:14px;border:1px solid var(--border);border-radius:8px;">
            <div style="font-weight:600;margin-block-end:6px;">${source.name}</div>
            <div style="font-size:0.85em;color:#666;margin-block-end:8px;">${source.type.toUpperCase()} | ${source.recordCount} records</div>
            <button class="btn btn-sm" onclick="selectDataSourceForUse('${source.id}');" style="background:#e0f2fe;color:#0369a1;font-size:0.8em;padding:4px 8px;">Use This Source</button>
        </div>
    `).join('');
}

function selectDataSourceForUse(sourceId) {
    document.getElementById('dd-data-source').value = sourceId;
    showDDTTab('execution');
    addDDConsoleLog(`Selected data source: ${sourceId}`, 'info');
}

function showAddDataSourceModal() {
    addDDConsoleLog('Opening Add JSON Source dialog...', 'info');
}

function showUploadCSVModal() {
    addDDConsoleLog('Opening CSV Upload dialog...', 'info');
}

function showAPISourceModal() {
    addDDConsoleLog('Opening API Source dialog...', 'info');
}

/**
 * Toggle error card visibility
 * Show/hide detailed error information for a specific failed test row
 */
function toggleErrorCard(cardId) {
    const card = document.getElementById(cardId);
    const content = document.getElementById(cardId + '-content');
    const icon = document.getElementById(cardId + '-icon');
    
    if (!card || !content || !icon) return;
    
    const isHidden = content.style.display === 'none';
    
    if (isHidden) {
        // Expand
        content.style.display = 'block';
        icon.textContent = '▼';
        card.style.maxHeight = 'none';
    } else {
        // Collapse
        content.style.display = 'none';
        icon.textContent = '▶';
        card.style.maxHeight = '60px';
    }
}

/**
 * Toggle step details visibility
 * Show/hide the list of test steps within an error card
 */
function toggleStepDetails(stepsId) {
    const container = document.getElementById(stepsId);
    const icon = document.getElementById(stepsId + '-icon');
    
    if (!container || !icon) return;
    
    const isHidden = container.style.display === 'none';
    
    if (isHidden) {
        // Expand
        container.style.display = 'block';
        icon.textContent = '▼';
    } else {
        // Collapse
        container.style.display = 'none';
        icon.textContent = '▶';
    }
}

/**
 * Toggle full error text visibility
 * Show/hide complete error message (for messages truncated to first line)
 */
function toggleErrorText(textId) {
    const truncated = document.getElementById(textId + '-truncated');
    const full = document.getElementById(textId + '-full');
    
    if (!truncated || !full) return;
    
    const isHidden = full.style.display === 'none';
    
    if (isHidden) {
        // Show full
        truncated.style.display = 'none';
        full.style.display = 'block';
    } else {
        // Show truncated
        truncated.style.display = 'block';
        full.style.display = 'none';
    }
}

/**
 * Expand all error cards at once
 * Opens all failed test error details for bulk review
 */
function expandAllErrors() {
    const errorCards = document.querySelectorAll('[id^="error-card-"]');
    
    errorCards.forEach(card => {
        const cardId = card.id;
        const content = document.getElementById(cardId + '-content');
        const icon = document.getElementById(cardId + '-icon');
        
        if (content && icon) {
            content.style.display = 'block';
            icon.textContent = '▼';
            card.style.maxHeight = 'none';
        }
    });
    
    console.log(`[DDT] Expanded ${errorCards.length} error cards`);
}

/**
 * Collapse all error cards at once
 * Closes all error details to view summary only
 */
function collapseAllErrors() {
    const errorCards = document.querySelectorAll('[id^="error-card-"]');
    
    errorCards.forEach(card => {
        const cardId = card.id;
        const content = document.getElementById(cardId + '-content');
        const icon = document.getElementById(cardId + '-icon');
        
        if (content && icon) {
            content.style.display = 'none';
            icon.textContent = '▶';
            card.style.maxHeight = '60px';
        }
    });
    
    console.log(`[DDT] Collapsed ${errorCards.length} error cards`);
}

/**
 * Toggle execution logs visibility
 * Show/hide the execution logs section in results summary
 */
function toggleExecutionLogs() {
    const logsSection = document.getElementById('execution-logs-section');
    const toggleIcon = document.getElementById('logs-toggle-icon');
    
    if (!logsSection || !toggleIcon) return;
    
    const isHidden = logsSection.style.display === 'none';
    
    if (isHidden) {
        // Show
        logsSection.style.display = 'block';
        toggleIcon.className = 'fas fa-chevron-up';
    } else {
        // Hide
        logsSection.style.display = 'none';
        toggleIcon.className = 'fas fa-chevron-down';
    }
}

console.log('[DDT] Advanced Data-Driven Testing Module initialized with 20-feature engine');
