using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Otokoneko.DataType;
using Quartz;
using Quartz.Impl;
using TaskStatus = Otokoneko.DataType.TaskStatus;

namespace Otokoneko.Server.ScheduleTaskManage
{
    public interface ITriggerHandler<in T>
    where T: ScheduleTaskTrigger
    {
        public ValueTask Start(T trigger, Plan plan);
        public ValueTask Stop(T trigger, Plan plan);
    }

    public class CronTriggerHandler : ITriggerHandler<CronTrigger>
    {
        public class CreateScheduleTaskJob : IJob
        {
            public async Task Execute(IJobExecutionContext context)
            {
                var metaTask = (Plan)context.JobDetail.JobDataMap.Get(nameof(Plan));
                if (metaTask.Enable)
                    metaTask.OnTriggered();
            }
        }

        private static readonly StdSchedulerFactory Factory;
        private static readonly IScheduler Scheduler;

        static CronTriggerHandler()
        {
            Factory = new StdSchedulerFactory();
            Scheduler = Factory.GetScheduler().Result;
            Scheduler.Start().Wait();
        }

        public async ValueTask Start(CronTrigger cronTrigger, Plan plan)
        {
            var name = plan.ObjectId;

            await Scheduler.UnscheduleJob(new TriggerKey($"{name}-Trigger", nameof(CronTrigger)));

            var job = JobBuilder.Create<CreateScheduleTaskJob>()
                .WithIdentity($"{name}-Job", nameof(CronTrigger))
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity($"{name}-Trigger", nameof(CronTrigger))
                .StartAt(cronTrigger.StartDateTime.ToUniversalTime())
                .WithSimpleSchedule(builder =>
                {
                    builder
                        .WithInterval(TimeSpan.FromMinutes(cronTrigger.Interval))
                        .RepeatForever();
                })
                .Build();

            job.JobDataMap.Put(nameof(Plan), plan);

            await Scheduler.ScheduleJob(job, trigger);
        }

        public async ValueTask Stop(CronTrigger trigger, Plan plan)
        {
            var name = plan.ObjectId;

            await Scheduler.UnscheduleJob(new TriggerKey($"{name}-Trigger", nameof(CronTrigger)));
        }
    }

    public class DisposableTriggerHandler: ITriggerHandler<DisposableTrigger>
    {
        public async ValueTask Start(DisposableTrigger trigger, Plan plan)
        {
            if (plan.Enable && !trigger.Triggered)
            {
                trigger.Triggered = true;
                plan.OnTriggered();
            }
        }

        public async ValueTask Stop(DisposableTrigger trigger, Plan plan)
        {
            
        }
    }

    public class TaskCompletedTriggerHandler: ITriggerHandler<TaskCompletedTrigger>
    {
        public PlanManager PlanManager { get; set; }

        public async ValueTask Start(TaskCompletedTrigger trigger, Plan plan)
        {
            var tasks = new List<ScheduleTask>();
            PlanManager.PlanTriggered += (sender, tuple) =>
            {
                if (trigger.PlanId != tuple.Item1.ObjectId) return;
                lock (tasks)
                {
                    tasks.AddRange(tuple.Item2);
                }
                foreach (var scheduleTask in tuple.Item2)
                {
                    scheduleTask.Updated += (o, status) =>
                    {
                        lock (tasks)
                        {
                            if (tasks.Count == 0) return;
                            foreach (var task in tasks.ToList().Where(task => task.Status == TaskStatus.Success || 
                                                                              task.Status == TaskStatus.Canceled ||
                                                                              task.Status == TaskStatus.Fail))
                            {
                                tasks.Remove(task);
                            }

                            if (tasks.Count == 0 && plan.Enable)
                            {
                                plan.OnTriggered();
                            }
                        }
                    };
                }
            };
        }

        public async ValueTask Stop(TaskCompletedTrigger trigger, Plan plan)
        {

        }
    }
}
