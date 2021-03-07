using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Otokoneko.DataType;

namespace Otokoneko.Client.WPFClient.ViewModel
{
    class TaskSchedulerViewModel: BaseViewModel
    {
        public TaskExplorerViewModel TaskExplorerViewModel { get; set; }
        public ObservableCollection<DisplayTask> Tasks { get; set; }

        public ICommand RefreshCommand => new AsyncCommand(async () =>
        {
            await Refresh();
        });

        public ICommand ShowDetailCommand => new AsyncCommand<long>(async (taskId) =>
        {
            await TaskExplorerViewModel.CreateNewTab(taskId);
        });

        public ICommand RestartCommand => new AsyncCommand<long>(async (taskId) =>
        {
            await Model.RestartScheduleTask(taskId);
            await Refresh();
        });

        public ICommand DeleteCommand => new AsyncCommand<long>(async (taskId) =>
        {
            await Model.RemoveScheduleTask(taskId);
            await Refresh();
        });

        public TaskSchedulerViewModel()
        {
            TaskExplorerViewModel = new TaskExplorerViewModel();
        }

        private async ValueTask Refresh()
        {
            var tasks = await Model.GetSubScheduleTasks(0);
            if (tasks == null) return;
            Tasks = new ObservableCollection<DisplayTask>(tasks);
            OnPropertyChanged(nameof(Tasks));
        }

        public async ValueTask OnLoaded()
        {
            await Refresh();
        }
    }
}
