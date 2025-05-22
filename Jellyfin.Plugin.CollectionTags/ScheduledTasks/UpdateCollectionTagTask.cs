using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CollectionTags.ScheduledTasks
{
    /// <inheritdoc/>
    public class UpdateCollectionTagTask : IScheduledTask
    {
        private readonly ILogger<UpdateCollectionTagTask> _logger;
        private readonly ILibraryManager _libraryManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateCollectionTagTask"/> class.
        /// </summary>
        /// <param name="logger">Instance of the <see cref="ILogger{UpdateCollectionTagTask}"/> interface.</param>
        /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
        public UpdateCollectionTagTask(ILogger<UpdateCollectionTagTask> logger, ILibraryManager libraryManager)
        {
            _logger = logger;
            _libraryManager = libraryManager;
        }

        /// <inheritdoc/>
        public string Name => "Collection Tag Update Task";

        /// <inheritdoc/>
        public string Key => "CollectionTagUpdateTask";

        /// <inheritdoc/>
        public string Description => "Updates item tags based on their collections, using a defined prefix.";

        /// <inheritdoc/>
        public string Category => "Collection Tags";

        /// <inheritdoc/>
        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Task - Start: {Name}", Name);
            string? prefix = Plugin.Instance?.Configuration.TagPrefix;

            if (string.IsNullOrWhiteSpace(prefix))
            {
                _logger.LogWarning("TagPrefix is not configured or is empty in the plugin settings. Skipping task. Please configure it to enable tagging.");
                progress.Report(100);
                _logger.LogInformation("Task - Complete (skipped due to missing prefix): {Name}", Name);
                return;
            }

            _logger.LogInformation("Using TagPrefix: '{Prefix}'", prefix);

            progress.Report(1.0); // Initial small progress

            _logger.LogInformation("Fetching all relevant items from the library...");
            List<BaseItem> allItems = GetAllItems();
            if (allItems.Count == 0)
            {
                _logger.LogInformation("No items found in the library to process.");
                progress.Report(100);
                _logger.LogInformation("Task - Complete (no items found): {Name}", Name);
                return;
            }

            _logger.LogInformation("Found {Count} total items to check for tag updates.", allItems.Count);
            progress.Report(5.0); // Progress after GetAllItems

            _logger.LogInformation("Identifying items within target collections...");

            // itemsInTargetCollectionsMap: Dictionary<BoxSet, List<BaseItem>>
            // Contains collections (BoxSet) and their associated items (List<BaseItem>) that are targeted for tagging.
            Dictionary<BoxSet, List<BaseItem>> itemsInTargetCollectionsMap = GetItemsInTargetCollections();
            progress.Report(15.0); // Progress after GetItemsInTargetCollections

            _logger.LogInformation("Determining desired prefixed tags for items based on their collections...");

            // itemDesiredPrefixedTags: Dictionary<BaseItem, HashSet<string>>
            // Maps each item (that is in at least one target collection) to the set of prefixed tags it *should* have.
            // The HashSet uses StringComparer.OrdinalIgnoreCase for managing desired tags.
            Dictionary<BaseItem, HashSet<string>> itemDesiredPrefixedTags = GetItemDesiredPrefixedTags(itemsInTargetCollectionsMap, prefix);
            progress.Report(25.0); // Progress after GetItemDesiredPrefixedTags

            int totalItemsToProcess = allItems.Count;
            int processedCount = 0;
            int itemsUpdatedCount = 0;

            _logger.LogInformation("Processing {Count} items for tag updates...", totalItemsToProcess);

            foreach (var item in allItems)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Retrieve the set of prefixed tags this item *should* have based on its collection memberships.
                // If item is not in any *target* collection, desiredPrefixedTagsForItem will be an empty set.
                HashSet<string> desiredPrefixedTagsForItem = itemDesiredPrefixedTags.TryGetValue(item, out var desiredTags)
                    ? desiredTags
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase); // Ensure comparer consistency

                List<string> currentTags = item.Tags?.ToList() ?? new List<string>(); // Handle cases where item.Tags might be null.

                // Separate current tags into those that are managed by this plugin (prefixed) and those that are not.
                List<string> currentNonPrefixedTags = currentTags.Where(t => !t.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
                List<string> currentPrefixedTagsOnItem = currentTags.Where(t => t.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();

                // Determine if the item's prefixed tags match the desired state.
                // This checks if the set of current prefixed tags is identical to the set of desired prefixed tags.
                // Order doesn't matter due to HashSet comparison.
                bool needsUpdate = !new HashSet<string>(currentPrefixedTagsOnItem, StringComparer.OrdinalIgnoreCase).SetEquals(desiredPrefixedTagsForItem);

                if (needsUpdate)
                {
                    // Reconstruct the item's tags: start with non-prefixed tags, then add all desired prefixed tags.
                    List<string> newTagList = new List<string>(currentNonPrefixedTags);
                    newTagList.AddRange(desiredPrefixedTagsForItem); // Add all desired (new or existing) prefixed tags.

                    // Store the new list of tags, ensuring no duplicates (case-insensitive for distinctness).
                    item.Tags = newTagList.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                    itemsUpdatedCount++;

                    // Log specific tags added or removed for better traceability (optional, can be verbose)
                    var addedTags = desiredPrefixedTagsForItem.Except(currentPrefixedTagsOnItem, StringComparer.OrdinalIgnoreCase).ToList();
                    var removedTags = currentPrefixedTagsOnItem.Except(desiredPrefixedTagsForItem, StringComparer.OrdinalIgnoreCase).ToList();

                    try
                    {
                        await _libraryManager.UpdateItemAsync(item, item.GetParent(), ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
                        _logger.LogInformation(
                            "Updated tags for item: '{ItemName}' (ID: {ItemId}). New tags: [{NewTags}]. Added: [{AddedTags}], Removed: [{RemovedTags}]",
                            item.Name,
                            item.Id,
                            string.Join(", ", item.Tags),
                            string.Join(", ", addedTags),
                            string.Join(", ", removedTags));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to update item '{ItemName}' (ID: {ItemId}) in repository.", item.Name, item.Id);
                    }
                }

                processedCount++;
                if (totalItemsToProcess <= 0)
                {
                    continue;
                }

                // Calculate progress for the loop, scaling it from 25% to 95% of total task progress.
                double loopProgress = (double)processedCount / totalItemsToProcess * 70.0;
                progress.Report(25.0 + loopProgress);
            }

            _logger.LogInformation("Tag update processing complete. {UpdatedCount} items had their tags modified out of {TotalCount} items checked.", itemsUpdatedCount, totalItemsToProcess);
            progress.Report(100); // Final progress report.
            _logger.LogInformation("Task - Complete: {Name}", Name);
        }

        /// <summary>
        /// Gets all collections that should be processed and the items within them.
        /// </summary>
        /// <returns>A dictionary where keys are BoxSet collections and values are lists of BaseItems in those collections.</returns>
        private Dictionary<BoxSet, List<BaseItem>> GetItemsInTargetCollections()
        {
            _logger.LogDebug("Fetching items in target collections...");
            bool tagAllCollections = Plugin.Instance?.Configuration.TagAllCollections ?? false;
            string[] configuredCollectionNamesToTag = Plugin.Instance?.Configuration.CollectionsToTag?
                .Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

            _logger.LogInformation("TagAllCollections setting: {TagAll}", tagAllCollections);
            if (!tagAllCollections)
            {
                _logger.LogInformation("Targeted collections by name: [{Collections}]", string.Join(", ", configuredCollectionNamesToTag));
            }

            // Fetch all BoxSets from the library.
            List<BoxSet> allBoxSetsInLibrary = _libraryManager.GetItemList(
                    new InternalItemsQuery() { IncludeItemTypes = [BaseItemKind.BoxSet] })
                .OfType<BoxSet>() // Safely cast to BoxSet and filter out nulls or incorrect types.
                .ToList();

            // Filter these BoxSets based on plugin configuration.
            List<BoxSet> collectionsToProcess = allBoxSetsInLibrary
                .Where(collection => collection != null &&
                                     (tagAllCollections ||
                                      (collection.Name != null &&
                                       configuredCollectionNamesToTag.Contains(collection.Name.Trim(), StringComparer.OrdinalIgnoreCase))))
                .ToList();

            _logger.LogInformation("Found {Count} collections to process based on configuration.", collectionsToProcess.Count);

            Dictionary<BoxSet, List<BaseItem>> itemsByCollection = new();
            foreach (BoxSet collection in collectionsToProcess)
            {
                // GetRecursiveChildren() finds all items considered part of this BoxSet, potentially including items in sub-folders if applicable.
                List<BaseItem> itemsInCollection = collection.GetRecursiveChildren().ToList();
                if (itemsInCollection.Count == 0)
                {
                    itemsByCollection.Add(collection, itemsInCollection);
                    _logger.LogDebug("Collection '{CollectionName}' (ID: {CollectionId}) contains {ItemCount} items.", collection.Name, collection.Id, itemsInCollection.Count);
                }
                else
                {
                    _logger.LogDebug("Collection '{CollectionName}' (ID: {CollectionId}) contains no items.", collection.Name, collection.Id);
                }
            }

            _logger.LogDebug("Finished fetching items in target collections. Found {Count} collections with items to tag.", itemsByCollection.Count);
            return itemsByCollection;
        }

        /// <summary>
        /// Calculates the desired set of prefixed tags for each item based on the collections it belongs to.
        /// </summary>
        /// <param name="itemsInCollections">A dictionary mapping collections to their items.</param>
        /// <param name="prefix">The tag prefix to use.</param>
        /// <returns>A dictionary where keys are BaseItems and values are HashSets of desired prefixed tag strings for that item.</returns>
        private Dictionary<BaseItem, HashSet<string>> GetItemDesiredPrefixedTags(Dictionary<BoxSet, List<BaseItem>> itemsInCollections, string prefix)
        {
            _logger.LogDebug("Calculating desired prefixed tags for items using prefix '{Prefix}'...", prefix);
            Dictionary<BaseItem, HashSet<string>> itemDesiredTags = new();

            foreach (var collectionEntry in itemsInCollections)
            {
                BoxSet collection = collectionEntry.Key;
                List<BaseItem> itemsInThisCollection = collectionEntry.Value;

                string collectionName = collection.Name?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(collectionName))
                {
                    _logger.LogWarning("Collection with ID {CollectionId} has an empty or null name. Cannot generate tag for it. Skipping.", collection.Id);
                    continue;
                }

                // Construct the tag value. Casing is preserved from prefix and collectionName.
                string prefixedCollectionTag = prefix + collectionName;
                _logger.LogDebug("Desired tag for items in collection '{CollectionName}' (ID: {CollectionId}) will be '{Tag}'.", collectionName, collection.Id, prefixedCollectionTag);

                foreach (var item in itemsInThisCollection)
                {
                    if (item == null)
                    {
                        continue; // Safety check
                    }

                    if (!itemDesiredTags.TryGetValue(item, out var tagsForItemSet))
                    {
                        // Using OrdinalIgnoreCase for the HashSet ensures that if "prefixMyColl" and "prefixmycoll"
                        // were somehow desired for the same item (e.g., due to near-identical collection names or varied prefix casing),
                        // they are treated as one unique desired tag logically. The actual tag added is `prefixedCollectionTag`.
                        tagsForItemSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        itemDesiredTags.Add(item, tagsForItemSet);
                    }

                    tagsForItemSet.Add(prefixedCollectionTag); // Add the precisely cased tag to the set of desired tags.
                }
            }

            _logger.LogDebug("Finished calculating desired prefixed tags. {Count} items have at least one desired prefixed tag.", itemDesiredTags.Count);
            return itemDesiredTags;
        }

        /// <summary>
        /// Retrieves a list of all items from the library that are candidates for tagging.
        /// </summary>
        /// <returns>A list of BaseItems.</returns>
        private List<BaseItem> GetAllItems()
        {
            return _libraryManager.GetItemList(
                new InternalItemsQuery()
                {
                    IncludeItemTypes = [
                        BaseItemKind.Movie,
                        BaseItemKind.Series,
                        BaseItemKind.Season,
                        BaseItemKind.Episode,
                        BaseItemKind.Video,
                        BaseItemKind.Audio,
                        BaseItemKind.Book
                    ],
                }).ToList();
        }

        /// <inheritdoc/>
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            // Default trigger: run every 6 hours.
            return [new TaskTriggerInfo { Type = TaskTriggerInfo.TriggerInterval, IntervalTicks = TimeSpan.FromHours(6).Ticks }];
        }
    }
}
