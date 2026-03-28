namespace LayeredChat;

/// <summary>
/// A registered orchestration profile keyed by <see cref="RegistryKey"/>.
/// </summary>
public sealed class OrchestrationDefinition
{
    public OrchestrationProfileManifest Manifest { get; init; } = new();

    public string RegistryKey => OrchestrationRegistryKeys.Compose(Manifest.OrchestrationId, Manifest.SemanticVersion);
}
