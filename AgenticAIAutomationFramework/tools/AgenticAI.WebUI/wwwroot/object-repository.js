// Object Repository Management Module
// Provides Ranorex Studio-like centralized test object management

let repositoryState = {
    objects: [],
    selectedObject: null,
    filterCategory: 'all',
    searchQuery: '',
    categories: []
};

/**
 * Initialize the Object Repository view
 */
async function initRepositoryView() {
    const viewContent = document.getElementById('repository-view');
    
    const html = `
        <div class="header">
            <h2><i class="fas fa-database"></i> Object Repository</h2>
            <div class="header-actions">
                <button class="btn btn-primary" onclick="refreshRepository()">
                    <i class="fas fa-sync"></i> Refresh
                </button>
                <button class="btn btn-success" onclick="showCreateObjectModal()">
                    <i class="fas fa-plus"></i> Add Object
                </button>
            </div>
        </div>

        <div style="display: grid; grid-template-columns: 280px 1fr; gap: 16px; margin-top: 16px;">
            <!-- Repository Browser (Left Panel) -->
            <div class="card" style="padding: 12px; max-height: 600px; overflow-y: auto;">
                <div style="font-weight: 600; margin-bottom: 12px; font-size: 0.9em;">Categories</div>
                <div id="category-list" style="display: flex; flex-direction: column; gap: 6px;">
                    <div class="repo-category active" onclick="filterByCategory('all')" style="cursor: pointer; padding: 8px; border-radius: 4px; background: #f3e8ff; color: #7c3aed; font-weight: 500;">
                        <i class="fas fa-cube"></i> All Objects
                    </div>
                </div>
                
                <div style="font-weight: 600; margin-top: 18px; margin-bottom: 12px; font-size: 0.9em;">Statistics</div>
                <div style="background: #f9fafb; border-radius: 4px; padding: 10px; font-size: 0.85em;">
                    <div style="margin-bottom: 8px;"><strong>Total Objects:</strong> <span id="stat-total">0</span></div>
                    <div style="margin-bottom: 8px;"><strong>Categories:</strong> <span id="stat-categories">0</span></div>
                    <div><strong>Most Used:</strong> <span id="stat-mostused">—</span></div>
                </div>
            </div>

            <!-- Object Details (Right Panel) -->
            <div>
                <!-- Search and Filter Bar -->
                <div class="card" style="padding: 12px; margin-bottom: 16px;">
                    <div style="display: grid; grid-template-columns: 1fr auto auto; gap: 10px;">
                        <input type="text" id="repo-search" placeholder="Search objects by name or locator..." 
                               style="padding: 8px 12px; border: 1px solid #e9d5ff; border-radius: 4px; font-size: 0.9em;"
                               onkeyup="searchRepositoryObjects()">
                        <select id="repo-locator-filter" 
                                style="padding: 8px 12px; border: 1px solid #e9d5ff; border-radius: 4px; font-size: 0.9em;"
                                onchange="filterByLocatorType()">
                            <option value="">All Locators</option>
                            <option value="CSS">CSS Selector</option>
                            <option value="XPath">XPath</option>
                            <option value="Robust">Robust Locator</option>
                        </select>
                        <button class="btn btn-secondary" onclick="clearRepositoryFilters()" style="padding: 8px 12px;">
                            <i class="fas fa-times"></i> Clear
                        </button>
                    </div>
                </div>

                <!-- Objects Table -->
                <div class="card" style="padding: 0; max-height: 600px; overflow-y: auto;">
                    <table style="width: 100%; border-collapse: collapse; font-size: 0.9em;">
                        <thead style="position: sticky; top: 0; background: #f9fafb; border-bottom: 1px solid #e9d5ff;">
                            <tr>
                                <th style="padding: 10px 12px; text-align: left; font-weight: 600;">Name</th>
                                <th style="padding: 10px 12px; text-align: left; font-weight: 600;">Category</th>
                                <th style="padding: 10px 12px; text-align: left; font-weight: 600;">Locator Type</th>
                                <th style="padding: 10px 12px; text-align: left; font-weight: 600;">Uses</th>
                                <th style="padding: 10px 12px; text-align: center; font-weight: 600;">Actions</th>
                            </tr>
                        </thead>
                        <tbody id="objects-tbody">
                            <tr style="text-align: center; padding: 30px;">
                                <td colspan="5" style="padding: 30px; color: #999;">Loading objects...</td>
                            </tr>
                        </tbody>
                    </table>
                </div>

                <!-- Object Details Panel -->
                <div id="object-details-panel" style="display: none; margin-top: 16px;">
                    <div class="card" style="padding: 16px;">
                        <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px;">
                            <h3 id="details-name" style="font-size: 1.1em; font-weight: 600;">Object Details</h3>
                            <button class="btn btn-secondary" onclick="closeDetailsPanel()" style="padding: 4px 8px; font-size: 0.85em;">
                                <i class="fas fa-times"></i> Close
                            </button>
                        </div>
                        <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 16px; margin-bottom: 12px;">
                            <div>
                                <label style="display: block; font-weight: 500; font-size: 0.85em; margin-bottom: 4px;">Name</label>
                                <div id="details-name-value" style="padding: 8px; background: #f9fafb; border-radius: 4px;"></div>
                            </div>
                            <div>
                                <label style="display: block; font-weight: 500; font-size: 0.85em; margin-bottom: 4px;">Category</label>
                                <div id="details-category-value" style="padding: 8px; background: #f9fafb; border-radius: 4px;"></div>
                            </div>
                            <div>
                                <label style="display: block; font-weight: 500; font-size: 0.85em; margin-bottom: 4px;">Element Type</label>
                                <div id="details-type-value" style="padding: 8px; background: #f9fafb; border-radius: 4px;"></div>
                            </div>
                            <div>
                                <label style="display: block; font-weight: 500; font-size: 0.85em; margin-bottom: 4px;">Usage Count</label>
                                <div id="details-usage-value" style="padding: 8px; background: #f9fafb; border-radius: 4px;"></div>
                            </div>
                        </div>
                        
                        <div style="margin-bottom: 12px;">
                            <label style="display: block; font-weight: 500; font-size: 0.85em; margin-bottom: 4px;">Primary Locator</label>
                            <div style="display: grid; grid-template-columns: 80px 1fr; gap: 8px;">
                                <div id="details-locator-type" style="padding: 8px; background: #ede9fe; border-radius: 4px; font-weight: 500; font-size: 0.85em;"></div>
                                <div id="details-locator-value" style="padding: 8px; background: #f9fafb; border-radius: 4px; font-family: monospace; word-break: break-all;"></div>
                            </div>
                        </div>

                        <div style="margin-bottom: 12px;">
                            <label style="display: block; font-weight: 500; font-size: 0.85em; margin-bottom: 4px;">Alternate Locators</label>
                            <div id="details-alternates" style="background: #f9fafb; border-radius: 4px; padding: 8px; font-size: 0.85em; max-height: 200px; overflow-y: auto;">
                                <div style="color: #999;">No alternate locators</div>
                            </div>
                        </div>

                        <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 12px; margin-top: 16px;">
                            <button class="btn btn-primary" onclick="editSelectedObject()" style="padding: 8px;">
                                <i class="fas fa-edit"></i> Edit
                            </button>
                            <button class="btn btn-danger" onclick="deleteSelectedObject()" style="padding: 8px;">
                                <i class="fas fa-trash"></i> Delete
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    `;

    viewContent.innerHTML = html;
    viewContent.classList.remove('hidden');

    // Hide other views
    document.querySelectorAll('.view-content').forEach(v => {
        if (v.id !== 'repository-view') v.classList.add('hidden');
    });

    await loadRepositoryObjects();
}

