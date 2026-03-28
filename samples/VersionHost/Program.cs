using LayeredChat;
using LayeredChat.Connectors.OpenAiCompatible;
using LayeredChat.VersionHost;

var builder = WebApplication.CreateBuilder(args);

var manifestPath = builder.Configuration["LAYEREDCHAT_MANIFEST_PATH"]
                   ?? Environment.GetEnvironmentVariable("LAYEREDCHAT_MANIFEST_PATH");
if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
{
    throw new InvalidOperationException(
        "Set LAYEREDCHAT_MANIFEST_PATH to a JSON file containing OrchestrationProfileManifest.");
}

var manifestJson = await File.ReadAllTextAsync(manifestPath);
var manifest = OrchestrationProfileManifestJson.Deserialize(manifestJson);
var definition = new OrchestrationDefinition { Manifest = manifest };

var registry = new InMemoryOrchestrationDefinitionRegistry();
registry.Register(definition);

var baseUrl = builder.Configuration["OPENAI_COMPATIBLE_BASE_URL"]
              ?? Environment.GetEnvironmentVariable("OPENAI_COMPATIBLE_BASE_URL")
              ?? "https://api.openai.com/v1/";
var model = builder.Configuration["OPENAI_COMPATIBLE_MODEL"]
            ?? Environment.GetEnvironmentVariable("OPENAI_COMPATIBLE_MODEL")
            ?? "gpt-4o-mini";
var apiKey = builder.Configuration["OPENAI_API_KEY"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";

builder.Services.AddSingleton(registry);
builder.Services.AddSingleton<IDataSourceRegistry>(_ => new DataSourceRegistry(Array.Empty<IDataSourceProvider>()));
builder.Services.AddSingleton<IToolCatalog>(_ => new DictionaryToolCatalog(Array.Empty<ToolDefinition>()));
builder.Services.AddSingleton<IToolExecutor, NoopToolExecutor>();
builder.Services.AddHttpClient("openai", client => { client.Timeout = TimeSpan.FromMinutes(10); });
builder.Services.AddSingleton(sp =>
{
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("openai");
    return new OpenAiCompatibleChatConnector(
        http,
        new OpenAiCompatibleOptions
        {
            BaseUri = new Uri(baseUrl, UriKind.Absolute),
            Model = model,
            ApiKey = string.IsNullOrEmpty(apiKey) ? null : apiKey
        });
});
builder.Services.AddSingleton<ILlmChatConnector>(sp => sp.GetRequiredService<OpenAiCompatibleChatConnector>());
builder.Services.AddSingleton<LayeredChatOrchestrator>();

var app = builder.Build();

var includeExampleMarkdown = builder.Configuration.GetValue("LAYEREDCHAT_INCLUDE_EXAMPLE_MARKDOWN", false)
    || string.Equals(
        Environment.GetEnvironmentVariable("LAYEREDCHAT_INCLUDE_EXAMPLE_MARKDOWN"),
        "1",
        StringComparison.OrdinalIgnoreCase)
    || string.Equals(
        Environment.GetEnvironmentVariable("LAYEREDCHAT_INCLUDE_EXAMPLE_MARKDOWN"),
        "true",
        StringComparison.OrdinalIgnoreCase);

app.MapPost("/v1/orchestration/forward", async (
    OrchestrationForwardPayload body,
    LayeredChatOrchestrator orchestrator,
    CancellationToken ct) =>
{
    var request = OrchestrationForwardMapping.FromDto(body.Request);
    if (includeExampleMarkdown)
    {
        request = await ExampleMarkdownComposer.MergeBundledExampleAsync(request, ct).ConfigureAwait(false);
    }

    var result = await orchestrator.RunTurnAsync(request, ct);
    return Results.Json(
        OrchestrationTurnResultMapping.ToDto(result),
        OrchestrationProfileManifestJson.SerializerOptions);
});

app.MapGet("/health", () => Results.Ok(new { status = "ok", registryKey = definition.RegistryKey }));

app.Run();

internal sealed class NoopToolExecutor : IToolExecutor
{
    public Task<ToolExecutionResult> ExecuteAsync(
        string toolName,
        string argumentsJson,
        OrchestrationSessionContext session,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ToolExecutionResult
        {
            Success = false,
            SummaryText = $"Tool '{toolName}' is not configured on this version host."
        });
    }
}
