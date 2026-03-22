using AgenticAI.Core.Models;

namespace AgenticAI.Core.Utilities
{
    /// <summary>
    /// Interface for test lifecycle hooks
    /// </summary>
    public interface ITestHook
    {
        /// <summary>
        /// Called before test starts
        /// </summary>
        Task OnBeforeTestAsync(TestHookContext context);

        /// <summary>
        /// Called after test completes
        /// </summary>
        Task OnAfterTestAsync(TestHookContext context);

        /// <summary>
        /// Called before each step
        /// </summary>
        Task OnBeforeStepAsync(TestStepHookContext context);

        /// <summary>
        /// Called after each step
        /// </summary>
        Task OnAfterStepAsync(TestStepHookContext context);

        /// <summary>
        /// Called on test failure
        /// </summary>
        Task OnTestFailureAsync(TestFailureContext context);
    }

    /// <summary>
    /// Context passed to test hooks
    /// </summary>
    public class TestHookContext
    {
        public string TestName { get; set; } = "";
        public string TestId { get; set; } = "";
        public string Module { get; set; } = "";
        public List<string> Tags { get; set; } = new();
        public Dictionary<string, object> Properties { get; set; } = new();
        public DateTime StartTime { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Context passed to step hooks
    /// </summary>
    public class TestStepHookContext
    {
        public string TestName { get; set; } = "";
        public string StepName { get; set; } = "";
        public int StepNumber { get; set; }
        public string? Action { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
        public DateTime ExecutionTime { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Context passed to failure hooks
    /// </summary>
    public class TestFailureContext
    {
        public string TestName { get; set; } = "";
        public string TestId { get; set; } = "";
        public Exception? Exception { get; set; }
        public string? ErrorMessage { get; set; }
        public string? StackTrace { get; set; }
        public TestCaseResult? TestResult { get; set; }
        public DateTime FailureTime { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Manager for test lifecycle hooks
    /// </summary>
    public class TestHookManager
    {
        private List<ITestHook> _hooks = new();
        private static TestHookManager? _instance;
        private static readonly object _lockObject = new();

        public static TestHookManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lockObject)
                    {
                        _instance ??= new TestHookManager();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Register a hook
        /// </summary>
        public void RegisterHook(ITestHook hook)
        {
            _hooks.Add(hook);
        }

        /// <summary>
        /// Unregister a hook
        /// </summary>
        public void UnregisterHook(ITestHook hook)
        {
            _hooks.Remove(hook);
        }

        /// <summary>
        /// Execute before test hooks
        /// </summary>
        public async Task ExecuteBeforeTestHooksAsync(TestHookContext context)
        {
            foreach (var hook in _hooks)
            {
                await hook.OnBeforeTestAsync(context);
            }
        }

        /// <summary>
        /// Execute after test hooks
        /// </summary>
        public async Task ExecuteAfterTestHooksAsync(TestHookContext context)
        {
            foreach (var hook in _hooks)
            {
                await hook.OnAfterTestAsync(context);
            }
        }

        /// <summary>
        /// Execute before step hooks
        /// </summary>
        public async Task ExecuteBeforeStepHooksAsync(TestStepHookContext context)
        {
            foreach (var hook in _hooks)
            {
                await hook.OnBeforeStepAsync(context);
            }
        }

        /// <summary>
        /// Execute after step hooks
        /// </summary>
        public async Task ExecuteAfterStepHooksAsync(TestStepHookContext context)
        {
            foreach (var hook in _hooks)
            {
                await hook.OnAfterStepAsync(context);
            }
        }

        /// <summary>
        /// Execute failure hooks
        /// </summary>
        public async Task ExecuteFailureHooksAsync(TestFailureContext context)
        {
            foreach (var hook in _hooks)
            {
                await hook.OnTestFailureAsync(context);
            }
        }

        /// <summary>
        /// Get all registered hooks
        /// </summary>
        public IReadOnlyList<ITestHook> GetAllHooks()
        {
            return _hooks.AsReadOnly();
        }

        /// <summary>
        /// Clear all hooks
        /// </summary>
        public void ClearHooks()
        {
            _hooks.Clear();
        }
    }

    /// <summary>
    /// Base implementation of ITestHook for convenience
    /// </summary>
    public abstract class BaseTestHook : ITestHook
    {
        public virtual Task OnBeforeTestAsync(TestHookContext context)
        {
            return Task.CompletedTask;
        }

        public virtual Task OnAfterTestAsync(TestHookContext context)
        {
            return Task.CompletedTask;
        }

        public virtual Task OnBeforeStepAsync(TestStepHookContext context)
        {
            return Task.CompletedTask;
        }

        public virtual Task OnAfterStepAsync(TestStepHookContext context)
        {
            return Task.CompletedTask;
        }

        public virtual Task OnTestFailureAsync(TestFailureContext context)
        {
            return Task.CompletedTask;
        }
    }
}
