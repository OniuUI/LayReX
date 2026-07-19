namespace LayeredChat;

/// <summary>
/// Host-implemented per-call permission policy (governance, billing, capability gating).
/// Wire into the loop via <see cref="ToolLifecycleHooks.ForPermissionGate"/>.
/// </summary>
public interface IToolPermissionGate
{
    ValueTask<PreToolUseDecision> EvaluateAsync(PreToolUseContext context, CancellationToken cancellationToken);
}
