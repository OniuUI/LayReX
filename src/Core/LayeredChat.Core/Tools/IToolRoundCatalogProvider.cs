namespace LayeredChat;

/// <summary>
/// Lets a host change which tools are exposed to the model on each tool round (within the manifest allow-list),
/// for example starting with a narrow catalog and expanding after validation failures.
/// </summary>
public interface IToolRoundCatalogProvider
{
    /// <summary>
    /// Returns tool names to send to the model for this round. When null or empty, the orchestrator uses
    /// <see cref="OrchestrationProfileManifest.AllowedToolNames"/> unchanged.
    /// Every returned name must appear in that allow-list.
    /// </summary>
    IReadOnlyList<string>? GetActiveToolNamesForRound(
        int roundIndex,
        IReadOnlyList<ChatMessage> workingMessages,
        OrchestrationProfileManifest manifest);
}
