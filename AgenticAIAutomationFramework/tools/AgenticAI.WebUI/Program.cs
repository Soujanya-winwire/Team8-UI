using AgenticAI.WebUI.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSignalR();

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
app.UseStaticFiles();
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