/**
 * Load repository objects from API
 */
async function loadRepositoryObjects() {
    try {
        const response = await fetch('/api/object-repository/all');
        const result = await response.json();
        
        if (result.success) {
            repositoryState.objects = result.data || [];
            
            // Extract categories
            const cats = new Set(repositoryState.objects.map(obj => obj.category || 'Uncategorized'));
            repositoryState.categories = Array.from(cats).sort();
            
            await displayRepositoryObjects();
            updateRepositoryStatistics();
        }
    } catch (error) {
        console.error('Error loading repository objects:', error);
        showNotification('Failed to load repository objects', 'error');
    }
}

/**
 * Display objects in the table
 */
function displayRepositoryObjects() {
    const tbody = document.getElementById('objects-tbody');
    const filtered = filterObjects();

    if (filtered.length === 0) {
        tbody.innerHTML = `
            <tr>
                <td colspan="5" style="padding: 30px; text-align: center; color: #999;">
                    ${repositoryState.objects.length === 0 ? 'No objects in repository' : 'No objects match your filters'}
                </td>
            </tr>
        `;
        return;
    }

    tbody.innerHTML = filtered.map((obj, idx) => `
        <tr style="border-bottom: 1px solid #e9d5ff; ${idx % 2 === 0 ? 'background: #faf5ff;' : ''}">
            <td style="padding: 10px 12px;"><strong>${escapeHtml(obj.name)}</strong></td>
            <td style="padding: 10px 12px;">
                <span style="background: #ede9fe; color: #7c3aed; padding: 4px 8px; border-radius: 12px; font-size: 0.8em;">
                    ${escapeHtml(obj.category || 'Uncategorized')}
                </span>
            </td>
            <td style="padding: 10px 12px;"><code style="background: #f9fafb; padding: 2px 4px; border-radius: 2px; font-size: 0.8em;">${obj.locatorType}</code></td>
            <td style="padding: 10px 12px; text-align: center;">
                <span style="background: #d1fae5; color: #065f46; padding: 2px 6px; border-radius: 4px; font-size: 0.75em;">
                    ${obj.usageCount || 0}
                </span>
            </td>
            <td style="padding: 10px 12px; text-align: center; display: flex; gap: 6px; justify-content: center; font-size: 0.85em;">
                <button class="btn btn-small" style="padding: 4px 8px;" onclick="viewObjectDetails('${obj.id}')">
                    <i class="fas fa-eye"></i> View
                </button>
                <button class="btn btn-danger btn-small" style="padding: 4px 8px;" onclick="deleteObject('${obj.id}')">
                    <i class="fas fa-trash"></i>
                </button>
            </td>
        </tr>
    `).join('');
}

