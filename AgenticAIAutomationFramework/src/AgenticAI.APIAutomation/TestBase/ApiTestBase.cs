using AgenticAI.APIAutomation.Client;
using AgenticAI.Core.Configuration;
using AgenticAI.Core.TestBase;
using NUnit.Framework;

namespace AgenticAI.APIAutomation.TestBase
{
    /// <summary>
    /// Base class for API automation tests
    /// </summary>
    public abstract class ApiTestBase : BaseTest
    {
        protected ApiClient ApiClient { get; private set; } = null!;

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            InitializeApiClient();
        }

        private void InitializeApiClient()
        {
            ApiClient = new ApiClient(EnvConfig.ApiBaseUrl);
            
            // Setup default authentication if configured
            if (EnvConfig.ApiKeys.ContainsKey("DefaultApiKey"))
            {
                ApiClient.SetApiKey("X-API-Key", EnvConfig.ApiKeys["DefaultApiKey"]);
            }
        }

        [TearDown]
        public override async Task TearDown()
        {
            await base.TearDown();
            ApiClient?.ClearContextData();
        }
    }
}
