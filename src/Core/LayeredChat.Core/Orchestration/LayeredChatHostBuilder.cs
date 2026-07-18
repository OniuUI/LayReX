namespace LayeredChat;

/// <summary>
/// Fluent builder for a single <see cref="LayeredChatHost"/> so applications can wire the orchestrator without juggling five constructor arguments.
/// </summary>
public sealed class LayeredChatHostBuilder
{
    private ILlmChatConnector? _connector;
    private IToolCatalog? _toolCatalog;
    private IToolExecutor? _toolExecutor;
    private IOrchestrationDefinitionRegistry? _definitionRegistry;
    private IDataSourceRegistry? _dataSourceRegistry;
    private IReadOnlyList<SubAgentDefinition>? _subAgents;
    private SubAgentDispatchOptions? _subAgentOptions;
    private IKnowledgeSkillRegistry? _skills;
    private string _loadSkillToolName = LoadSkillToolFactory.DefaultToolName;

    /// <summary>
    /// Required: model connector (OpenAI-compatible, Extensions.AI, or custom).
    /// </summary>
    public LayeredChatHostBuilder UseConnector(ILlmChatConnector connector)
    {
        _connector = connector ?? throw new ArgumentNullException(nameof(connector));
        return this;
    }

    /// <summary>
    /// Required: where orchestration manifests / registry keys are resolved.
    /// </summary>
    public LayeredChatHostBuilder UseDefinitionRegistry(IOrchestrationDefinitionRegistry registry)
    {
        _definitionRegistry = registry ?? throw new ArgumentNullException(nameof(registry));
        return this;
    }

    /// <summary>
    /// Registers one or more definitions into a new in-memory registry (convenience over constructing <see cref="InMemoryOrchestrationDefinitionRegistry"/> yourself).
    /// </summary>
    public LayeredChatHostBuilder UseDefinitions(params OrchestrationDefinition[] definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        if (definitions.Length == 0)
        {
            throw new ArgumentException("At least one orchestration definition is required.", nameof(definitions));
        }

        var registry = new InMemoryOrchestrationDefinitionRegistry();
        foreach (var definition in definitions)
        {
            registry.Register(definition);
        }

        _definitionRegistry = registry;
        return this;
    }

    /// <summary>
    /// Tool surface exposed to the model. If omitted, an empty catalog and <see cref="NoOpToolExecutor.Instance"/> are used.
    /// </summary>
    public LayeredChatHostBuilder UseTools(IToolCatalog catalog, IToolExecutor executor)
    {
        _toolCatalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _toolExecutor = executor ?? throw new ArgumentNullException(nameof(executor));
        return this;
    }

    /// <summary>
    /// Context slices (SQL, Mongo, Qdrant, custom). If omitted, an empty <see cref="DataSourceRegistry"/> is used.
    /// </summary>
    public LayeredChatHostBuilder UseDataSources(IDataSourceRegistry dataSources)
    {
        _dataSourceRegistry = dataSources ?? throw new ArgumentNullException(nameof(dataSources));
        return this;
    }

    /// <summary>
    /// Registers dispatchable subagents. Adds a <c>dispatch_agent</c> tool to the catalog; manifests that
    /// should expose it must include the tool name in their allow-list.
    /// </summary>
    public LayeredChatHostBuilder UseSubAgents(
        IReadOnlyList<SubAgentDefinition> agents,
        SubAgentDispatchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(agents);
        if (agents.Count == 0)
        {
            throw new ArgumentException("At least one subagent is required.", nameof(agents));
        }

        _subAgents = agents;
        _subAgentOptions = options;
        return this;
    }

    /// <summary>
    /// Registers knowledge skills. Adds a <c>load_skill</c> tool to the catalog; manifests that should
    /// expose it must include the tool name in their allow-list.
    /// </summary>
    public LayeredChatHostBuilder UseSkills(
        IKnowledgeSkillRegistry skills,
        string toolName = LoadSkillToolFactory.DefaultToolName)
    {
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
        if (string.IsNullOrWhiteSpace(toolName))
        {
            throw new ArgumentException("Tool name is required.", nameof(toolName));
        }

        _loadSkillToolName = toolName;
        return this;
    }

    /// <summary>
    /// Builds the host. Throws if connector or definition registry was not configured.
    /// </summary>
    public LayeredChatHost Build()
    {
        if (_connector is null)
        {
            throw new InvalidOperationException("Call UseConnector(ILlmChatConnector) before Build().");
        }

        if (_definitionRegistry is null)
        {
            throw new InvalidOperationException(
                "Call UseDefinitionRegistry(IOrchestrationDefinitionRegistry) or UseDefinitions(...) before Build().");
        }

        _toolCatalog ??= new DictionaryToolCatalog([]);
        _toolExecutor ??= NoOpToolExecutor.Instance;
        _dataSourceRegistry ??= new DataSourceRegistry(Array.Empty<IDataSourceProvider>());

        var catalog = _toolCatalog;
        var executor = _toolExecutor;
        var extraDefinitions = new List<ToolDefinition>();
        var routes = new Dictionary<string, Func<string, OrchestrationSessionContext, CancellationToken, Task<ToolExecutionResult>>>(
            StringComparer.Ordinal);

        SubAgentDispatchToolExecutor? dispatchExecutor = null;
        if (_subAgents is { Count: > 0 })
        {
            var dispatchOptions = _subAgentOptions ?? new SubAgentDispatchOptions();
            dispatchExecutor = new SubAgentDispatchToolExecutor(_subAgents, dispatchOptions);
            extraDefinitions.Add(SubAgentDispatchToolFactory.CreateToolDefinition(
                _subAgents, dispatchOptions.ToolName, dispatchOptions.AllowParallelDispatch));
            var boundDispatch = dispatchExecutor;
            routes[dispatchOptions.ToolName] = (args, session, ct) =>
                boundDispatch.ExecuteAsync(dispatchOptions.ToolName, args, session, ct);
        }

        if (_skills is not null)
        {
            var skillExecutor = new LoadSkillToolExecutor(_skills);
            extraDefinitions.Add(LoadSkillToolFactory.CreateToolDefinition(_skills, _loadSkillToolName));
            var boundSkillName = _loadSkillToolName;
            routes[boundSkillName] = (args, session, ct) =>
                skillExecutor.ExecuteAsync(boundSkillName, args, session, ct);
        }

        if (extraDefinitions.Count > 0)
        {
            catalog = new CompositeToolCatalog([new DictionaryToolCatalog(extraDefinitions), catalog]);
            executor = new DelegatingToolExecutor(routes, executor);
        }

        var orchestrator = new LayeredChatOrchestrator(
            _connector,
            executor,
            catalog,
            _definitionRegistry,
            _dataSourceRegistry);

        if (dispatchExecutor is not null)
        {
            dispatchExecutor.Orchestrator = orchestrator;
        }

        return new LayeredChatHost(orchestrator);
    }
}