/**
 * Filter objects based on current filters
 */
function filterObjects() {
    let filtered = repositoryState.objects;

    // Filter by category
    if (repositoryState.filterCategory !== 'all') {
        filtered = filtered.filter(obj => (obj.category || 'Uncategorized') === repositoryState.filterCategory);
    }

    // Filter by search query
    if (repositoryState.searchQuery) {
        const q = repositoryState.searchQuery.toLowerCase();
        filtered = filtered.filter(obj => 
            obj.name.toLowerCase().includes(q) || 
            obj.locatorValue.toLowerCase().includes(q)
        );
    }

    // Filter by locator type
    const typeFilter = document.getElementById('repo-locator-filter')?.value;
    if (typeFilter) {
        filtered = filtered.filter(obj => obj.locatorType === typeFilter);
    }

    return filtered;
}

/**
 * View object details
 */
function viewObjectDetails(objectId) {
    const obj = repositoryState.objects.find(o => o.id === objectId);
    if (!obj) return;

    repositoryState.selectedObject = obj;

    const panel = document.getElementById('object-details-panel');
    document.getElementById('details-name').textContent = escapeHtml(obj.name);
    document.getElementById('details-name-value').textContent = escapeHtml(obj.name);
    document.getElementById('details-category-value').textContent = escapeHtml(obj.category || 'Uncategorized');
    document.getElementById('details-type-value').textContent = escapeHtml(obj.elementType || 'Unknown');
    document.getElementById('details-usage-value').textContent = obj.usageCount || 0;
    document.getElementById('details-locator-type').textContent = obj.locatorType;
    document.getElementById('details-locator-value').textContent = escapeHtml(obj.locatorValue);

    // Display alternate locators
    const alternatesDiv = document.getElementById('details-alternates');
    if (obj.alternateLocators && obj.alternateLocators.length > 0) {
        alternatesDiv.innerHTML = obj.alternateLocators.map((alt, idx) => `
            <div style="margin-bottom: 8px; padding-bottom: 8px; border-bottom: 1px solid #e9d5ff;">
                <div style="font-weight: 500; font-size: 0.8em; color: #7c3aed; margin-bottom: 2px;">${escapeHtml(alt.locatorType)}</div>
                <div style="font-family: monospace; font-size: 0.8em; word-break: break-all; color: #666;">${escapeHtml(alt.locatorValue)}</div>
            </div>
        `).join('');
    }

    panel.style.display = 'block';
}

