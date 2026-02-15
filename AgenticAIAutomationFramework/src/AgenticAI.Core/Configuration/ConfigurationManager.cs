using AgenticAI.Core.Enums;
using Newtonsoft.Json;
using Serilog;

namespace AgenticAI.Core.Configuration
{
    /// <summary>
    /// Singleton configuration manager for framework settings
    /// </summary>
    public class ConfigurationManager
    {
        private static ConfigurationManager? _instance;
        private static readonly object _lock = new object();
        private FrameworkConfiguration _frameworkConfig;
        private Dictionary<Enums.Environment, EnvironmentConfiguration> _environmentConfigs;

        private ConfigurationManager()
        {
            _frameworkConfig = new FrameworkConfiguration();
            _environmentConfigs = new Dictionary<Enums.Environment, EnvironmentConfiguration>();
            LoadConfigurations();
        }

        public static ConfigurationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new ConfigurationManager();
                        }
                    }
                }
                return _instance;
            }
        }

        public FrameworkConfiguration FrameworkConfig => _frameworkConfig;

        public EnvironmentConfiguration GetEnvironmentConfig(Enums.Environment environment)
        {
            if (_environmentConfigs.ContainsKey(environment))
            {
                return _environmentConfigs[environment];
            }
            
            Log.Warning($"Environment configuration for {environment} not found. Using default.");
            return new EnvironmentConfiguration();
        }

        public EnvironmentConfiguration CurrentEnvironmentConfig => 
            GetEnvironmentConfig(_frameworkConfig.Environment);

        private void LoadConfigurations()
        {
            try
            {
                // Load framework configuration
                var frameworkConfigPath = "Configuration/frameworkConfig.json";
                if (File.Exists(frameworkConfigPath))
                {
                    var json = File.ReadAllText(frameworkConfigPath);
                    var config = JsonConvert.DeserializeObject<FrameworkConfiguration>(json);
                    if (config != null)
                    {
                        _frameworkConfig = config;
                    }
                }
                else
                {
                    // Create default configuration file
                    SaveFrameworkConfiguration();
                }

                // Load environment configurations
                LoadEnvironmentConfigurations();
            }
            catch (Exception ex)
            {
                Log.Error($"Error loading configurations: {ex.Message}");
                // Use default configurations
            }
        }

        private void LoadEnvironmentConfigurations()
        {
            var envConfigPath = "Configuration/Environments";
            if (!Directory.Exists(envConfigPath))
            {
                Directory.CreateDirectory(envConfigPath);
                CreateDefaultEnvironmentConfigs();
            }

            foreach (Enums.Environment env in Enum.GetValues(typeof(Enums.Environment)))
            {
                var envFile = Path.Combine(envConfigPath, $"{env.ToString().ToLower()}.json");
                if (File.Exists(envFile))
                {
                    var json = File.ReadAllText(envFile);
                    var config = JsonConvert.DeserializeObject<EnvironmentConfiguration>(json);
                    if (config != null)
                    {
                        _environmentConfigs[env] = config;
                    }
                }
                else
                {
                    _environmentConfigs[env] = new EnvironmentConfiguration();
                }
            }
        }

        private void CreateDefaultEnvironmentConfigs()
        {
            var envConfigPath = "Configuration/Environments";

            // Dev environment
            var devConfig = new EnvironmentConfiguration
            {
                BaseUrl = "https://dev.example.com",
                ApiBaseUrl = "https://api-dev.example.com",
                Credentials = new Dictionary<string, string>
                {
                    { "username", "dev_user" },
                    { "password", "dev_password" }
                }
            };
            SaveEnvironmentConfiguration(Enums.Environment.Dev, devConfig);

            // QA environment
            var qaConfig = new EnvironmentConfiguration
            {
                BaseUrl = "https://qa.example.com",
                ApiBaseUrl = "https://api-qa.example.com",
                Credentials = new Dictionary<string, string>
                {
                    { "username", "qa_user" },
                    { "password", "qa_password" }
                }
            };
            SaveEnvironmentConfiguration(Enums.Environment.QA, qaConfig);

            // Prod environment
            var prodConfig = new EnvironmentConfiguration
            {
                BaseUrl = "https://www.example.com",
                ApiBaseUrl = "https://api.example.com",
                Credentials = new Dictionary<string, string>
                {
                    { "username", "prod_user" },
                    { "password", "prod_password" }
                }
            };
            SaveEnvironmentConfiguration(Enums.Environment.Prod, prodConfig);
        }

        public void SaveFrameworkConfiguration()
        {
            var configDir = "Configuration";
            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            var json = JsonConvert.SerializeObject(_frameworkConfig, Formatting.Indented);
            File.WriteAllText(Path.Combine(configDir, "frameworkConfig.json"), json);
        }

        public void SaveEnvironmentConfiguration(Enums.Environment environment, EnvironmentConfiguration config)
        {
            var envConfigPath = "Configuration/Environments";
            if (!Directory.Exists(envConfigPath))
            {
                Directory.CreateDirectory(envConfigPath);
            }

            var json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(Path.Combine(envConfigPath, $"{environment.ToString().ToLower()}.json"), json);
            _environmentConfigs[environment] = config;
        }

        public void UpdateFrameworkConfiguration(Action<FrameworkConfiguration> updateAction)
        {
            updateAction(_frameworkConfig);
            SaveFrameworkConfiguration();
        }
    }
}
