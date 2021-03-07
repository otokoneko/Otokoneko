using System.Collections.Generic;
using System.Linq;
using Otokoneko.DataType;

namespace Otokoneko.Server.ScheduleTaskManage
{
    public interface IPlanHandler<in T>
    where T:Plan
    {
        public List<ScheduleTask> Execute(T plan);
    }

    public class DownloadPlanHandler : IPlanHandler<DownloadPlan>
    {
        public Scheduler Scheduler { get; set; }

        public List<ScheduleTask> Execute(DownloadPlan downloadPlan)
        {
            return downloadPlan
                .Urls
                .Split('\n')
                .Where(it => !string.IsNullOrEmpty(it.Trim()))
                .Select(url => (ScheduleTask)new DownloadMangaScheduleTask(url.Trim(), downloadPlan.LibraryPath, url))
                .Where(task => Scheduler.ScheduleAndStart(task))
                .ToList();
        }
    }

    public class ScanPlanHandler : IPlanHandler<ScanPlan>
    {
        public Scheduler Scheduler { get; set; }
        public LibraryManager LibraryManager { get; set; }

        public List<ScheduleTask> Execute(ScanPlan scanPlan)
        {
            var task = new ScanLibraryTask(scanPlan.LibraryId, $"扫描-{LibraryManager.GetLibrary(scanPlan.LibraryId).Name}");
            var result = new List<ScheduleTask>();
            if(Scheduler.ScheduleAndStart(task)) result.Add(task);
            return result;
        }
    }
}