/**
 * Search repository objects
 */
function searchRepositoryObjects() {
    repositoryState.searchQuery = document.getElementById('repo-search')?.value || '';
    displayRepositoryObjects();
}

/**
 * Filter by category
 */
function filterByCategory(category) {
    repositoryState.filterCategory = category;
    
    // Update UI
    document.querySelectorAll('.repo-category').forEach(el => {
        el.classList.remove('active');
    });
    event.target.closest('.repo-category')?.classList.add('active');
    
    displayRepositoryObjects();
}

/**
 * Filter by locator type
 */
function filterByLocatorType() {
    displayRepositoryObjects();
}

/**
 * Clear repository filters
 */
function clearRepositoryFilters() {
    repositoryState.filterCategory = 'all';
    repositoryState.searchQuery = '';
    document.getElementById('repo-search').value = '';
    document.getElementById('repo-locator-filter').value = '';
    
    document.querySelectorAll('.repo-category').forEach(el => {
        el.classList.remove('active');
    });
    document.querySelector('.repo-category').classList.add('active');
    
    displayRepositoryObjects();
}

/**
 * Close details panel
 */
function closeDetailsPanel() {
    document.getElementById('object-details-panel').style.display = 'none';
    repositoryState.selectedObject = null;
}

/**
 * Delete object
 */
async function deleteObject(objectId) {
    if (!confirm('Are you sure you want to delete this object?')) return;

    try {
        const response = await fetch(`/api/object-repository/delete/${objectId}`, {
            method: 'DELETE'
        });
        const result = await response.json();

        if (result.success) {
            showNotification('Object deleted successfully', 'success');
            await loadRepositoryObjects();
            closeDetailsPanel();
        } else {
            showNotification(result.error || 'Failed to delete object', 'error');
        }
    } catch (error) {
        console.error('Error deleting object:', error);
        showNotification('Error deleting object', 'error');
    }
}

/**
 * Delete selected object
 */
async function deleteSelectedObject() {
    if (!repositoryState.selectedObject) return;
    await deleteObject(repositoryState.selectedObject.id);
}

/**
 * Edit selected object
 */
function editSelectedObject() {
    if (!repositoryState.selectedObject) return;
    
    // Show edit modal (similar to create but with populated fields)
    const obj = repositoryState.selectedObject;
    const modal = createEditObjectModal(obj);
    document.body.appendChild(modal);
}

/**
 * Show create object modal
 */
function showCreateObjectModal() {
    const modal = createCreateObjectModal();
    document.body.appendChild(modal);
}

/**
 * Create create object modal
 */
