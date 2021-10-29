using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using IdGen;
using LevelDB;
using log4net;
using MessagePack;
using Otokoneko.DataType;
using Otokoneko.Server.MessageBox;

namespace Otokoneko.Server.ScheduleTaskManage
{
    public class PlanManager
    {
        public ILog Logger { get; set; }
        private DB PlanLibrary { get; }

        private static readonly IdGenerator IdGenerator = new IdGenerator(4);

        private readonly ConcurrentDictionary<long, Plan> _idToPlan = new ConcurrentDictionary<long, Plan>();

        private readonly object _updateMutex = new object();

        public MessagePackSerializerOptions SerializerOptions { get; set; } =
            MessagePackSerializerOptions.Standard
                .WithCompression(MessagePackCompression.Lz4Block);

        public ITriggerHandler<TaskCompletedTrigger> TaskCompletedTriggerHandler { get; set; }
        public ITriggerHandler<DisposableTrigger> DisposableTriggerHandler { get; set; }
        public ITriggerHandler<CronTrigger> CronTriggerHandler { get; set; }

        public IPlanHandler<DownloadPlan> DownloadPlanHandler { get; set; }
        public IPlanHandler<ScanPlan> ScanPlanHandler { get; set; }
        public MessageManager MessageManager { get; set; }

        public event EventHandler<Tuple<Plan, List<ScheduleTask>>> PlanTriggered; 

        public PlanManager(ILog logger)
        {
            logger.Info("加载计划任务...");

            var options = new Options { CreateIfMissing = true };
            PlanLibrary = new DB(options, @"./data/planLibrary");
        }

        private async ValueTask SendTriggeredMessage(string planName, int taskNumber)
        {
            var message = new Message()
            {
                SenderId = 0,
                Data = string.Format(MessageTemplate.PlanTriggeredMessage, planName, taskNumber)
            };
            await MessageManager.Send(message, new HashSet<UserAuthority>() {UserAuthority.Root, UserAuthority.Admin});
        }

        private async void PlanOnTriggered(object? sender, EventArgs e)
        {
            var plan = sender as Plan;
            Debug.Assert(plan != null, nameof(plan) + " != null");
            List<ScheduleTask> result = null;
            if (plan is DownloadPlan downloadPlan)
            {
                result = DownloadPlanHandler.Execute(downloadPlan);
            }
            else if (plan is ScanPlan scanPlan)
            {
                result = ScanPlanHandler.Execute(scanPlan);
            }

            plan.LastTriggeredTime = DateTime.Now;
            Update(plan);
            await SendTriggeredMessage(plan.Name, result?.Count ?? 0);
            PlanTriggered?.Invoke(this, new Tuple<Plan, List<ScheduleTask>>(plan, result));
        }

        public void Recover()
        {
            foreach (var (_, value) in PlanLibrary)
            {
                var plan = MessagePackSerializer.Deserialize<Plan>(value, SerializerOptions);
                _idToPlan.TryAdd(plan.ObjectId, plan);
                plan.Triggered += PlanOnTriggered;
                StartTrigger(plan);
            }
        }

        private void Insert(Plan plan)
        {
            plan.Triggered += PlanOnTriggered;
            Update(plan);
            _idToPlan.TryAdd(plan.ObjectId, plan);
        }

        private void Update(Plan plan)
        {
            var bytes = MessagePackSerializer.Serialize(plan, SerializerOptions);
            PlanLibrary.Put(BitConverter.GetBytes(plan.ObjectId), bytes);
        }

        private void Delete(Plan plan)
        {
            plan.Triggered -= PlanOnTriggered;
            PlanLibrary.Delete(BitConverter.GetBytes(plan.ObjectId));
            _idToPlan.Remove(plan.ObjectId, out _);
        }

        public Plan GetPlan(long planId)
        {
            return _idToPlan.TryGetValue(planId, out var plan) ? plan : null;
        }

        public List<Plan> GetPlans()
        {
            return _idToPlan.Values.ToList();
        }

        public long InsertPlan(Plan plan)
        {
            plan.ObjectId = IdGenerator.CreateId();
            try
            {
                Insert(plan);
                StartTrigger(plan);
                return plan.ObjectId;
            }
            catch (Exception e)
            {
                Logger.Error(e);
                return -1;
            }
        }

        public void UpdatePlan(Plan plan)
        {
            lock (_updateMutex)
            {
                if (!_idToPlan.TryGetValue(plan.ObjectId, out var oldPlan)) return;
                oldPlan.Enable = false;
                oldPlan.Triggered -= PlanOnTriggered;
                plan.Triggered += PlanOnTriggered;
                plan.LastTriggeredTime = oldPlan.LastTriggeredTime;
                StartTrigger(plan);
                _idToPlan[plan.ObjectId] = plan;
                Update(plan);
            }
        }

        public bool TriggerPlan(long planId)
        {
            if (!_idToPlan.TryGetValue(planId, out var plan)) return false;
            plan.OnTriggered();
            return true;
        }

        public void RemovePlan(long planId)
        {
            if (!_idToPlan.Remove(planId, out var plan)) return;
            plan.Enable = false;
            StopTrigger(plan);
            Delete(plan);
        }

        public void StartTrigger(Plan plan)
        {
            switch (plan.Trigger)
            {
                case CronTrigger cronTrigger:
                    CronTriggerHandler.Start(cronTrigger, plan);
                    break;
                case TaskCompletedTrigger taskCompletedTrigger:
                    TaskCompletedTriggerHandler.Start(taskCompletedTrigger, plan);
                    break;
                case DisposableTrigger disposableTrigger:
                    DisposableTriggerHandler.Start(disposableTrigger, plan);
                    break;
            }
        }

        public void StopTrigger(Plan plan)
        {
            switch (plan.Trigger)
            {
                case CronTrigger cronTrigger:
                    CronTriggerHandler.Stop(cronTrigger, plan);
                    break;
                case TaskCompletedTrigger taskCompletedTrigger:
                    TaskCompletedTriggerHandler.Stop(taskCompletedTrigger, plan);
                    break;
                case DisposableTrigger disposableTrigger:
                    DisposableTriggerHandler.Stop(disposableTrigger, plan);
                    break;
            }
        }
    }
}