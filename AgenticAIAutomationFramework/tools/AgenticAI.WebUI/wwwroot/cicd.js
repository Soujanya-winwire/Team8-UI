// ============================================================================
// CI/CD DASHBOARD
// ============================================================================

async function loadCICDView() {
    const view = document.getElementById('cicd-view');
    if (!view) return;

    view.innerHTML = `
        <div class="header">
            <h2><i class="fas fa-rocket"></i> CI/CD Dashboard</h2>
            <div class="header-actions">
                <button class="btn btn-secondary" onclick="loadCICDView()">
                    <i class="fas fa-sync"></i> Refresh
                </button>
                <button class="btn btn-primary" onclick="triggerPipeline()">
                    <i class="fas fa-play"></i> Trigger Build
                </button>
            </div>
        </div>

        <!-- Statistics Cards -->
        <div class="stats-grid" style="grid-template-columns: repeat(4, 1fr);">
            <div class="stat-card" style="background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white;">
                <div class="stat-icon"><i class="fas fa-check-circle"></i></div>
                <div class="stat-details">
                    <h3 id="ci-success-rate">--%</h3>
                    <p>Success Rate</p>
                </div>
            </div>
            <div class="stat-card" style="background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%); color: white;">
                <div class="stat-icon"><i class="fas fa-clock"></i></div>
                <div class="stat-details">
                    <h3 id="ci-avg-duration">--</h3>
                    <p>Avg Duration</p>
                </div>
            </div>
            <div class="stat-card" style="background: linear-gradient(135deg, #4facfe 0%, #00f2fe 100%); color: white;">
                <div class="stat-icon"><i class="fas fa-vial"></i></div>
                <div class="stat-details">
                    <h3 id="ci-tests-passed">--</h3>
                    <p>Tests Passed</p>
                </div>
            </div>
            <div class="stat-card" style="background: linear-gradient(135deg, #43e97b 0%, #38f9d7 100%); color: white;">
                <div class="stat-icon"><i class="fas fa-chart-line"></i></div>
                <div class="stat-details">
                    <h3 id="ci-coverage">--%</h3>
                    <p>Code Coverage</p>
                </div>
            </div>
        </div>

        <!-- Pipeline Runs -->
        <div class="card mt-20">
            <div class="card-header">
                <div class="card-title"><i class="fas fa-history"></i> Recent Pipeline Runs</div>
            </div>
            <div id="ci-pipelines-list">
                <div class="spinner"></div>
            </div>
        </div>

        <!-- Build Trends -->
        <div class="card mt-20">
            <div class="card-header">
                <div class="card-title"><i class="fas fa-chart-area"></i> Build Trends (Last 7 Days)</div>
            </div>
            <div style="padding: 20px;">
                <canvas id="ci-build-trends-chart" height="80"></canvas>
            </div>
        </div>
    `;

    await loadCIPipelineStats();
    await loadCIPipelines();
    renderCIBuildTrendsChart();
}

async function loadCIPipelineStats() {
    try {
        const response = await fetch(`${API_BASE_URL}/cicd/stats`);
        const data = await response.json();

        if (data.success) {
            document.getElementById('ci-success-rate').textContent = `${data.stats.successRate}%`;
            document.getElementById('ci-avg-duration').textContent = data.stats.averageDuration;
            document.getElementById('ci-tests-passed').textContent = `${data.stats.testsPassed}/${data.stats.testsRun}`;
            document.getElementById('ci-coverage').textContent = `${data.stats.coverage}%`;
        }
    } catch (error) {
        console.error('Error loading CI stats:', error);
    }
}

async function loadCIPipelines() {
    try {
        const response = await fetch(`${API_BASE_URL}/cicd/pipelines`);
        const data = await response.json();

        const container = document.getElementById('ci-pipelines-list');
        
        if (!data.success || !data.pipelines || data.pipelines.length === 0) {
            container.innerHTML = `
                <div class="empty-state">
                    <i class="fas fa-inbox"></i>
                    <h3>No pipeline runs found</h3>
                    <p>${data.message || 'Trigger a build to see pipeline history'}</p>
                    ${data.message && data.message.includes('mock') ? '<p style="color: #f59e0b; margin-top: 10px;"><i class="fas fa-info-circle"></i> Configure GitHub token in appsettings.json for live data</p>' : ''}
                </div>
            `;
            return;
        }

        const html = `
            ${data.message ? `<div class="alert alert-info" style="margin: 15px;"><i class="fas fa-info-circle"></i> ${data.message}</div>` : ''}
            <table>
                <thead>
                    <tr>
                        <th style="width: 80px;">#</th>
                        <th style="width: 120px;">Status</th>
                        <th>Pipeline</th>
                        <th>Branch</th>
                        <th style="max-width: 400px;">Commit</th>
                        <th>Author</th>
                        <th>Started</th>
                        <th style="width: 100px;">Actions</th>
                    </tr>
                </thead>
                <tbody>
                    ${data.pipelines.map(pipeline => {
                        const statusIcon = getCIStatusIcon(pipeline.conclusion || pipeline.status);
                        const statusClass = getCIStatusClass(pipeline.conclusion || pipeline.status);
                        const timeAgo = getCITimeAgo(pipeline.created_at);

                        return `
                            <tr>
                                <td><strong>#${pipeline.run_number || '-'}</strong></td>
                                <td>
                                    <span class="badge badge-${statusClass}">
                                        ${statusIcon} ${(pipeline.conclusion || pipeline.status || 'unknown').toUpperCase()}
                                    </span>
                                </td>
                                <td><strong>${escapeHtml(pipeline.name)}</strong></td>
                                <td><code style="font-size: 11px;">${escapeHtml(pipeline.head_branch)}</code></td>
                                <td style="max-width: 400px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;" title="${escapeHtml(pipeline.head_commit.message)}">
                                    ${escapeHtml(pipeline.head_commit.message)}
                                </td>
                                <td>${escapeHtml(pipeline.head_commit.author)}</td>
                                <td style="font-size: 11px;">${timeAgo}</td>
                                <td>
                                    <a href="${pipeline.html_url}" target="_blank" class="btn btn-secondary btn-icon" title="View on GitHub">
                                        <i class="fas fa-external-link-alt"></i>
                                    </a>
                                </td>
                            </tr>
                        `;
                    }).join('')}
                </tbody>
            </table>
        `;

        container.innerHTML = html;
    } catch (error) {
        console.error('Error loading pipelines:', error);
        document.getElementById('ci-pipelines-list').innerHTML = `
            <div class="alert alert-danger" style="margin: 15px;">
                <i class="fas fa-exclamation-triangle"></i> Failed to load pipelines: ${error.message}
            </div>
        `;
    }
}

