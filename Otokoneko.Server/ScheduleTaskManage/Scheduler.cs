using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Otokoneko.DataType;
using Otokoneko.Utils;
using TaskStatus = Otokoneko.DataType.TaskStatus;

namespace Otokoneko.Server.ScheduleTaskManage
{
    public class ScheduleTaskPriority:IComparable<ScheduleTaskPriority>
    {
        private int _priority;
        private long _objectId;

        public ScheduleTaskPriority(ScheduleTask task)
        {
            _priority = task.Priority;
            _objectId = task.ObjectId;
        }

        public int CompareTo(ScheduleTaskPriority? other)
        {
            if (_priority < other._priority)
            {
                return -1;
            }
            else if(_priority==other._priority)
            {
                if (_objectId < other._objectId)
                {
                    return -1;
                }
                else if (_objectId == other._objectId)
                {
                    return 0;
                }

            }

            return 1;
        }
    }

    public class Scheduler
    {
        private readonly List<Task> _worker;
        private readonly ConcurrentDictionary<long, ScheduleTask> _idToTask;
        private readonly AsyncQueue<ScheduleTaskPriority, ScheduleTask> _waitingScheduleTask;
        private readonly ScheduleTask _root;

        public ITaskHandler<DownloadMangaScheduleTask> DownloadMangaScheduleTaskHandler { get; set; }
        public ITaskHandler<DownloadChapterScheduleTask> DownloadChapterScheduleTaskHandler { get; set; }
        public ITaskHandler<DownloadImageScheduleTask> DownloadImageScheduleTaskHandler { get; set; }
        public ITaskHandler<ScanLibraryTask> ScanLibraryTaskHandler { get; set; }
        public ITaskHandler<ScanMangaTask> ScanMangaTaskHandler { get; set; }

        public Scheduler()
        {
            var workerNumber = 6;
            _root = new RootScheduleTask();
            _idToTask = new ConcurrentDictionary<long, ScheduleTask>();
            _idToTask.TryAdd(0, _root);
            _waitingScheduleTask = new AsyncQueue<ScheduleTaskPriority, ScheduleTask>();
            _worker = Enumerable.Range(0, workerNumber).Select(i => Task.Run(Work)).ToList();
        }

        private async Task Work()
        {
            while (true)
            {
                var task = await _waitingScheduleTask.Dequeue();
                if (task.Status != TaskStatus.Waiting) continue;
                await Execute(task);
                await Task.Yield();
            }
        }

        private async ValueTask Execute(ScheduleTask task)
        {
            switch (task)
            {
                case DownloadMangaScheduleTask downloadMangaScheduleTask:
                    await DownloadMangaScheduleTaskHandler.Execute(downloadMangaScheduleTask);
                    break;
                case DownloadChapterScheduleTask downloadChapterScheduleTask:
                    await DownloadChapterScheduleTaskHandler.Execute(downloadChapterScheduleTask);
                    break;
                case DownloadImageScheduleTask downloadImageScheduleTask:
                    await DownloadImageScheduleTaskHandler.Execute(downloadImageScheduleTask);
                    break;
                case ScanLibraryTask scanLibraryTask:
                    await ScanLibraryTaskHandler.Execute(scanLibraryTask);
                    break;
                case ScanMangaTask scanMangaTask:
                    await ScanMangaTaskHandler.Execute(scanMangaTask);
                    break;
            }
        }

        private async void TaskOnUpdated(object? sender, TaskStatus e)
        {
            var task = sender as ScheduleTask;
            switch (e)
            {
                case TaskStatus.None:
                    break;
                case TaskStatus.Waiting:
                    await _waitingScheduleTask.Enqueue(task, new ScheduleTaskPriority(task));
                    break;
                case TaskStatus.Executing:
                    break;
                case TaskStatus.Running:
                    foreach (var child in task.Children)
                    {
                        _idToTask.TryAdd(child.ObjectId, child);
                        child.Updated += TaskOnUpdated;
                        child.Start();
                    }
                    if (task.Children.Count == 0)
                    {
                        task.Update(TaskStatus.Success);
                    }
                    break;
                case TaskStatus.Fail:
                    break;
                case TaskStatus.Success:
                    if (task.Parent == _root) goto case TaskStatus.Canceled;
                    break;
                case TaskStatus.Canceled:
                    RemoveFromScheduler(task);
                    break;
            }
        }

        private void RemoveFromScheduler(ScheduleTask task, bool removeFromParent = true)
        {
            if (removeFromParent)
            {
                task.Parent.Children.Remove(task);
            }
            task.Updated -= TaskOnUpdated;
            _idToTask.TryRemove(task.ObjectId, out _);
            foreach (var child in task.Children)
            {
                RemoveFromScheduler(child, false);
            }
        }

        public bool ScheduleAndStart(ScheduleTask task)
        {
            var taskId = _root.Children.FirstOrDefault(it => it.Equals(task))?.ObjectId;
            if (taskId != null)
            {
                Cancel((long)taskId);
            }
            _idToTask.TryAdd(task.ObjectId, task);
            _root.Children.Add(task);
            task.Parent = _root;
            task.Updated += TaskOnUpdated;
            task.Start();
            return true;
        }

        public bool Restart(long taskId)
        {
            return _idToTask.TryGetValue(taskId, out var task) &&
                   _root.Children.Contains(task) &&
                   task.Restart();
        }

        public bool Cancel(long taskId)
        {
            return _idToTask.TryGetValue(taskId, out var task) &&
                   _root.Children.Contains(task) &&
                   task.Stop();
        }

        public List<DisplayTask> GetSubTasks(long taskId)
        {
            return _idToTask.TryGetValue(taskId, out var task)
                ? task.Children.Select(it => (DisplayTask)it).ToList()
                : null;
        }
    }
}