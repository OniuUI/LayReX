using System.Collections.Concurrent;
using System.Text.Json;

namespace LayeredChat;

/// <summary>
/// Produces the <c>load_skill</c> tool definition and executor for a skill registry.
/// The manifest allow-list must include the tool name for it to be exposed.
/// </summary>
public static class LoadSkillToolFactory
{
    public const string DefaultToolName = "load_skill";

    public static ToolDefinition CreateToolDefinition(IKnowledgeSkillRegistry registry, string toolName = DefaultToolName)
    {
        ArgumentNullException.ThrowIfNull(registry);
        var names = registry.ListFor(null).Select(s => s.Name).ToList();
        var schema = new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "skill" },
            properties = new
            {
                skill = new
                {
                    type = "string",
                    description = "Name of the skill to load.",
                    @enum = names
                }
            }
        };

        return new ToolDefinition
        {
            Name = toolName,
            Description = "Loads the full body of a named skill into the conversation. " +
                          "Call before relying on knowledge a skill description promises.",
            ParametersSchemaJson = JsonSerializer.Serialize(schema),
            IsReadOnly = true
        };
    }
}

/// <summary>
/// Executes <c>load_skill</c>: returns the skill body once per correlation; repeat loads return a short marker.
/// </summary>
public sealed class LoadSkillToolExecutor : IToolExecutor
{
    private const int MaxTrackedCorrelations = 2048;

    private readonly IKnowledgeSkillRegistry _registry;
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _loadedByCorrelation =
        new(StringComparer.Ordinal);

    public LoadSkillToolExecutor(IKnowledgeSkillRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public Task<ToolExecutionResult> ExecuteAsync(
        string toolName,
        string argumentsJson,
        OrchestrationSessionContext session,
        CancellationToken cancellationToken = default)
    {
        string? skillName = null;
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
            if (doc.RootElement.TryGetProperty("skill", out var s) && s.ValueKind == JsonValueKind.String)
            {
                skillName = s.GetString();
            }
        }
        catch (JsonException)
        {
            // fall through to the invalid-arguments failure below
        }

        if (string.IsNullOrWhiteSpace(skillName))
        {
            return Task.FromResult(new ToolExecutionResult
            {
                Success = false,
                FailureKind = ToolFailureKind.Invalid,
                SummaryText = "load_skill requires a 'skill' argument with the skill name."
            });
        }

        var skill = _registry.Get(skillName);
        if (skill is null)
        {
            var available = string.Join(", ", _registry.ListFor(null).Select(s => s.Name));
            return Task.FromResult(new ToolExecutionResult
            {
                Success = false,
                FailureKind = ToolFailureKind.Invalid,
                SummaryText = $"Unknown skill '{skillName}'. Available skills: {available}."
            });
        }

        if (_loadedByCorrelation.Count > MaxTrackedCorrelations)
        {
            _loadedByCorrelation.Clear();
        }

        var loaded = _loadedByCorrelation.GetOrAdd(
            session.CorrelationId,
            static _ => new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase));

        if (!loaded.TryAdd(skill.Name, 0))
        {
            return Task.FromResult(new ToolExecutionResult
            {
                Success = true,
                SummaryText = $"Skill '{skill.Name}' is already loaded in this turn. Re-read it from the transcript above."
            });
        }

        return Task.FromResult(new ToolExecutionResult
        {
            Success = true,
            SummaryText = $"# Skill: {skill.Name}\n\n{skill.BodyMarkdown}"
        });
    }
}
