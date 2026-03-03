using AgenticAI.Core.Logging;

namespace AgenticAI.Core.ZeroCode
{
    /// <summary>
    /// Manages iframe context tracking and switching during test recording and execution
    /// Automatically detects iframe context and generates appropriate switch actions
    /// </summary>
    public class IFrameContext
    {
        private readonly Stack<IFrameInfo> _iframeStack;
        private IFrameInfo? _currentFrame;

        public IFrameContext()
        {
            _iframeStack = new Stack<IFrameInfo>();
            _currentFrame = null;
        }

        /// <summary>
        /// Current iframe context (null if in default/main frame)
        /// </summary>
        public IFrameInfo? CurrentFrame => _currentFrame;

        /// <summary>
        /// Whether currently inside an iframe
        /// </summary>
        public bool IsInFrame => _currentFrame != null;

        /// <summary>
        /// Depth of nested iframes (0 = main frame)
        /// </summary>
        public int FrameDepth => _iframeStack.Count;

        /// <summary>
        /// Enter an iframe context
        /// </summary>
        public void EnterFrame(string frameLocator, string frameSelector, int frameIndex = -1)
        {
            var frameInfo = new IFrameInfo
            {
                Locator = frameLocator,
                Selector = frameSelector,
                Index = frameIndex,
                EnteredAt = DateTime.Now
            };

            _iframeStack.Push(frameInfo);
            _currentFrame = frameInfo;
            
            Logger.Debug($"Entered iframe: {frameSelector} (depth: {FrameDepth})");
        }

        /// <summary>
        /// Exit current iframe context (go up one level)
        /// </summary>
        public IFrameInfo? ExitFrame()
        {
            if (_iframeStack.Count == 0)
            {
                Logger.Warning("Attempted to exit frame but no frame context exists");
                return null;
            }

            var exitedFrame = _iframeStack.Pop();
            _currentFrame = _iframeStack.Count > 0 ? _iframeStack.Peek() : null;
            
            Logger.Debug($"Exited iframe: {exitedFrame.Selector} (new depth: {FrameDepth})");
            
            return exitedFrame;
        }

        /// <summary>
        /// Switch to default/main content (exit all iframes)
        /// </summary>
        public void SwitchToDefaultContent()
        {
            var frameCount = _iframeStack.Count;
            
            _iframeStack.Clear();
            _currentFrame = null;
            
            if (frameCount > 0)
            {
                Logger.Debug($"Switched to default content (exited {frameCount} frame(s))");
            }
        }

        /// <summary>
        /// Get the complete iframe path from root to current frame
        /// </summary>
        public List<IFrameInfo> GetFramePath()
        {
            return _iframeStack.Reverse().ToList();
        }

        /// <summary>
        /// Check if we're in the same frame as specified
        /// </summary>
        public bool IsInSameFrame(string frameLocator)
        {
            return _currentFrame?.Locator == frameLocator;
        }

        /// <summary>
        /// Clone the current context state
        /// </summary>
        public IFrameContext Clone()
        {
            var cloned = new IFrameContext();
            foreach (var frame in _iframeStack.Reverse())
            {
                cloned._iframeStack.Push(frame);
            }
            cloned._currentFrame = _currentFrame;
            return cloned;
        }

        /// <summary>
        /// Get debug information about current frame context
        /// </summary>
        public string GetContextInfo()
        {
            if (!IsInFrame)
                return "Main content (no iframe)";

            var path = string.Join(" > ", GetFramePath().Select(f => f.Selector));
            return $"Inside iframe (depth {FrameDepth}): {path}";
        }
    }

    /// <summary>
    /// Information about an iframe context
    /// </summary>
    public class IFrameInfo
    {
        public string Locator { get; set; } = "";
        public string Selector { get; set; } = "";
        public int Index { get; set; } = -1;
        public DateTime EnteredAt { get; set; }
        
        public override string ToString()
        {
            return $"Frame: {Selector}" + (Index >= 0 ? $" [index: {Index}]" : "");
        }
    }
}
