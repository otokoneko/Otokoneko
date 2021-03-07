using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MessagePack;
using Otokoneko.Client.WPFClient.ViewModel;
using Otokoneko.DataType;

namespace Otokoneko.DataType
{
    public partial class DisplayTask : BaseViewModel
    {
        private List<DisplayTask> _children;
        private bool _loaded = false;

        [IgnoreMember]
        public bool IsExpanded
        {
            set
            {
                if (value && (!_loaded))
                {
                    LoadChildren();
                }
            }
        }

        [IgnoreMember]
        public bool IsPlaceHolder { get; set; }

        [IgnoreMember]
        public List<DisplayTask> SubScheduleTasks
        {
            get
            {
                if (IsPlaceHolder) return null;
                if (_loaded || _children != null) return _children;
                if (HasChild)
                    return new List<DisplayTask>()
                    {
                        new DisplayTask() {Name = "Loading...", IsPlaceHolder = true}
                    };
                return null;
            }
            set => _children = value;
        }

        public async ValueTask LoadChildren()
        {
            _loaded = true;
            var children = await Model.GetSubScheduleTasks(ObjectId);
            if (children == null) return;
            _children = children
                .OrderByDescending(it => it.Status)
                .ThenBy(it => it.ObjectId)
                .ToList();
            OnPropertyChanged(nameof(SubScheduleTasks));
        }
    }
}

namespace Otokoneko.Client.WPFClient.ViewModel
{
    class TaskDetailViewModel: ExplorerContent
    {
        private DisplayTask Task { get; }
        public List<DisplayTask> SubScheduleTasks { get; set; }

        public async ValueTask OnLoaded()
        {
            SubScheduleTasks = await Model.GetSubScheduleTasks(Task.ObjectId);
            OnPropertyChanged(nameof(SubScheduleTasks));
        }

        public TaskDetailViewModel() {}

        public TaskDetailViewModel(DisplayTask task)
        {
            Task = task;
            ExplorerHeader = new ExplorerHeader()
            {
                CloseButtonEnabled = true,
                Header = Task.Name
            };
        }
    }
}
