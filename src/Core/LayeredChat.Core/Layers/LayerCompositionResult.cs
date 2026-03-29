namespace LayeredChat;

/// <summary>
/// Outcome of merging a baseline manifest with ordered <see cref="LayerContribution"/> instances.
/// </summary>
public sealed class LayerCompositionResult
{
    public OrchestrationProfileManifest EffectiveManifest { get; init; } = new();

    public IReadOnlyList<string> InstructionFragments { get; init; } = [];

    /// <summary>
    /// Joins non-empty <see cref="InstructionFragments"/> for use as a system-instruction prefix or appendix.
    /// </summary>
    public string JoinInstructionFragments(string delimiter)
    {
        ArgumentNullException.ThrowIfNull(delimiter);
        var parts = InstructionFragments.Where(static s => !string.IsNullOrWhiteSpace(s)).ToList();
        return parts.Count == 0 ? string.Empty : string.Join(delimiter, parts);
    }
}
