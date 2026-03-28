namespace LayeredChat;

/// <summary>
/// Host-supplied tool definitions keyed by name. The orchestrator intersects with <see cref="OrchestrationProfileManifest.AllowedToolNames"/>.
/// </summary>
public interface IToolCatalog
{
    bool TryGet(string name, out ToolDefinition? definition);

    IReadOnlyList<ToolDefinition> ResolveAllowed(IReadOnlyList<string> allowedNames);
}
