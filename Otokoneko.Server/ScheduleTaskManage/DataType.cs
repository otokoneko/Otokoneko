using IdGen;
using System;
using System.Linq;
using Otokoneko.Utils;
using Otokoneko.Server.Utils;

namespace Otokoneko.DataType
{
    #region ScheduleTask

    public abstract partial class ScheduleTask
    {
        public long ObjectId { get; set; }
        public string Name { get; set; }
        public TaskStatus Status { get; set; }

        public double Progress
        {
            get
            {
                if (Children == null || Children.Count == 0)
                    return
                        Status == TaskStatus.Success
                            ? 1
                            : (Counter.Target == 0
                                ? 0
                                : (double) Counter);

                var result = .0;
                foreach (var child in Children)
                {
                    result += child.Progress;
                }

                return result / Children.Count;
            }
        }

        public virtual int Priority => int.MaxValue;

        public long ParentId { get; set; }

        private ScheduleTask _parent;
        public ScheduleTask Parent
        {
            get => _parent;
            set
            {
                _parent = value;
                ParentId = value.ObjectId;
            }
        }

        public ThreadSafeList<ScheduleTask> Children { get; set; }

        public AtomicCounter Counter { get; set; }

        private static readonly IdGenerator IdGenerator = new IdGenerator(3);

        public event EventHandler<TaskStatus> Updated;

        public bool Stop()
        {
            Update(TaskStatus.Canceled);
            return Status == TaskStatus.Canceled;
        }

        public bool Restart()
        {
            Update(TaskStatus.Waiting);
            return Status == TaskStatus.Waiting;
        }

        public void Start()
        {
            Update(TaskStatus.Waiting);
        }

        protected virtual void OnUpdated()
        {
            Updated?.Invoke(this, Status);
        }

        public virtual void Update(TaskStatus status, TaskStatus fromChild = TaskStatus.None)
        {
            if (ObjectId == 0) return;
            var old = Status;
            switch (status)
            {
                case TaskStatus.None:
                    if (fromChild == TaskStatus.Success) Counter++;

                    if (Children.All(child => child.Status == TaskStatus.Fail || child.Status == TaskStatus.Success))
                    {
                        Status = Counter.IsCompleted()
                            ? TaskStatus.Success
                            : TaskStatus.Fail;
                    }

                    Parent?.Update(TaskStatus.None, Status);

                    break;
                case TaskStatus.Executing:
                case TaskStatus.Running:
                case TaskStatus.Fail:
                case TaskStatus.Success:
                    Status = (TaskStatus)Math.Max((int)Status, (int)status);
                    Parent?.Update(TaskStatus.None, Status);
                    break;
                case TaskStatus.Waiting:
                    if (Status != TaskStatus.None && Status != TaskStatus.Fail) break;
                    Counter = new AtomicCounter(0, Counter?.Target ?? 0);
                    Status = status;
                    foreach (var child in Children.ToArray())
                    {
                        child.Update(TaskStatus.Canceled);
                    }

                    break;
                case TaskStatus.Canceled:
                    Status = status;
                    foreach (var child in Children.ToArray())
                    {
                        child.Update(TaskStatus.Canceled);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, null);
            }

            if (old != Status)
                OnUpdated();
        }

        protected ScheduleTask()
        {
            ObjectId = IdGenerator.CreateId();
            Children = new ThreadSafeList<ScheduleTask>();
        }

        public static explicit operator DisplayTask(ScheduleTask task)
        {
            return new DisplayTask()
            {
                ObjectId = task.ObjectId,
                Name = task.Name,
                Progress = task.Progress,
                Status = task.Status,
                CreateTime = IdGenerator.FromId(task.ObjectId).DateTimeOffset.DateTime,
                HasChild = task.Children.Count > 0
            };
        }
    }

    public sealed class RootScheduleTask : ScheduleTask
    {
        public RootScheduleTask()
        {
            ObjectId = 0;
        }
    }

    #endregion

    #region Plan

    public partial class Plan
    {
        public event EventHandler Triggered;

        public virtual void OnTriggered()
        {
            Triggered?.Invoke(this, null);
        }
    }

    #endregion
}