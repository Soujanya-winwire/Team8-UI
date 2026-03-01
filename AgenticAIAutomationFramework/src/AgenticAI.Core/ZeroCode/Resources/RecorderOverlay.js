// Floating control panel for test recording
(function() {
    'use strict';
    
    // Create floating control panel
    const controlPanel = document.createElement('div');
    controlPanel.id = '__playwright_recorder_controls';
    controlPanel.style.cssText = `
        position: fixed;
        top: 10px;
        right: 10px;
        z-index: 999999;
        background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
        color: white;
        padding: 15px 20px;
        border-radius: 12px;
        box-shadow: 0 8px 32px rgba(0,0,0,0.3);
        font-family: 'Segoe UI', sans-serif;
        font-size: 14px;
        font-weight: 600;
        display: flex;
        align-items: center;
        gap: 15px;
        cursor: move;
    `;
    
    controlPanel.innerHTML = `
        <div style="display: flex; align-items: center; gap: 8px;">
            <div id="__recorder_pulse" style="
                width: 12px;
                height: 12px;
                background: #ef4444;
                border-radius: 50%;
            "></div>
            <span>RECORDING</span>
        </div>
        <div style="
            background: rgba(255,255,255,0.2);
            padding: 8px 15px;
            border-radius: 8px;
            font-size: 12px;
        ">
            <span id="__recorder_action_count">0</span> actions captured
        </div>
        <button id="__recorder_stop_btn" style="
            background: white;
            color: #667eea;
            border: none;
            padding: 8px 16px;
            border-radius: 8px;
            font-weight: 700;
            cursor: pointer;
            font-size: 12px;
            transition: all 0.2s;
        ">
            ⏹ Stop Recording
        </button>
    `;
    
    // Add to page when DOM is ready
    if (document.body) {
        document.body.appendChild(controlPanel);
    } else {
        document.addEventListener('DOMContentLoaded', function() {
            document.body.appendChild(controlPanel);
        });
    }
    
    // Pulse animation
    setInterval(function() {
        const pulse = document.getElementById('__recorder_pulse');
        if (pulse) {
            pulse.style.opacity = pulse.style.opacity === '0.3' ? '1' : '0.3';
        }
    }, 1000);
    
    // Handle stop recording button
    setTimeout(function() {
        const stopBtn = document.getElementById('__recorder_stop_btn');
        if (stopBtn) {
            stopBtn.addEventListener('click', function() {
                if (confirm('Stop recording and save test scenario?')) {
                    alert('Recording stopped! Close this browser window and return to the UI to save your test.');
                    window.close();
                }
            });
            
            stopBtn.addEventListener('mouseover', function() {
                this.style.transform = 'scale(1.05)';
            });
            
            stopBtn.addEventListener('mouseout', function() {
                this.style.transform = 'scale(1)';
            });
        }
    }, 500);
    
    // Update action count function
    window.__updateRecorderCount = function(count) {
        const countEl = document.getElementById('__recorder_action_count');
        if (countEl) countEl.textContent = count;
    };
    
    console.log('RECORDER: Control panel loaded');
})();
