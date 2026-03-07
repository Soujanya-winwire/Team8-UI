using AgenticAI.Core.Logging;
using System.Text.Json;

namespace AgenticAI.Core.ZeroCode
{
    /// <summary>
    /// Detects iframe context for elements during recording
    /// Automatically generates iframe switch actions when needed
    /// </summary>
    public class IFrameDetector
    {
        private readonly IFrameContext _context;

        public IFrameDetector(IFrameContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Detect if an element is inside an iframe and return the iframe path
        /// </summary>
        public async Task<IFrameDetectionResult> DetectIFrameAsync(string elementSelector, Func<string, Task<JsonElement>> evaluateJsFunc)
        {
            try
            {
                // JavaScript to detect iframe context
                var script = @"
                    (selector) => {
                        function findElementAndFrames(sel) {
                            // Try to find element in main document
                            let element = document.querySelector(sel);
                            if (element) {
                                return { found: true, frames: [], element: sel };
                            }

                            // Search in all iframes
                            const iframes = document.querySelectorAll('iframe, frame');
                            for (let i = 0; i < iframes.length; i++) {
                                const iframe = iframes[i];
                                try {
                                    const iframeDoc = iframe.contentDocument || iframe.contentWindow?.document;
                                    if (!iframeDoc) continue;

                                    element = iframeDoc.querySelector(sel);
                                    if (element) {
                                        // Get iframe locator
                                        const iframeId = iframe.id;
                                        const iframeName = iframe.name;
                                        const iframeSrc = iframe.src;
                                        const iframeIndex = i;

                                        let iframeSelector = '';
                                        if (iframeId) {
                                            iframeSelector = `#${iframeId}`;
                                        } else if (iframeName) {
                                            iframeSelector = `iframe[name='${iframeName}']`;
                                        } else if (iframeSrc) {
                                            const srcPart = iframeSrc.substring(iframeSrc.lastIndexOf('/') + 1, iframeSrc.length).split('?')[0];
                                            iframeSelector = `iframe[src*='${srcPart}']`;
                                        } else {
                                            iframeSelector = `iframe:nth-of-type(${iframeIndex + 1})`;
                                        }

                                        return {
                                            found: true,
                                            frames: [{
                                                selector: iframeSelector,
                                                index: iframeIndex,
                                                id: iframeId || '',
                                                name: iframeName || '',
                                                src: iframeSrc || ''
                                            }],
                                            element: sel
                                        };
                                    }

                                    // Check nested iframes recursively
                                    const nestedIframes = iframeDoc.querySelectorAll('iframe, frame');
                                    for (let j = 0; j < nestedIframes.length; j++) {
                                        const nested = nestedIframes[j];
                                        try {
                                            const nestedDoc = nested.contentDocument || nested.contentWindow?.document;
                                            if (!nestedDoc) continue;

                                            element = nestedDoc.querySelector(sel);
                                            if (element) {
                                                // Build nested frame path
                                                const frames = [];
                                                
                                                // Parent iframe
                                                let parentSelector = '';
                                                if (iframe.id) parentSelector = `#${iframe.id}`;
                                                else if (iframe.name) parentSelector = `iframe[name='${iframe.name}']`;
                                                else parentSelector = `iframe:nth-of-type(${i + 1})`;
                                                
                                                frames.push({
                                                    selector: parentSelector,
                                                    index: i,
                                                    id: iframe.id || '',
                                                    name: iframe.name || ''
                                                });

                                                // Nested iframe
                                                let nestedSelector = '';
                                                if (nested.id) nestedSelector = `#${nested.id}`;
                                                else if (nested.name) nestedSelector = `iframe[name='${nested.name}']`;
                                                else nestedSelector = `iframe:nth-of-type(${j + 1})`;

                                                frames.push({
                                                    selector: nestedSelector,
                                                    index: j,
                                                    id: nested.id || '',
                                                    name: nested.name || ''
                                                });

                                                return { found: true, frames: frames, element: sel };
                                            }
                                        } catch (e) {
                                            // Cross-origin nested iframe
                                        }
                                    }
                                } catch (e) {
                                    // Cross-origin iframe - can't access
                                }
                            }

                            return { found: false, frames: [], element: sel };
                        }

                        return findElementAndFrames(selector);
                    }
                ";

                var result = await evaluateJsFunc($"{script}('{elementSelector}')");

                var found = result.GetProperty("found").GetBoolean();
                
                if (!found)
                {
                    Logger.Debug($"Element '{elementSelector}' not found in any frame");
                    return new IFrameDetectionResult
                    {
                        IsInFrame = false,
                        ElementFound = false
                    };
                }

                var framesArray = result.GetProperty("frames");
                var frameCount = framesArray.GetArrayLength();

                if (frameCount == 0)
                {
                    // Element is in main document
                    return new IFrameDetectionResult
                    {
                        IsInFrame = false,
                        ElementFound = true,
                        FramePath = new List<IFrameInfo>()
                    };
                }

                // Element is inside iframe(s)
                var framePath = new List<IFrameInfo>();
                for (int i = 0; i < frameCount; i++)
                {
                    var frame = framesArray[i];
                    framePath.Add(new IFrameInfo
                    {
                        Selector = frame.GetProperty("selector").GetString() ?? "",
                        Index = frame.GetProperty("index").GetInt32(),
                        Locator = frame.GetProperty("selector").GetString() ?? ""
                    });
                }

                Logger.Info($"Element '{elementSelector}' found in iframe (depth: {frameCount})");
                
                return new IFrameDetectionResult
                {
                    IsInFrame = true,
                    ElementFound = true,
                    FramePath = framePath
                };
            }
            catch (Exception ex)
            {
                Logger.Error($"Error detecting iframe context: {ex.Message}");
                return new IFrameDetectionResult
                {
                    IsInFrame = false,
                    ElementFound = false,
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// Check if iframe switch is needed based on current context
        /// </summary>
        public bool NeedsFrameSwitch(IFrameDetectionResult detection)
        {
            if (!detection.IsInFrame)
            {
                // Element is in main content but we're in a frame - need to switch out
                return _context.IsInFrame;
            }

            // Element is in iframe - check if it's different from current frame
            if (!_context.IsInFrame)
            {
                // We're in main but element is in iframe - need to switch in
                return true;
            }

            // Both in iframe - check if same frame
            var currentPath = _context.GetFramePath();
            var targetPath = detection.FramePath;

            if (currentPath.Count != targetPath.Count)
                return true;

            for (int i = 0; i < currentPath.Count; i++)
            {
                if (currentPath[i].Selector != targetPath[i].Selector)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Generate iframe switch actions based on detection result
        /// </summary>
        public List<Models.RecordedAction> GenerateSwitchActions(IFrameDetectionResult detection)
        {
            var actions = new List<Models.RecordedAction>();

            if (!detection.IsInFrame)
            {
                // Need to switch to default content if currently in frame
                if (_context.IsInFrame)
                {
                    actions.Add(new Models.RecordedAction
                    {
                        ActionType = "SwitchToDefaultContent",
                        Locator = "",
                        Description = "Switch to default content (exit all iframes)",
                        Timestamp = actions.Count
                    });
                }
                return actions;
            }

            // Need to switch to iframe(s)
            // First, switch to default if in wrong frame
            if (_context.IsInFrame)
            {
                actions.Add(new Models.RecordedAction
                {
                    ActionType = "SwitchToDefaultContent",
                    Locator = "",
                    Description = "Switch to default content before entering target iframe",
                    Timestamp = actions.Count
                });
            }

            // Then switch into each nested frame
            foreach (var frame in detection.FramePath)
            {
                actions.Add(new Models.RecordedAction
                {
                    ActionType = "SwitchToFrame",
                    Locator = frame.Selector,
                    Value = frame.Index >= 0 ? frame.Index.ToString() : "",
                    Description = $"Switch to iframe: {frame.Selector}",
                    Timestamp = actions.Count
                });
            }

            return actions;
        }
    }

    /// <summary>
    /// Result of iframe detection
    /// </summary>
    public class IFrameDetectionResult
    {
        public bool ElementFound { get; set; }
        public bool IsInFrame { get; set; }
        public List<IFrameInfo> FramePath { get; set; } = new();
        public string? Error { get; set; }

        public bool HasError => !string.IsNullOrEmpty(Error);
    }
}