function getCIStatusIcon(status) {
    const statusLower = (status || '').toLowerCase();
    const icons = {
        'success': '?',
        'failure': '?',
        'cancelled': '?',
        'in_progress': '?',
        'queued': '?',
        'completed': '?'
    };
    return icons[statusLower] || '?';
}

function getCIStatusClass(status) {
    const statusLower = (status || '').toLowerCase();
    const classes = {
        'success': 'success',
        'failure': 'danger',
        'cancelled': 'warning',
        'in_progress': 'info',
        'queued': 'secondary',
        'completed': 'success'
    };
    return classes[statusLower] || 'secondary';
}

function getCITimeAgo(dateString) {
    try {
        const date = new Date(dateString);
        const now = new Date();
        const seconds = Math.floor((now - date) / 1000);

        if (seconds < 60) return `${seconds}s ago`;
        if (seconds < 3600) return `${Math.floor(seconds / 60)}m ago`;
        if (seconds < 86400) return `${Math.floor(seconds / 3600)}h ago`;
        return `${Math.floor(seconds / 86400)}d ago`;
    } catch {
        return 'Unknown';
    }
}

async function triggerPipeline() {
    if (!confirm('?? Are you sure you want to trigger a new build?\n\nThis will start a new CI/CD pipeline run.')) return;

    try {
        showLoading('Triggering pipeline...');
        
        const response = await fetch(`${API_BASE_URL}/cicd/trigger`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                workflowId: 'ci-build-test.yml',
                branch: 'main'
            })
        });

        const data = await response.json();
        hideLoading();

        if (data.success) {
            showSuccess('? Pipeline triggered successfully! Refreshing in 3 seconds...');
            setTimeout(() => loadCIPipelines(), 3000);
        } else {
            showWarning(data.message || 'Failed to trigger pipeline. Check your GitHub token configuration.');
        }
    } catch (error) {
        hideLoading();
        showError('? Error triggering pipeline: ' + error.message);
    }
}

function renderCIBuildTrendsChart() {
    const ctx = document.getElementById('ci-build-trends-chart');
    if (!ctx) return;

    new Chart(ctx, {
        type: 'line',
        data: {
            labels: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'],
            datasets: [
                {
                    label: 'Successful Builds',
                    data: [12, 19, 15, 17, 14, 18, 16],
                    borderColor: '#10b981',
                    backgroundColor: 'rgba(16, 185, 129, 0.1)',
                    tension: 0.4,
                    fill: true
                },
                {
                    label: 'Failed Builds',
                    data: [1, 2, 1, 0, 2, 1, 1],
                    borderColor: '#ef4444',
                    backgroundColor: 'rgba(239, 68, 68, 0.1)',
                    tension: 0.4,
                    fill: true
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    display: true,
                    position: 'top',
                    labels: {
                        font: { size: 12 },
                        padding: 15,
                        usePointStyle: true
                    }
                },
                tooltip: {
                    mode: 'index',
                    intersect: false,
                    backgroundColor: 'rgba(30, 41, 59, 0.95)',
                    padding: 10,
                    cornerRadius: 6
                }
            },
            scales: {
                y: {
                    beginAtZero: true,
                    ticks: {
                        stepSize: 5,
                        font: { size: 11 }
                    },
                    grid: {
                        color: 'rgba(0, 0, 0, 0.05)'
                    }
                },
                x: {
                    ticks: {
                        font: { size: 11 }
                    },
                    grid: {
                        display: false
                    }
                }
            },
            interaction: {
                mode: 'nearest',
                axis: 'x',
                intersect: false
            }
        }
    });
}
