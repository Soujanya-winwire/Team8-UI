using AgenticAI.WebUI.Hubs;
using Microsoft.Extensions.FileProviders;
using AgenticAI.Core.Logging;

var builder = WebApplication.CreateBuilder(args);

// Initialize logging
Logger.Initialize();

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();

// Add HttpClient for CI/CD API calls
builder.Services.AddHttpClient();

builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// Add CORS for frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder => builder
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseCors("AllowAll");

// Serve static files from wwwroot
app.UseStaticFiles();

// Serve TestResults directory for screenshots and videos
var testResultsPath = Path.Combine(builder.Environment.ContentRootPath, "TestResults");
if (!Directory.Exists(testResultsPath))
{
    Directory.CreateDirectory(testResultsPath);
}
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(testResultsPath),
    RequestPath = "/TestResults"
});

app.UseRouting();
app.UseAuthorization();

app.MapControllers();
app.MapHub<TestExecutionHub>("/testExecutionHub");

// Serve the UI
app.MapGet("/", () => Results.Redirect("/index.html"));

Console.WriteLine("??????????????????????????????????????????????????????????");
Console.WriteLine("?   ?? Agentic AI Test Management Platform             ?");
Console.WriteLine("??????????????????????????????????????????????????????????");
Console.WriteLine();
Console.WriteLine("?? Web UI: http://localhost:5000");
Console.WriteLine("?? SignalR Hub: ws://localhost:5000/testExecutionHub");
Console.WriteLine();
Console.WriteLine("? Server is running...");
Console.WriteLine();

app.Run("http://localhost:5000");
