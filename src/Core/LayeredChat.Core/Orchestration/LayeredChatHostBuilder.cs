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

        var orchestrator = new LayeredChatOrchestrator(
            _connector,
            _toolExecutor,
            _toolCatalog,
            _definitionRegistry,
            _dataSourceRegistry);

        return new LayeredChatHost(orchestrator);
    }
}
