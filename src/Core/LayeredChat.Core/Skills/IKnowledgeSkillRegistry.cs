namespace LayeredChat;

/// <summary>
/// A versioned knowledge package: a one-line description always visible to the model and a Markdown body
/// loaded on demand via the <c>load_skill</c> tool (progressive disclosure).
/// </summary>
public sealed class KnowledgeSkillDefinition
{
    public required string Name { get; init; }

    /// <summary>One-line trigger-phrased description rendered into the skill index.</summary>
    public required string Description { get; init; }

    public required string BodyMarkdown { get; init; }

    /// <summary>Agent names this skill is scoped to; empty means available to all agents.</summary>
    public IReadOnlyList<string> Agents { get; init; } = [];
}

/// <summary>
/// Host-facing registry of knowledge skills with index rendering for system prompts.
/// </summary>
public interface IKnowledgeSkillRegistry
{
    /// <summary>Skills visible to <paramref name="agentName"/> (null or empty = unscoped view of all skills).</summary>
    IReadOnlyList<KnowledgeSkillDefinition> ListFor(string? agentName);

    KnowledgeSkillDefinition? Get(string name);

    /// <summary>Renders the name+description index block for an agent's stable system prompt.</summary>
    string RenderIndexFor(string? agentName);
}
