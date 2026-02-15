using Microsoft.AspNetCore.SignalR;

namespace AgenticAI.WebUI.Hubs
{
    /// <summary>
    /// SignalR hub for real-time test execution updates
    /// </summary>
    public class TestExecutionHub : Hub
    {
        public async Task SendTestUpdate(string testName, string status, string message)
        {
            await Clients.All.SendAsync("ReceiveTestUpdate", testName, status, message);
        }

        public async Task SendTestProgress(int current, int total)
        {
            await Clients.All.SendAsync("ReceiveTestProgress", current, total);
        }

        public async Task SendTestResult(object result)
        {
            await Clients.All.SendAsync("ReceiveTestResult", result);
        }

        public override async Task OnConnectedAsync()
        {
            await Clients.Caller.SendAsync("Connected", Context.ConnectionId);
            await base.OnConnectedAsync();
        }
    }
}
