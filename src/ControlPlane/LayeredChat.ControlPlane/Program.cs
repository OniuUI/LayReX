using LayeredChat;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
var dataRoot = builder.Configuration["LAYREX_DATA_ROOT"]
               ?? Environment.GetEnvironmentVariable("LAYREX_DATA_ROOT")
               ?? Path.Combine(Environment.CurrentDirectory, "layrex-data");
Directory.CreateDirectory(dataRoot);
var app = builder.Build();

app.MapOpenApi();

app.MapGet("/health", () => Results.Ok(new { status = "ok", dataRoot }));

app.MapGet("/v1/layers", () =>
{
    var layersDir = Path.Combine(dataRoot, "layers");
    if (!Directory.Exists(layersDir))
    {
        return Results.Ok(new { layerIds = Array.Empty<string>() });
    }

    var ids = Directory.GetDirectories(layersDir).Select(static d => Path.GetFileName(d)).Where(static s => !string.IsNullOrEmpty(s)).ToArray();
    return Results.Ok(new { layerIds = ids });
});

app.MapGet("/v1/layers/{layerId}/versions", (string layerId) =>
{
    var layerDir = Path.Combine(dataRoot, "layers", layerId);
    if (!Directory.Exists(layerDir))
    {
        return Results.NotFound();
    }

    var versions = Directory.GetDirectories(layerDir).Select(static d => Path.GetFileName(d)).Where(static s => !string.IsNullOrEmpty(s)).ToArray();
    return Results.Ok(new { versions });
});

app.MapMethods("/v1/layers/{layerId}/{version}", new[] { "PUT", "POST" }, async (
    string layerId,
    string version,
    HttpRequest request,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(layerId) || string.IsNullOrWhiteSpace(version))
    {
        return Results.BadRequest("layerId and version are required.");
    }

    foreach (var c in Path.GetInvalidFileNameChars())
    {
        if (layerId.Contains(c, StringComparison.Ordinal) || version.Contains(c, StringComparison.Ordinal))
        {
            return Results.BadRequest("Invalid layerId or version character.");
        }
    }

    using var reader = new StreamReader(request.Body);
    var json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    if (string.IsNullOrWhiteSpace(json))
    {
        return Results.BadRequest("Body must contain layer JSON.");
    }

    try
    {
        _ = LayerContributionJson.Deserialize(json);
    }
    catch (Exception ex)
    {
        return Results.BadRequest($"Invalid layer JSON: {ex.Message}");
    }

    var targetDir = Path.Combine(dataRoot, "layers", layerId.Trim(), version.Trim());
    Directory.CreateDirectory(targetDir);
    var path = Path.Combine(targetDir, "layer.json");
    await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
    return Results.Ok(new { stored = path });
});

app.MapGet("/v1/layers/{layerId}/{version}/layer.json", async (
    string layerId,
    string version,
    CancellationToken cancellationToken) =>
{
    var path = Path.Combine(dataRoot, "layers", layerId, version, "layer.json");
    if (!File.Exists(path))
    {
        return Results.NotFound();
    }

    var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
    return Results.Text(json, "application/json");
});

app.Run();
