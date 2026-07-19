namespace LayeredChat;

/// <summary>
/// Per-tool-call lifecycle hooks invoked by the orchestrator around every tool execution.
/// All hooks are optional; exceptions thrown from hooks fail the tool call, not the turn.
/// </summary>
public sealed class ToolLifecycleHooks
{
    /// <summary>Runs before execution; can allow, deny (with a reason surfaced to the model), or rewrite arguments.</summary>
    public Func<PreToolUseContext, CancellationToken, ValueTask<PreToolUseDecision>>? PreToolUse { get; init; }

    /// <summary>Runs after a successful execution.</summary>
    public Func<PostToolUseContext, CancellationToken, ValueTask>? PostToolUse { get; init; }

    /// <summary>Runs after a failed execution (including caught executor exceptions and denied calls).</summary>
    public Func<PostToolUseFailureContext, CancellationToken, ValueTask>? PostToolUseFailure { get; init; }

    /// <summary>Adapts an <see cref="IToolPermissionGate"/> into a <see cref="PreToolUse"/> hook.</summary>
    public static ToolLifecycleHooks ForPermissionGate(IToolPermissionGate gate)
    {
        ArgumentNullException.ThrowIfNull(gate);
        return new ToolLifecycleHooks
        {
            PreToolUse = gate.EvaluateAsync
        };
    }
}

/// <summary>Context passed to <see cref="ToolLifecycleHooks.PreToolUse"/>.</summary>
public sealed class PreToolUseContext
{
    public required ToolCallRequest Call { get; init; }

    public required string RegistryKey { get; init; }

    public required OrchestrationSessionContext Session { get; init; }

    public int RoundIndex { get; init; }
}

/// <summary>Context passed to <see cref="ToolLifecycleHooks.PostToolUse"/>.</summary>
public sealed class PostToolUseContext
{
    public required ToolCallRequest Call { get; init; }

    public required ToolExecutionResult Result { get; init; }

    public required string RegistryKey { get; init; }

    public required OrchestrationSessionContext Session { get; init; }
}

/// <summary>Context passed to <see cref="ToolLifecycleHooks.PostToolUseFailure"/>.</summary>
public sealed class PostToolUseFailureContext
{
    public required ToolCallRequest Call { get; init; }

    public required ToolExecutionResult Result { get; init; }

    public required string RegistryKey { get; init; }

    public required OrchestrationSessionContext Session { get; init; }

    /// <summary>Set when the failure came from a caught executor exception.</summary>
    public Exception? Exception { get; init; }
}

/// <summary>Outcome of a <see cref="ToolLifecycleHooks.PreToolUse"/> evaluation.</summary>
public sealed class PreToolUseDecision
{
    public PreToolUseAction Action { get; init; } = PreToolUseAction.Allow;

    public string? DeniedReason { get; init; }

    public string? RewrittenArgumentsJson { get; init; }

    public static PreToolUseDecision Allow() => new();

    public static PreToolUseDecision Deny(string reason) => new()
    {
        Action = PreToolUseAction.Deny,
        DeniedReason = reason
    };

    public static PreToolUseDecision Rewrite(string argumentsJson) => new()
    {
        Action = PreToolUseAction.RewriteArguments,
        RewrittenArgumentsJson = argumentsJson
    };
}

public enum PreToolUseAction
{
    Allow,
    Deny,
    RewriteArguments
}