function createCreateObjectModal() {
    const modal = document.createElement('div');
    modal.className = 'modal';
    modal.id = 'create-object-modal';
    modal.style.cssText = 'position: fixed; top: 0; left: 0; width: 100%; height: 100%; background: rgba(0,0,0,0.5); display: flex; align-items: center; justify-content: center; z-index: 1000;';
    
    modal.innerHTML = `
        <div class="card" style="width: 90%; max-width: 500px; max-height: 90vh; overflow-y: auto;">
            <div style="padding: 16px; border-bottom: 1px solid #e9d5ff; display: flex; justify-content: space-between; align-items: center;">
                <h3 style="font-weight: 600;">Create New Object</h3>
                <button onclick="document.getElementById('create-object-modal').remove()" style="background: none; border: none; font-size: 1.5em; cursor: pointer;">×</button>
            </div>
            <form id="create-form" style="padding: 16px;">
                <div style="margin-bottom: 12px;">
                    <label style="display: block; font-weight: 500; margin-bottom: 4px;" for="obj-name">Object Name <span style="color: red;">*</span></label>
                    <input type="text" id="obj-name" required style="width: 100%; padding: 8px; border: 1px solid #e9d5ff; border-radius: 4px;">
                </div>
                <div style="margin-bottom: 12px;">
                    <label style="display: block; font-weight: 500; margin-bottom: 4px;" for="obj-category">Category</label>
                    <input type="text" id="obj-category" placeholder="e.g. Login, Dashboard" style="width: 100%; padding: 8px; border: 1px solid #e9d5ff; border-radius: 4px;">
                </div>
                <div style="margin-bottom: 12px;">
                    <label style="display: block; font-weight: 500; margin-bottom: 4px;" for="obj-locator-type">Locator Type <span style="color: red;">*</span></label>
                    <select id="obj-locator-type" required style="width: 100%; padding: 8px; border: 1px solid #e9d5ff; border-radius: 4px;">
                        <option value="" disabled selected>Select locator type</option>
                        <option value="CSS">CSS Selector</option>
                        <option value="XPath">XPath</option>
                        <option value="Robust">Robust Locator</option>
                    </select>
                </div>
                <div style="margin-bottom: 12px;">
                    <label style="display: block; font-weight: 500; margin-bottom: 4px;" for="obj-locator">Locator Value <span style="color: red;">*</span></label>
                    <textarea id="obj-locator" required rows="3" placeholder="Enter the locator string" style="width: 100%; padding: 8px; border: 1px solid #e9d5ff; border-radius: 4px; font-family: monospace; font-size: 0.85em;"></textarea>
                </div>
                <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 12px;">
                    <button type="button" class="btn btn-secondary" style="padding: 8px;" onclick="document.getElementById('create-object-modal').remove()">Cancel</button>
                    <button type="submit" class="btn btn-primary" style="padding: 8px;">Create</button>
                </div>
            </form>
        </div>
    `;

    modal.querySelector('#create-form').addEventListener('submit', async (e) => {
        e.preventDefault();
        await createObject({
            name: document.getElementById('obj-name').value,
            category: document.getElementById('obj-category').value,
            locatorType: document.getElementById('obj-locator-type').value,
            locatorValue: document.getElementById('obj-locator').value
        });
        modal.remove();
    });

    return modal;
}

/**
 * Create edit object modal
 */
