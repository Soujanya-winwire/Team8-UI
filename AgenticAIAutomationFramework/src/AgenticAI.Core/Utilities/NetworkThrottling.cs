using AgenticAI.Core.Logging;

namespace AgenticAI.Core.Utilities
{
    /// <summary>
    /// Simulates network conditions for testing
    /// </summary>
    public class NetworkThrottling
    {
        /// <summary>
        /// Common network profiles
        /// </summary>
        public enum NetworkProfile
        {
            Fast3G,      // 1600 Kbps, 150ms latency
            Slow3G,      // 400 Kbps, 400ms latency
            LTE,         // 4000 Kbps, 50ms latency
            WIFI,        // 30000 Kbps, 2ms latency
            Offline,     // No connection
            Custom
        }

        public class NetworkSettings
        {
            public string Name { get; set; } = "";
            public int DownloadKbps { get; set; }
            public int UploadKbps { get; set; }
            public int LatencyMs { get; set; }
            public double PacketLossPercentage { get; set; } = 0;
        }

        private static Dictionary<NetworkProfile, NetworkSettings> _profiles = new()
        {
            {
                NetworkProfile.Fast3G, new NetworkSettings
                {
                    Name = "Fast 3G",
                    DownloadKbps = 1600,
                    UploadKbps = 750,
                    LatencyMs = 150,
                    PacketLossPercentage = 0
                }
            },
            {
                NetworkProfile.Slow3G, new NetworkSettings
                {
                    Name = "Slow 3G",
                    DownloadKbps = 400,
                    UploadKbps = 400,
                    LatencyMs = 400,
                    PacketLossPercentage = 0.5
                }
            },
            {
                NetworkProfile.LTE, new NetworkSettings
                {
                    Name = "LTE",
                    DownloadKbps = 4000,
                    UploadKbps = 3000,
                    LatencyMs = 50,
                    PacketLossPercentage = 0
                }
            },
            {
                NetworkProfile.WIFI, new NetworkSettings
                {
                    Name = "WiFi",
                    DownloadKbps = 30000,
                    UploadKbps = 15000,
                    LatencyMs = 2,
                    PacketLossPercentage = 0
                }
            },
            {
                NetworkProfile.Offline, new NetworkSettings
                {
                    Name = "Offline",
                    DownloadKbps = 0,
                    UploadKbps = 0,
                    LatencyMs = 0,
                    PacketLossPercentage = 100
                }
            }
        };

        /// <summary>
        /// Get preset network profile
        /// </summary>
        public static NetworkSettings GetProfile(NetworkProfile profile)
        {
            return _profiles[profile];
        }

        /// <summary>
        /// Create custom network settings
        /// </summary>
        public static NetworkSettings CreateCustomProfile(
            string name,
            int downloadKbps,
            int uploadKbps,
            int latencyMs,
            double packetLossPercentage = 0)
        {
            return new NetworkSettings
            {
                Name = name,
                DownloadKbps = downloadKbps,
                UploadKbps = uploadKbps,
                LatencyMs = latencyMs,
                PacketLossPercentage = packetLossPercentage
            };
        }

        /// <summary>
        /// Simulate network delay
        /// </summary>
        public static async Task SimulateNetworkDelayAsync(int latencyMs)
        {
            if (latencyMs > 0)
            {
                await Task.Delay(latencyMs);
                Logger.Debug($"Simulated network latency: {latencyMs}ms");
            }
        }

        /// <summary>
        /// Estimate page load time under network conditions
        /// </summary>
        public static long EstimateLoadTime(
            long baselineLoadTimeMs,
            NetworkSettings networkSettings)
        {
            // Calculate bandwidth ratio (compared to WiFi)
            var wifiSettings = _profiles[NetworkProfile.WIFI];
            var bandwidthRatio = (double)wifiSettings.DownloadKbps / networkSettings.DownloadKbps;

            // Estimate time with latency and bandwidth consideration
            var estimatedTime = baselineLoadTimeMs * bandwidthRatio + networkSettings.LatencyMs;

            return (long)estimatedTime;
        }

        /// <summary>
        /// Generate a network profile report
        /// </summary>
        public static string GenerateProfileReport(NetworkSettings settings)
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== Network Profile ===");
            report.AppendLine($"Name: {settings.Name}");
            report.AppendLine($"Download Speed: {settings.DownloadKbps} Kbps ({settings.DownloadKbps / 1000.0:F1} Mbps)");
            report.AppendLine($"Upload Speed: {settings.UploadKbps} Kbps ({settings.UploadKbps / 1000.0:F1} Mbps)");
            report.AppendLine($"Latency: {settings.LatencyMs}ms");
            report.AppendLine($"Packet Loss: {settings.PacketLossPercentage}%");
            report.AppendLine();

            // Calculate time to download common file sizes
            var testSizes = new[] { 1, 5, 10, 50 }; // MB
            report.AppendLine("Estimated Download Times:");
            foreach (var sizeMb in testSizes)
            {
                var sizeKb = sizeMb * 1024;
                var timeMs = (sizeKb * 8000) / settings.DownloadKbps + settings.LatencyMs;
                var timeSecs = timeMs / 1000.0;
                report.AppendLine($"  {sizeMb}MB file: {timeSecs:F2} seconds");
            }

            return report.ToString();
        }

        /// <summary>
        /// Get all available profiles
        /// </summary>
        public static Dictionary<NetworkProfile, NetworkSettings> GetAllProfiles()
        {
            return new Dictionary<NetworkProfile, NetworkSettings>(_profiles);
        }
    }

    /// <summary>
    /// Bandwidth calculator
    /// </summary>
    public class BandwidthCalculator
    {
        /// <summary>
        /// Calculate time to download content
        /// </summary>
        public static double CalculateDownloadTime(long fileSizeBytes, int bandwidthKbps)
        {
            if (bandwidthKbps <= 0)
                return double.PositiveInfinity;

            var fileSizeBits = fileSizeBytes * 8;
            var timeMs = (fileSizeBits * 1000.0) / (bandwidthKbps * 1000);
            return timeMs;
        }

        /// <summary>
        /// Calculate effective bandwidth after packet loss
        /// </summary>
        public static int CalculateEffectiveBandwidth(
            int bandwidthKbps,
            double packetLossPercentage)
        {
            if (packetLossPercentage <= 0)
                return bandwidthKbps;

            // Rough estimation: packet loss reduces throughput significantly
            var retransmissionFactor = 1 + (packetLossPercentage / 100.0);
            var effectiveBandwidth = (int)(bandwidthKbps / retransmissionFactor);

            return effectiveBandwidth;
        }

        /// <summary>
        /// Convert between bandwidth units
        /// </summary>
        public static double ConvertBandwidth(double value, string fromUnit, string toUnit)
        {
            var valueInBps = fromUnit.ToUpper() switch
            {
                "KBPS" => value * 1000,
                "MBPS" => value * 1000 * 1000,
                "GBPS" => value * 1000 * 1000 * 1000,
                _ => value
            };

            return toUnit.ToUpper() switch
            {
                "KBPS" => valueInBps / 1000,
                "MBPS" => valueInBps / (1000 * 1000),
                "GBPS" => valueInBps / (1000 * 1000 * 1000),
                _ => valueInBps
            };
        }
    }
}
