using System.Threading.Tasks;
using System.Windows;

namespace Otokoneko.Client.WPFClient.ViewModel
{
    public static partial class Constant
    {
        public const string PlanNotFound = "该计划不存在";
    }

    class PlanExplorerViewModel: ExplorerViewModel<PlanDetailViewModel>
    {
        public override async ValueTask OnLoaded()
        {
        }

        public override async ValueTask CreateNewTab(long planId = -1)
        {
            if (planId <= 0)
            {
                var planDetail = new PlanDetailViewModel()
                {
                    ExplorerHeader = new ExplorerHeader()
                    {
                        Header = $"New tab",
                        CloseButtonEnabled = true
                    }
                };
                Explorer.Insert(Explorer.Count - 1, planDetail);
            }
            else
            {
                var plan = await Model.GetPlan(planId);
                if (plan == null)
                {
                    MessageBox.Show(Constant.PlanNotFound);
                    return;
                }
                var planDetail = new PlanDetailViewModel(plan);
                Explorer.Insert(Explorer.Count - 1, planDetail);
            }
        }

    }
}
