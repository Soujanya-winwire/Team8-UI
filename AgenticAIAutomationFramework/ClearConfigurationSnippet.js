// ?? Clear AgenticAI Configuration from Browser Cache
// Copy and paste this in your browser console (F12 ? Console tab)

console.log('?? Clearing AgenticAI configuration from localStorage...');

// Remove the cached configuration
localStorage.removeItem('agenticai-config');

console.log('? Configuration cleared!');
console.log('?? Now refresh the page (F5) to see empty Base URL field');

// Auto-refresh option
if (confirm('Configuration cleared! Refresh page now?')) {
    location.reload();
}
