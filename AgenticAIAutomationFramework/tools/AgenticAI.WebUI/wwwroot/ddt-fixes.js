// DATA-DRIVEN TESTING - Critical Bug Fixes
// Fixes: data persistence, result deduplication, and collapsible output

// FIX 1: Sync in-memory data to textarea before execution
const origExecute = window.executeDataDriven;
window.executeDataDriven = async function() {
    const ta = document.getElementById('dd-data');
    if ((!ta || !ta.value || ta.value.trim() === '') && 
        window.ddtCurrentData && window.ddtCurrentData.length > 0) {
        const csv = convertToCSV(window.ddtCurrentData);
        if (ta) ta.value = csv;
        console.log('[FIXED] Data synced to textarea');
    }
    return origExecute.apply(this, arguments);
};

// FIX 2: Clear results before rendering to prevent duplicates
const origRender = window.renderDataDrivenResults;
window.renderDataDrivenResults = function(data) {
    const s = document.getElementById('dd-summary-bar');
    const t = document.getElementById('dd-results-table');
    if (s) s.innerHTML = '';
    if (t) t.innerHTML = '';
    return origRender.apply(this, arguments);
};

// FIX 3: Make console collapsible
setTimeout(function() {
    const co = document.getElementById('dd-console-output');
    if (co) {
        co.style.display = 'none';
        co.style.maxHeight = '300px';
        co.style.overflow = 'auto';
        const hdr = document.querySelector('[id*="console"][id*="card"] .card-header');
        if (hdr) {
            hdr.style.cursor = 'pointer';
            hdr.onclick = function(e) {
                if (e.target.closest('.btn')) return;
                co.style.display = co.style.display === 'none' ? 'block' : 'none';
            };
        }
    }
}, 500);

console.log('[DDT-FIXES] All critical fixes applied');
