using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Otokoneko.Client.WPFClient.ViewModel
{
    public static partial class Constant
    {
        public const string TaskNotFound = "该任务不存在";
    }

    class TaskExplorerViewModel : ExplorerViewModel<TaskDetailViewModel>
    {
        public async override ValueTask OnLoaded()
        {
            
        }

        public override async ValueTask CreateNewTab(long objectId = -1)
        {
            if (objectId > 0)
            {
                var tasks = await Model.GetSubScheduleTasks(0);
                var task = tasks.FirstOrDefault(it => it.ObjectId == objectId);
                if (task == null)
                {
                    MessageBox.Show(Constant.TaskNotFound);
                    return;
                }
                var detail = new TaskDetailViewModel(task);
                Explorer.Insert(Explorer.Count - 1, detail);
            }
        }

        public TaskExplorerViewModel() : base(false)
        {

        }
    }
}
