import sys

path = r'tools\AgenticAI.WebUI\wwwroot\app.js'
with open(path, 'r', encoding='utf-8') as f:
    content = f.read()

marker = '    // Load modules and tags'
if marker not in content:
    print('MARKER_NOT_FOUND')
    sys.exit(1)

data_driven_html = r"""
        <!-- DATA-DRIVEN EXECUTION (Selenium) -->
        <div class="card" style="border:2px solid var(--info-color);box-shadow:0 8px 24px rgba(59,130,246,0.15);">
            <div class="card-header" style="background:linear-gradient(135deg,rgba(59,130,246,0.08),rgba(37,99,235,0.04));border-radius:12px 12px 0 0;">
                <div class="card-title" style="display:flex;align-items:center;gap:10px;color:var(--info-color);">
                    <i class="fas fa-table"></i>
                    <span>Data-Driven Execution <span style="font-size:0.75em;color:#6b7280;font-weight:400;">(Selenium)</span></span>
                </div>
                <span class="badge badge-info"><i class="fas fa-flask"></i> CSV / JSON</span>
            </div>
            <div style="padding:4px 0 16px;">
                <p style="color:#6b7280;font-size:0.92em;line-height:1.7;margin-bottom:18px;">
                    Run a scenario once per data row. Use <code style="background:#f3f4f6;padding:2px 6px;border-radius:4px;">${ColumnName}</code>
                    placeholders in your scenario action values (e.g. <code style="background:#f3f4f6;padding:2px 6px;border-radius:4px;">${username}</code>).
                </p>
                <div class="grid-2">
                    <div class="form-group"><label>Module</label><select class="form-control" id="dd-module" onchange="loadDDScenarios()"><option value="">Select Module</option></select></div>
                    <div class="form-group"><label>Scenario</label><select class="form-control" id="dd-scenario"><option value="">Select Scenario</option></select></div>
                </div>
                <div class="grid-2">
                    <div class="form-group"><label>Data Format</label><select class="form-control" id="dd-format"><option value="CSV">CSV (comma-separated)</option><option value="JSON">JSON (array of objects)</option></select></div>
                    <div class="form-group" style="display:flex;align-items:flex-end;"><button class="btn btn-secondary" style="width:100%;" onclick="loadSampleData()"><i class="fas fa-magic"></i> Load Sample Data</button></div>
                </div>
                <div class="form-group"><label>Data (paste CSV or JSON)</label>
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
"""

# Try both LF and CRLF forms
for sep in ['\r\n', '\n']:
    old = f'    `;{sep}    {sep}    // Load modules and tags'
    if old in content:
        new = data_driven_html + f'    // Load modules and tags'
        content = content.replace(old, new, 1)
        with open(path, 'w', encoding='utf-8') as f:
            f.write(content)
        print(f'SUCCESS with sep={repr(sep)}')
        sys.exit(0)

print('OLD_SECTION_NOT_FOUND')
print(repr(content[content.index(marker)-50:content.index(marker)+5]))
