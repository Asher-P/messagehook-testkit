using System.Text.Json.Serialization;
using MessageHook.Playbook.Execution;
using MessageHook.Playbook.Service.Endpoints;
using MessageHook.Playbook.Service.Runs;
using MessageHook.Playbook.Service.Storage;

var builder = WebApplication.CreateBuilder(args);

// Suites/payloads live on a volume in the container; default to a local ./data folder for dev.
var dataDir = builder.Configuration["DataDir"]
              ?? Path.Combine(builder.Environment.ContentRootPath, "data");

builder.Services.AddSingleton(new SuiteStore(dataDir));
builder.Services.AddSingleton<PlaybookRunner>();
builder.Services.AddSingleton<RunManager>();

// Match the playbook file format: string enums, case-insensitive, omit nulls — so the HTTP API and the
// on-disk playbook JSON have exactly the same shape.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
});

// Internal test tool: allow the Vite dev server (and any local origin) to call the API in development.
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapApi();

// SPA fallback: any non-API, non-file route serves the React index (client-side routing).
app.MapFallbackToFile("index.html");

app.Run();