function createEditObjectModal(obj) {
    const modal = document.createElement('div');
    modal.className = 'modal';
    modal.id = 'edit-object-modal';
    modal.style.cssText = 'position: fixed; top: 0; left: 0; width: 100%; height: 100%; background: rgba(0,0,0,0.5); display: flex; align-items: center; justify-content: center; z-index: 1000;';
    
    modal.innerHTML = `
        <div class="card" style="width: 90%; max-width: 500px; max-height: 90vh; overflow-y: auto;">
            <div style="padding: 16px; border-bottom: 1px solid #e9d5ff; display: flex; justify-content: space-between; align-items: center;">
                <h3 style="font-weight: 600;">Edit Object</h3>
                <button onclick="document.getElementById('edit-object-modal').remove()" style="background: none; border: none; font-size: 1.5em; cursor: pointer;">×</button>
            </div>
            <form id="edit-form" style="padding: 16px;">
                <div style="margin-bottom: 12px;">
                    <label style="display: block; font-weight: 500; margin-bottom: 4px;" for="edit-obj-name">Object Name</label>
                    <input type="text" id="edit-obj-name" value="${escapeHtml(obj.name)}" style="width: 100%; padding: 8px; border: 1px solid #e9d5ff; border-radius: 4px;">
                </div>
                <div style="margin-bottom: 12px;">
                    <label style="display: block; font-weight: 500; margin-bottom: 4px;" for="edit-obj-category">Category</label>
                    <input type="text" id="edit-obj-category" value="${escapeHtml(obj.category || '')}" style="width: 100%; padding: 8px; border: 1px solid #e9d5ff; border-radius: 4px;">
                </div>
                <div style="margin-bottom: 12px;">
                    <label style="display: block; font-weight: 500; margin-bottom: 4px;" for="edit-obj-locator">Locator Value</label>
                    <textarea id="edit-obj-locator" rows="3" style="width: 100%; padding: 8px; border: 1px solid #e9d5ff; border-radius: 4px; font-family: monospace; font-size: 0.85em;">${escapeHtml(obj.locatorValue)}</textarea>
                </div>
                <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 12px;">
                    <button type="button" class="btn btn-secondary" style="padding: 8px;" onclick="document.getElementById('edit-object-modal').remove()">Cancel</button>
                    <button type="submit" class="btn btn-primary" style="padding: 8px;">Update</button>
                </div>
            </form>
        </div>
    `;

    modal.querySelector('#edit-form').addEventListener('submit', async (e) => {
        e.preventDefault();
        await updateObject(obj.id, {
            name: document.getElementById('edit-obj-name').value,
            category: document.getElementById('edit-obj-category').value,
            locatorValue: document.getElementById('edit-obj-locator').value
        });
        modal.remove();
    });

    return modal;
}

/**
 * Create object via API
 */
async function createObject(data) {
    try {
        const response = await fetch('/api/object-repository/create', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data)
        });
        const result = await response.json();

        if (result.success) {
            showNotification('Object created successfully', 'success');
            await loadRepositoryObjects();
        } else {
            showNotification(result.error || 'Failed to create object', 'error');
        }
    } catch (error) {
        console.error('Error creating object:', error);
        showNotification('Error creating object', 'error');
    }
}

/**
 * Update object via API
 */
async function updateObject(objectId, data) {
    try {
        const response = await fetch(`/api/object-repository/update/${objectId}`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data)
        });
        const result = await response.json();

        if (result.success) {
            showNotification('Object updated successfully', 'success');
            await loadRepositoryObjects();
            closeDetailsPanel();
        } else {
            showNotification(result.error || 'Failed to update object', 'error');
        }
    } catch (error) {
        console.error('Error updating object:', error);
        showNotification('Error updating object', 'error');
    }
}

/**
 * Refresh repository
 */
async function refreshRepository() {
    await loadRepositoryObjects();
    showNotification('Repository refreshed', 'success');
}

/**
 * Update repository statistics
 */
function updateRepositoryStatistics() {
    document.getElementById('stat-total').textContent = repositoryState.objects.length;
    document.getElementById('stat-categories').textContent = repositoryState.categories.length;
    
    // Find most used object
    const mostUsed = repositoryState.objects.reduce((max, obj) => 
        (obj.usageCount > (max?.usageCount || 0)) ? obj : max, null
    );
    
    document.getElementById('stat-mostused').textContent = mostUsed ? escapeHtml(mostUsed.name) : '—';

    // Update category list
    const categoryList = document.getElementById('category-list');
    if (categoryList) {
        categoryList.innerHTML = `
            <div class="repo-category active" onclick="filterByCategory('all')" style="cursor: pointer; padding: 8px; border-radius: 4px; background: #f3e8ff; color: #7c3aed; font-weight: 500;">
                <i class="fas fa-cube"></i> All Objects
            </div>
            ${repositoryState.categories.map(cat => `
                <div class="repo-category" onclick="filterByCategory('${cat}')" style="cursor: pointer; padding: 8px; border-radius: 4px; color: #666; font-weight: 500;">
                    <i class="fas fa-folder"></i> ${escapeHtml(cat)}
                </div>
            `).join('')}
        `;
    }
}

/**
 * Escape HTML special characters
 */
function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}
