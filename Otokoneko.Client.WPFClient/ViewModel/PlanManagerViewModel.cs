using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Otokoneko.DataType;

namespace Otokoneko.Client.WPFClient.ViewModel
{
    public static partial class Constant
    {
        public const string DeletePlanTemplate = "是否确定删除计划 {0}？";
    }

    class DisplayPlan : BaseViewModel
    {
        public string Name { get; set; }
        public Plan Plan { get; set; }
        public string LastTriggeredLocalTime { get; set; }

        public bool Enable
        {
            get => Plan.Enable;
            set
            {
                if (value == Plan.Enable) return;
                Plan.Enable = value;
                UpdatePlan();
            }
        }

        private async ValueTask UpdatePlan()
        {
            await Model.UpdatePlan(Plan);
        }

        public DisplayPlan(Plan plan)
        {
            Plan = plan;
            Name = plan.Name;
            if (plan.LastTriggeredTime != null)
            {
                LastTriggeredLocalTime = Utils.FormatUtils.FormatLocalDateTime(((DateTime) plan.LastTriggeredTime).ToLocalTime());
            }
        }
    }

    class PlanManagerViewModel: BaseViewModel
    {
        public ObservableCollection<DisplayPlan> Plans { get; set; }
        public PlanExplorerViewModel PlanExplorerViewModel { get; set; }

        public PlanManagerViewModel()
        {
            PlanExplorerViewModel = new PlanExplorerViewModel();
        }

        public ICommand EditCommand => new AsyncCommand<Plan>(async (plan) =>
        {
            await PlanExplorerViewModel.CreateNewTab(plan.ObjectId);
        });

        public ICommand ExecuteCommand => new AsyncCommand<Plan>(async (plan) =>
        {
            await Model.TriggerPlan(plan.ObjectId);
        });

        public ICommand DeleteCommand => new AsyncCommand<Plan>(async (plan) =>
        {
            var result = MessageBox.Show(string.Format(Constant.DeletePlanTemplate, plan.Name), Constant.OperateNotice, MessageBoxButton.YesNo);
            if (result != MessageBoxResult.Yes) return;
            await Model.RemovePlan(plan.ObjectId);
            await Load();
        });

        public ICommand RefreshCommand => new AsyncCommand(async () =>
        {
            await Load();
        });

        private async ValueTask Load()
        {
            Plans = new ObservableCollection<DisplayPlan>();
            var plans = await Model.GetPlans();
            if (plans == null) return;
            foreach (var plan in plans)
            {
                Plans.Add(new DisplayPlan(plan));
            }
            OnPropertyChanged(nameof(Plans));
        }

        public async ValueTask OnLoaded()
        {
            await Load();
        }
    }
}
