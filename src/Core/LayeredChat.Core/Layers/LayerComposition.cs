namespace LayeredChat;

/// <summary>
/// Static composition of baseline manifests and layer contributions (pure, no I/O).
/// </summary>
public static class LayerComposition
{
    public static LayerCompositionResult Compose(
        OrchestrationProfileManifest baseline,
        IReadOnlyList<LayerContribution> contributions)
    {
        var tools = new List<string>();
        foreach (var t in baseline.AllowedToolNames)
        {
            AddDistinct(tools, t);
        }

        var dataSources = new List<string>();
        foreach (var d in baseline.DataSourceIdsInOrder)
        {
            AddDistinct(dataSources, d);
        }

        var parameters = new Dictionary<string, string>(baseline.Parameters, StringComparer.OrdinalIgnoreCase);
        var maxIter = baseline.MaxToolIterations;
        double temperature = baseline.DefaultTemperature;
        var caps = baseline.OutputCapabilities;
        var fragments = new List<string>();

        foreach (var layer in contributions)
        {
            foreach (var t in layer.AllowedToolNames)
            {
                AddDistinct(tools, t);
            }

            foreach (var d in layer.DataSourceIdsInOrder)
            {
                AddDistinct(dataSources, d);
            }

            foreach (var kv in layer.Parameters)
            {
                parameters[kv.Key] = kv.Value;
            }

            if (layer.MaxToolIterations is { } mi)
            {
                maxIter = Math.Max(maxIter, mi);
            }

            if (layer.DefaultTemperature is { } temp)
            {
                temperature = temp;
            }

            if (layer.OutputCapabilities is { } oc)
            {
                caps |= oc;
            }

            if (!string.IsNullOrWhiteSpace(layer.InstructionFragment))
            {
                fragments.Add(layer.InstructionFragment.Trim());
            }
        }

        var effective = new OrchestrationProfileManifest
        {
            SchemaVersion = baseline.SchemaVersion,
            OrchestrationId = baseline.OrchestrationId,
            SemanticVersion = baseline.SemanticVersion,
            ProfileVersion = baseline.ProfileVersion,
            DisplayName = baseline.DisplayName,
            Description = baseline.Description,
            AllowedToolNames = tools,
            DataSourceIdsInOrder = dataSources,
            OutputCapabilities = caps,
            MaxToolIterations = maxIter,
            DefaultTemperature = temperature,
            Parameters = parameters,
            ExternalForwardUri = baseline.ExternalForwardUri,
            ExternalForwardTimeoutSeconds = baseline.ExternalForwardTimeoutSeconds,
            LlmAdapterProfileId = baseline.LlmAdapterProfileId,
            PreferredConnectorKind = baseline.PreferredConnectorKind,
            LayerStack = null
        };

        return new LayerCompositionResult
        {
            EffectiveManifest = effective,
            InstructionFragments = fragments
        };
    }

    private static void AddDistinct(List<string> list, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        foreach (var existing in list)
        {
            if (string.Equals(existing, value, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        list.Add(value);
    }
}
