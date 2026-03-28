namespace LayeredChat;

/// <summary>
/// Resolves orchestration manifests by registry key (<see cref="OrchestrationRegistryKeys.Compose"/>).
/// </summary>
public interface IOrchestrationDefinitionRegistry
{
    void Register(OrchestrationDefinition definition);

    bool TryGet(string registryKey, out OrchestrationDefinition? definition);

    IReadOnlyCollection<OrchestrationDefinition> List();
}
