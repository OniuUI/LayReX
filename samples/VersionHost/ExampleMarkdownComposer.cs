namespace LayeredChat.VersionHost;

/// <summary>
/// Loads bundled markdown from <c>Content/example-system-instruction.md</c> and merges it into <see cref="LayeredChat.LayeredChatTurnRequest.SystemInstructionText"/>.
/// </summary>
internal static class ExampleMarkdownComposer
{
    internal static string ExampleFileName => Path.Combine("Content", "example-system-instruction.md");

    internal static string ResolveBundledPath() => Path.Combine(AppContext.BaseDirectory, ExampleFileName);

    internal static async Task<LayeredChat.LayeredChatTurnRequest> MergeBundledExampleAsync(
        LayeredChat.LayeredChatTurnRequest request,
        CancellationToken cancellationToken)
    {
        var path = ResolveBundledPath();
        if (!File.Exists(path))
        {
            return request;
        }

        var markdown = (await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)).Trim();
        if (string.IsNullOrEmpty(markdown))
        {
            return request;
        }

        var merged = string.IsNullOrWhiteSpace(request.SystemInstructionText)
            ? markdown
            : $"{markdown}\n\n---\n\n{request.SystemInstructionText}";

        return new LayeredChat.LayeredChatTurnRequest
        {
            OrchestrationRegistryKey = request.OrchestrationRegistryKey,
            PriorMessages = request.PriorMessages,
            UserMessageContent = request.UserMessageContent,
            SystemInstructionText = merged,
            Session = request.Session,
            ConnectorOptions = request.ConnectorOptions,
            Hooks = request.Hooks,
            ModelAdapterProfile = request.ModelAdapterProfile
        };
    }
}
