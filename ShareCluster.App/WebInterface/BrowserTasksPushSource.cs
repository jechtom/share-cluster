using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShareCluster.Network;
using ShareCluster.Synchronization;
using ShareCluster.WebInterface.Models;

namespace ShareCluster.WebInterface
{
    /// <summary>
    /// Pushes long running tasks events to browser.
    /// </summary>
    public class BrowserTasksPushSource : IBrowserPushSource
    {
        private readonly ILogger<BrowserPeersPushSource> _logger;
        private readonly IBrowserPushTarget _pushTarget;
        private readonly LongRunningTasksManager _tasksManager;
        private bool _isAnyConnected;
        private readonly ThrottlingTimer _throttlingTimer;

        public BrowserTasksPushSource(ILogger<BrowserPeersPushSource> logger, IBrowserPushTarget pushTarget, LongRunningTasksManager tasksManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _pushTarget = pushTarget ?? throw new ArgumentNullException(nameof(pushTarget));
            _tasksManager = tasksManager ?? throw new ArgumentNullException(nameof(tasksManager));
            _throttlingTimer = new ThrottlingTimer(
                minimumDelayBetweenExecutions: TimeSpan.FromMilliseconds(1000),
                maximumScheduleDelay: TimeSpan.FromMilliseconds(50),
                (c) => PushAll());
            _tasksManager.TasksChanged += TasksManager_Changed;
        }

        private void TasksManager_Changed(object sender, DictionaryChangedEvent<int, LongRunningTask> e)
        {
            if (_isAnyConnected) _throttlingTimer.Schedule();
        }

        private void PushAll()
        {
            _pushTarget.PushEventToClients(new EventTasksChanged()
            {
                Tasks = _tasksManager.Tasks
                    .OrderBy(t => t.Key)
                    .Select(t => ParseToDto(t.Value))
                    .ToArray()
            });

            // repeat until not all finished
            if (_tasksManager.Tasks.Any(t => !t.Value.Task.IsCompleted)) _throttlingTimer.Schedule();
        }

        public void OnAllClientsDisconnected()
        {
            _isAnyConnected = false;
        }

        public void PushForNewClient()
        {
            _isAnyConnected = true;
            PushAll();
        }

        private TaskDto ParseToDto(LongRunningTask task)
        {
            TaskStatus status = task.Task.Status;
            var result = new TaskDto();
            result.Id = task.Id;
            result.Title = task.Title;
            result.IsSuccess = status == TaskStatus.RanToCompletion;
            result.IsFaulted = status == TaskStatus.Faulted;
            result.IsRunning = !result.IsSuccess && !result.IsFaulted;
            return result;
        }
    }
}
