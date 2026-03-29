namespace LayeredChat;

/// <summary>
/// Default merge rules: tool and data-source ids are unioned in order (baseline first, then layers); parameters last-wins; max tool iterations takes the maximum; temperature last non-null wins; output capabilities are combined with bitwise OR.
/// </summary>
public sealed class LayerCompositionService : ILayerCompositionService
{
    public LayerCompositionResult Compose(
        OrchestrationProfileManifest baseline,
        IReadOnlyList<LayerContribution> contributions)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(contributions);
        return LayerComposition.Compose(baseline, contributions);
    }
}
