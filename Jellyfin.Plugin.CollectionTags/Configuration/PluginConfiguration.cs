using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.CollectionTags.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        UpdateOnLibraryScan = false;
        CollectionsToTag = string.Empty;
        TagAllCollections = false;
        TagPrefix = "#CollectionTags_";
    }

    /// <summary>
    /// Gets or sets a value indicating whether to update on library scan.
    /// </summary>
    public bool UpdateOnLibraryScan { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether all collections should be tagged.
    /// </summary>
    public bool TagAllCollections { get; set; }

    /// <summary>
    /// Gets or sets a value specifying for which collections tags should be created.
    /// </summary>
    public string CollectionsToTag { get; set; }

    /// <summary>
    /// Gets or sets a value indicating the prefix for the tags.
    /// </summary>
    public string TagPrefix { get; set; }
}
