using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CollectionTags.ScheduledTasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CollectionTags.LibraryScanTasks
{
    /// <inheritdoc/>
    public class PostLibraryScanCollectionTagUpdateTask : ILibraryPostScanTask
    {
        private readonly ITaskManager _taskManager;
        private readonly ILogger<PostLibraryScanCollectionTagUpdateTask> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="PostLibraryScanCollectionTagUpdateTask"/> class.
        /// </summary>
        /// <param name="logger">Instance of the <see cref="ILogger{ExampleLibaryPostScanTask}"/> interface.</param>
        /// <param name="taskManager">Instance of the <see cref="ITaskManager"/> interface.</param>
        public PostLibraryScanCollectionTagUpdateTask(ILogger<PostLibraryScanCollectionTagUpdateTask> logger, ITaskManager taskManager)
        {
            _logger = logger;
            _taskManager = taskManager;
        }

        /// <inheritdoc/>
        public Task Run(IProgress<double> progress, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Task - Start: {Name}", nameof(PostLibraryScanCollectionTagUpdateTask));
            if (Plugin.Instance?.Configuration.UpdateOnLibraryScan == true)
            {
                _taskManager.Execute<UpdateCollectionTagTask>();
            }

            _logger.LogInformation("Task - Complete: {Name}", nameof(PostLibraryScanCollectionTagUpdateTask));
            return Task.CompletedTask;
        }
    }
}
