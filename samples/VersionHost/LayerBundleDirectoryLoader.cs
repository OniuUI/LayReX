namespace LayeredChat.VersionHost;

/// <summary>
/// Loads <see cref="LayerContribution"/> instances from a bundle directory (see docs/LAYER_PACKAGE_FORMAT.md).
/// </summary>
public static class LayerBundleDirectoryLoader
{
    public static async Task<IReadOnlyList<LayerContribution>> LoadContributionsAsync(
        string bundleRoot,
        LayerStackManifest stack,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleRoot);
        ArgumentNullException.ThrowIfNull(stack);
        if (!Directory.Exists(bundleRoot))
        {
            throw new DirectoryNotFoundException($"Layer bundle root not found: {bundleRoot}");
        }

        var list = new List<LayerContribution>();
        foreach (var entry in stack.Layers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(entry.LayerId) || string.IsNullOrWhiteSpace(entry.Version))
            {
                throw new InvalidOperationException("Layer stack entry requires layerId and version.");
            }

            var layerDir = Path.Combine(bundleRoot, "layers", entry.LayerId.Trim(), entry.Version.Trim());
            var layerJsonPath = Path.Combine(layerDir, "layer.json");
            if (!File.Exists(layerJsonPath))
            {
                throw new FileNotFoundException($"Missing layer.json for {entry.LayerId}@{entry.Version}.", layerJsonPath);
            }

            var json = await File.ReadAllTextAsync(layerJsonPath, cancellationToken).ConfigureAwait(false);
            var contribution = LayerContributionJson.Deserialize(json);
            var fragment = contribution.InstructionFragment ?? string.Empty;
            foreach (var rel in contribution.InstructionMarkdownFiles)
            {
                if (string.IsNullOrWhiteSpace(rel))
                {
                    continue;
                }

                var mdPath = Path.Combine(layerDir, rel.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(mdPath))
                {
                    throw new FileNotFoundException($"Instruction file not found for layer {entry.LayerId}@{entry.Version}.", mdPath);
                }

                var md = await File.ReadAllTextAsync(mdPath, cancellationToken).ConfigureAwait(false);
                if (fragment.Length > 0)
                {
                    fragment += "\n\n";
                }

                fragment += md.Trim();
            }

            list.Add(new LayerContribution
            {
                SchemaVersion = contribution.SchemaVersion,
                LayerId = contribution.LayerId,
                SemanticVersion = contribution.SemanticVersion,
                InstructionFragment = string.IsNullOrWhiteSpace(fragment) ? null : fragment,
                AllowedToolNames = contribution.AllowedToolNames,
                DataSourceIdsInOrder = contribution.DataSourceIdsInOrder,
                Parameters = contribution.Parameters,
                MaxToolIterations = contribution.MaxToolIterations,
                DefaultTemperature = contribution.DefaultTemperature,
                OutputCapabilities = contribution.OutputCapabilities
            });
        }

        return list;
    }

    public static async Task<LayerStackManifest> LoadStackManifestAsync(
        string bundleRoot,
        CancellationToken cancellationToken = default)
    {
        var stackPath = Path.Combine(bundleRoot, "stack.json");
        if (!File.Exists(stackPath))
        {
            throw new FileNotFoundException("stack.json not found in bundle root.", stackPath);
        }

        var json = await File.ReadAllTextAsync(stackPath, cancellationToken).ConfigureAwait(false);
        return LayerStackManifestJson.Deserialize(json);
    }
}
