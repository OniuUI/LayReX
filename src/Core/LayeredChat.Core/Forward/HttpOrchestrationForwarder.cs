using System.Net.Http.Json;
using System.Text.Json;

namespace LayeredChat;

/// <summary>
/// Default JSON forwarder for remote orchestration pods using <see cref="OrchestrationForwardPayload"/> and <see cref="LayeredChatTurnResultDto"/>.
/// </summary>
public sealed class HttpOrchestrationForwarder : IHttpOrchestrationForwarder
{
    private readonly HttpClient _httpClient;

    public HttpOrchestrationForwarder(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<LayeredChatTurnResult?> TryForwardTurnAsync(
        LayeredChatTurnRequest request,
        OrchestrationProfileManifest manifest,
        Uri endpoint,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(endpoint);

        var payload = new OrchestrationForwardPayload
        {
            Request = OrchestrationForwardMapping.ToDto(request),
            RegistryKey = request.OrchestrationRegistryKey,
            Manifest = manifest
        };

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(timeout);

        using var response = await _httpClient.PostAsJsonAsync(
                endpoint,
                payload,
                OrchestrationProfileManifestJson.SerializerOptions,
                linked.Token)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(linked.Token).ConfigureAwait(false);
        var dto = await JsonSerializer
            .DeserializeAsync<LayeredChatTurnResultDto>(stream, OrchestrationProfileManifestJson.SerializerOptions, linked.Token)
            .ConfigureAwait(false);

        return dto is null ? null : OrchestrationTurnResultMapping.FromDto(dto);
    }
}
