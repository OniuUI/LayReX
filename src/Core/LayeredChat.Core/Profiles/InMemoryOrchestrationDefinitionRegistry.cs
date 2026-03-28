namespace LayeredChat;

/// <summary>
/// In-memory registry suitable for tests and single-node hosts. Database-backed hosts implement <see cref="IOrchestrationDefinitionRegistry"/> separately.
/// </summary>
public sealed class InMemoryOrchestrationDefinitionRegistry : IOrchestrationDefinitionRegistry
{
    private readonly Dictionary<string, OrchestrationDefinition> _definitions =
        new(StringComparer.OrdinalIgnoreCase);

    public void Register(OrchestrationDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _definitions[definition.RegistryKey] = definition;
    }

    public bool TryGet(string registryKey, out OrchestrationDefinition? definition)
    {
        return _definitions.TryGetValue(registryKey, out definition);
    }

    public IReadOnlyCollection<OrchestrationDefinition> List() => _definitions.Values.ToList();
}
