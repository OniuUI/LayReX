namespace LayeredChat;

/// <summary>
/// Points at one published layer by id and exact semantic version. Range resolution is a host or registry concern.
/// </summary>
public sealed class LayerReferenceEntry
{
    public string LayerId { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;
}
