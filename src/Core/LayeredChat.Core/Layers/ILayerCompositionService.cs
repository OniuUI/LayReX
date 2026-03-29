namespace LayeredChat;

/// <summary>
/// Merges resolved layer contributions into an effective <see cref="OrchestrationProfileManifest"/>.
/// </summary>
public interface ILayerCompositionService
{
    /// <summary>
    /// When <paramref name="contributions"/> is empty, returns a copy of <paramref name="baseline"/> with <see cref="OrchestrationProfileManifest.LayerStack"/> cleared.
    /// </summary>
    LayerCompositionResult Compose(
        OrchestrationProfileManifest baseline,
        IReadOnlyList<LayerContribution> contributions);
}
