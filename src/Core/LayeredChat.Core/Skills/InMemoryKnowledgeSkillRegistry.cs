using System.Text;

namespace LayeredChat;

/// <summary>
/// In-memory <see cref="IKnowledgeSkillRegistry"/>. Hosts typically populate this at startup from bundles or a database.
/// </summary>
public sealed class InMemoryKnowledgeSkillRegistry : IKnowledgeSkillRegistry
{
    private readonly Dictionary<string, KnowledgeSkillDefinition> _skills;
    private readonly List<KnowledgeSkillDefinition> _ordered;

    public InMemoryKnowledgeSkillRegistry(IEnumerable<KnowledgeSkillDefinition> skills)
    {
        ArgumentNullException.ThrowIfNull(skills);
        _ordered = skills.ToList();
        _skills = new Dictionary<string, KnowledgeSkillDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var skill in _ordered)
        {
            if (string.IsNullOrWhiteSpace(skill.Name))
            {
                throw new ArgumentException("Skill name is required.", nameof(skills));
            }

            if (!_skills.TryAdd(skill.Name, skill))
            {
                throw new ArgumentException($"Duplicate skill name '{skill.Name}'.", nameof(skills));
            }
        }
    }

    public IReadOnlyList<KnowledgeSkillDefinition> ListFor(string? agentName)
    {
        if (string.IsNullOrWhiteSpace(agentName))
        {
            return _ordered;
        }

        return _ordered
            .Where(s => s.Agents.Count == 0 ||
                        s.Agents.Contains(agentName, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    public KnowledgeSkillDefinition? Get(string name)
    {
        return _skills.TryGetValue(name, out var skill) ? skill : null;
    }

    public string RenderIndexFor(string? agentName)
    {
        var visible = ListFor(agentName);
        if (visible.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.AppendLine("## Available skills");
        sb.AppendLine("Call load_skill(skill) BEFORE relying on any knowledge described below.");
        foreach (var skill in visible)
        {
            sb.Append("- ").Append(skill.Name).Append(": ").AppendLine(skill.Description);
        }

        return sb.ToString().TrimEnd();
    }
}
