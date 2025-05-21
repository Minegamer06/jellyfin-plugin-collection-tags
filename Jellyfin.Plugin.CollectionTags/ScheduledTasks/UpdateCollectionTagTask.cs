using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CollectionTags.LibaryExamples;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CollectionTags.ScheduledTasks
{
    /// <inheritdoc/>
    public class UpdateCollectionTagTask : IScheduledTask
    {
        private readonly ILogger<UpdateCollectionTagTask> _logger;
        private readonly LibaryInfo _libaryInfo;

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateCollectionTagTask"/> class.
        /// </summary>
        /// <param name="logger">Instance of the <see cref="ILogger{ExampleScheduledTask}"/> interface.</param>
        /// <param name="libaryInfo">Instance of the <see cref="LibaryInfo"/> interface.</param>
        public UpdateCollectionTagTask(ILogger<UpdateCollectionTagTask> logger, LibaryInfo libaryInfo)
        {
            _logger = logger;
            _libaryInfo = libaryInfo;
        }

        /// <inheritdoc/>
        public string Name => "Collection Tag Update Task";

        /// <inheritdoc/>
        public string Key => "CollectionTagUpdateTask";

        /// <inheritdoc/>
        public string Description => "Update die Tags anhand der Collection";

        /// <inheritdoc/>
        public string Category => "Collection Tags";

        /// <inheritdoc/>
        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Task - Start: {Name}", Name);
            await _libaryInfo.Run().ConfigureAwait(false);
            _logger.LogInformation("Task - Complete: {Name}", Name);
        }

        /// <inheritdoc/>
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return [new TaskTriggerInfo
                {
                    Type = TaskTriggerInfo.TriggerInterval,
                    IntervalTicks = TimeSpan.FromHours(6).Ticks
                }
                ];
        }
    }
}
