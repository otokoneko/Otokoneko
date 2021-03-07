using System;
using System.Collections.Generic;
using MessagePack;

namespace Otokoneko.DataType
{
    public enum TaskStatus
    {
        None,
        Waiting,
        Executing,  //任何任务在执行Execute()函数时的状态
        Running,    //拥有子任务的任务在执行Execute()函数完成后，任务完成前的状态
        Fail,
        Success,
        Canceled
    }

    [Union(0, typeof(CronTrigger))]
    [Union(1, typeof(DisposableTrigger))]
    [Union(2, typeof(TaskCompletedTrigger))]
    [MessagePackObject]
    public abstract partial class ScheduleTaskTrigger
    {
        [IgnoreMember]
        public static HashSet<Type> SupportTriggerTypes { get; } = new HashSet<Type>()
        {
            typeof(DisposableTrigger),
            typeof(CronTrigger),
            typeof(TaskCompletedTrigger),
        };
    }

    [MessagePackObject]
    public partial class CronTrigger : ScheduleTaskTrigger
    {
        [Key(0)]
        public DateTime StartDateTime { get; set; }
        [Key(1)]
        public int Interval { get; set; }
    }

    [MessagePackObject]
    public partial class DisposableTrigger : ScheduleTaskTrigger
    {
        [Key(0)]
        public bool Triggered { get; set; }
    }

    [MessagePackObject]
    public partial class TaskCompletedTrigger: ScheduleTaskTrigger
    {
        [Key(0)]
        public long PlanId { get; set; }
    }

    [MessagePackObject]
    public partial class DisplayTask
    {
        [Key(0)]
        public long ObjectId { get; set; }
        [Key(1)]
        public string Name { get; set; }
        [Key(2)]
        public TaskStatus Status { get; set; }
        [Key(3)]
        public double Progress { get; set; }
        [Key(4)]
        public DateTime CreateTime { get; set; }
        [Key(5)]
        public bool HasChild { get; set; }
    }

    [Union(0, typeof(DownloadPlan))]
    [Union(1, typeof(ScanPlan))]
    [MessagePackObject]
    public abstract partial class Plan
    {
        [Key(0)]
        public long ObjectId { get; set; }
        [Key(1)]
        public string Name { get; set; }
        [Key(2)]
        public bool Enable { get; set; }
        [Key(3)]
        public ScheduleTaskTrigger Trigger { get; set; }
        [Key(4)]
        public DateTime? LastTriggeredTime { get; set; }

        [IgnoreMember]
        public static HashSet<Type> SupportPlanTypes { get; } = new HashSet<Type>()
        {
            typeof(DownloadPlan),
            typeof(ScanPlan),
        };
    }

    [MessagePackObject]
    public sealed partial class DownloadPlan: Plan
    {
        [Key(5)]
        public string LibraryPath { get; set; }
        [Key(6)]
        public string Urls { get; set; }
    }

    [MessagePackObject]
    public sealed partial class ScanPlan : Plan
    {
        [Key(5)]
        public long LibraryId { get; set; }
    }
}