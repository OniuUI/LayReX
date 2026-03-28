namespace LayeredChat;

/// <summary>
/// Delegates an entire turn to a remote orchestration pod or control plane when <see cref="OrchestrationProfileManifest.ExternalForwardUri"/> is set.
/// </summary>
public interface IHttpOrchestrationForwarder
{
    Task<LayeredChatTurnResult?> TryForwardTurnAsync(
        LayeredChatTurnRequest request,
        OrchestrationProfileManifest manifest,
        Uri endpoint,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
