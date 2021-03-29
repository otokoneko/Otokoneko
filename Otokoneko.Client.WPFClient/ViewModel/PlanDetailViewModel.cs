using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using MessagePack;
using Otokoneko.Client.WPFClient.ViewModel;
using Otokoneko.DataType;

namespace Otokoneko.DataType
{
    public abstract partial class Plan
    {
        public virtual async ValueTask OnLoaded()
        {

        }
    }

    public abstract partial class ScheduleTaskTrigger
    {
        public virtual async ValueTask OnLoaded()
        {

        }
    }

    partial class ScanPlan : INotifyPropertyChanged
    {
        private Client.Model Model { get; } = Client.Model.Instance;
    
        [IgnoreMember]
        public ObservableCollection<FileTreeRoot> Libraries { get; set; }

        [IgnoreMember]
        public int SelectedLibraryIndex
        {
            get => Libraries.IndexOf(Libraries.FirstOrDefault(it => it.ObjectId == LibraryId));
            set
            {
                if (value == -1) return;
                LibraryId = Libraries[value].ObjectId;
            }
        }

        [IgnoreMember]
        public FileTreeRoot SelectedLibrary
        {
            get => Libraries.FirstOrDefault(it => it.ObjectId == LibraryId);
            set
            {
                if (value == null) return;
                LibraryId = value.ObjectId;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    
        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    
        public override async ValueTask OnLoaded()
        {
            var libraries = await Model.GetLibraries();
            Libraries = new ObservableCollection<FileTreeRoot>(libraries);
            OnPropertyChanged(nameof(Libraries));
            OnPropertyChanged(nameof(SelectedLibrary));
        }
    }

    partial class CronTrigger : INotifyPropertyChanged
    {
        [IgnoreMember]
        public ObservableCollection<Tuple<string, int>> Intervals { get; set; } =
            new ObservableCollection<Tuple<string, int>>()
            {
                new Tuple<string, int>(Constant.EveryHour, 60),
                new Tuple<string, int>(Constant.EveryDay, 24 * 60),
                new Tuple<string, int>(Constant.EveryWeek, 7 * 24 * 60),
                new Tuple<string, int>(Constant.EveryMonth, 30 * 24 * 60),
            };

        [IgnoreMember]
        public Tuple<string, int> SelectedInterval
        {
            set
            {
                if (value == null) return;
                Interval = value.Item2;
                OnPropertyChanged(nameof(Interval));
            }
        }

        [IgnoreMember]
        public DateTime LocalDateTime
        {
            get => StartDateTime.ToLocalTime();
            set => StartDateTime = value.ToUniversalTime();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override async ValueTask OnLoaded()
        {
            if(StartDateTime == default)
                LocalDateTime = DateTime.Now;
        }
    }

    partial class TaskCompletedTrigger : INotifyPropertyChanged
    {
        private Client.Model Model { get; } = Client.Model.Instance;

        [IgnoreMember]
        public ObservableCollection<Plan> Plans { get; set; }

        [IgnoreMember]
        public Plan SelectedPlan
        {
            get => Plans.FirstOrDefault(it => it.ObjectId == PlanId);
            set => PlanId = value?.ObjectId ?? 0;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override async ValueTask OnLoaded()
        {
            var plans = await Model.GetPlans();
            Plans = new ObservableCollection<Plan>(plans);
            OnPropertyChanged(nameof(Plans));
            OnPropertyChanged(nameof(SelectedPlan));
        }
    }

    partial class DisposableTrigger
    {
        public override async ValueTask OnLoaded()
        {
            Triggered = false;
        }
    }
}

namespace Otokoneko.Client.WPFClient.ViewModel
{
    public static partial class Constant
    {
        public const string EveryHour = "每小时";
        public const string EveryDay = "每天";
        public const string EveryWeek = "每周";
        public const string EveryMonth = "每月";

        public const string NewPlanName = "新计划";

        public const string ShouldSelectPlanType = "请选择一种计划";
        public const string ShouldSelectTriggerType = "请选择一种触发器";
        public const string PlanNameShouldNotBeEmpty = "计划名称不可为空";

        public const string DownloadLibraryPathShouldNotBeEmpty = "下载计划中的下载位置不可为空";
        public const string DownloadUrlsPathShouldNotBeEmpty = "下载计划中的链接列表不可为空";

        public const string ScanLibraryShouldNotBeEmpty = "扫描计划中的扫描库不可为空";

        public const string IntervalShouldBiggerThanZero = "时间间隔应大于0";
    }

    class PlanDetailViewModel : ExplorerContent
    {
        private long ObjectId { get; set; }

        public string Name { get; set; } = Constant.NewPlanName;
        public bool Enable { get; set; } = true;
        private readonly Dictionary<Type, Plan> _plans;
        private readonly Dictionary<Type, ScheduleTaskTrigger> _triggers;
        public Plan Plan { get; set; }
        public ScheduleTaskTrigger Trigger { get; set; }
        public ObservableCollection<Type> PlanTypes => new ObservableCollection<Type>(Plan.SupportPlanTypes);
        public ObservableCollection<Type> TriggerTypes => new ObservableCollection<Type>(ScheduleTaskTrigger.SupportTriggerTypes);
        
        private Type _selectedPlanType;
        public Type SelectedPlanType
        {
            get => _selectedPlanType;
            set
            {
                _selectedPlanType = value;
                if (value == null)
                {
                    Plan = null;
                    OnPropertyChanged(nameof(Plan));
                    return;
                }
                if (!_plans.TryGetValue(value, out var plan))
                {
                    plan = (Plan) Activator.CreateInstance(value);
                    plan.OnLoaded();
                    _plans.Add(value, plan);
                }

                Plan = plan;
                OnPropertyChanged(nameof(Plan));
            }
        }

        private Type _selectedTriggerType;
        public Type SelectedTriggerType
        {
            get => _selectedTriggerType;
            set
            {
                _selectedTriggerType = value;
                if (value == null)
                {
                    Trigger = null;
                    OnPropertyChanged(nameof(Trigger));
                    return;
                }
                if (!_triggers.TryGetValue(value, out var scheduleTaskTrigger))
                {
                    scheduleTaskTrigger = (ScheduleTaskTrigger)Activator.CreateInstance(value);
                    scheduleTaskTrigger.OnLoaded();
                    _triggers.Add(value, scheduleTaskTrigger);
                }

                Trigger = scheduleTaskTrigger;
                OnPropertyChanged(nameof(Trigger));
            }
        }

        private bool Check()
        {
            if (Plan == null)
            {
                MessageBox.Show(Constant.ShouldSelectPlanType);
                return false;
            }

            if (Trigger == null)
            {
                MessageBox.Show(Constant.ShouldSelectTriggerType);
                return false;
            }

            if (string.IsNullOrEmpty(Name?.Trim()))
            {
                MessageBox.Show(Constant.PlanNameShouldNotBeEmpty);
                return false;
            }

            if (Plan is DownloadPlan downloadPlan)
            {
                if (string.IsNullOrEmpty(downloadPlan.LibraryPath?.Trim()))
                {
                    MessageBox.Show(Constant.DownloadLibraryPathShouldNotBeEmpty);
                    return false;
                }
                if (string.IsNullOrEmpty(downloadPlan.Urls?.Trim()))
                {
                    MessageBox.Show(Constant.DownloadUrlsPathShouldNotBeEmpty);
                    return false;
                }
            }
            else if(Plan is ScanPlan scanPlan)
            {
                if (scanPlan.LibraryId <= 0)
                {
                    MessageBox.Show(Constant.ScanLibraryShouldNotBeEmpty);
                    return false;
                }
            }

            if (Trigger is CronTrigger cronTrigger)
            {
                if (cronTrigger.Interval <= 0)
                {
                    MessageBox.Show(Constant.IntervalShouldBiggerThanZero);
                    return false;
                }
            }

            return true;
        }

        public ICommand SaveCommand => new AsyncCommand(async () =>
        {
            if (!Check()) return;
            Plan.Trigger = Trigger;
            Plan.Name = Name;
            Plan.Enable = Enable;
            Plan.ObjectId = ObjectId;
            if (ObjectId <= 0)
            {
                await Model.AddPlan(Plan);
            }
            else
            {
                await Model.UpdatePlan(Plan);
            }
        });

        public PlanDetailViewModel()
        {
            ObjectId = -1;
            _plans = new Dictionary<Type, Plan>();
            _triggers = new Dictionary<Type, ScheduleTaskTrigger>();
        }

        public PlanDetailViewModel(Plan plan)
        {
            Plan = plan;
            Trigger = Plan.Trigger;
            Plan.OnLoaded();
            Trigger.OnLoaded();
            ObjectId = Plan.ObjectId;
            Name = Plan.Name;
            Enable = Plan.Enable;
            _plans = new Dictionary<Type, Plan> {{Plan.GetType(), Plan}};
            _triggers = new Dictionary<Type, ScheduleTaskTrigger> {{Trigger.GetType(), Trigger}};
            _selectedTriggerType = Trigger.GetType();
            _selectedPlanType = Plan.GetType();
            ExplorerHeader = new ExplorerHeader()
            {
                CloseButtonEnabled = true,
                Header = Plan.Name
            };
        }
    }
}
