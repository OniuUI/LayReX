using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LayeredChat;

namespace LayReX.ControlPlane.Client;

/// <summary>
/// Minimal client for the LayReX layer registry HTTP API (LayeredChat.ControlPlane host).
/// </summary>
public sealed class LayReXRegistryClient : IDisposable
{
    private static readonly JsonSerializerOptions ListJsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient _http;

    public LayReXRegistryClient(HttpClient http)
    {
        _http = http;
    }

    public Uri? BaseAddress => _http.BaseAddress;

    public async Task PushLayerJsonAsync(
        string layerId,
        string version,
        string layerJson,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(layerJson);
        var enc = Encoding.UTF8;
        using var content = new StringContent(layerJson, enc, "application/json");
        var uri = $"v1/layers/{Uri.EscapeDataString(layerId)}/{Uri.EscapeDataString(version)}";
        var response = await _http.PutAsync(uri, content, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task PushLayerAsync(
        string layerId,
        string version,
        LayerContribution contribution,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contribution);
        var json = LayerContributionJson.Serialize(contribution);
        await PushLayerJsonAsync(layerId, version, json, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> ListLayerIdsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _http.GetAsync("v1/layers", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var doc = await response.Content.ReadFromJsonAsync<LayersResponse>(ListJsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return doc?.LayerIds ?? [];
    }

    public async Task<IReadOnlyList<string>> ListVersionsAsync(string layerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layerId);
        var uri = $"v1/layers/{Uri.EscapeDataString(layerId)}/versions";
        var response = await _http.GetAsync(uri, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var doc = await response.Content.ReadFromJsonAsync<VersionsResponse>(ListJsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return doc?.Versions ?? [];
    }

    public async Task<string> DownloadLayerJsonAsync(
        string layerId,
        string version,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        var uri = $"v1/layers/{Uri.EscapeDataString(layerId)}/{Uri.EscapeDataString(version)}/layer.json";
        return await _http.GetStringAsync(uri, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _http.Dispose();
    }

    private sealed class LayersResponse
    {
        [JsonPropertyName("layerIds")]
        public string[] LayerIds { get; set; } = [];
    }

    private sealed class VersionsResponse
    {
        [JsonPropertyName("versions")]
        public string[] Versions { get; set; } = [];
    }
}
