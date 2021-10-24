using System;
using System.Linq;
using System.Threading.Tasks;

namespace Otokoneko.Client.WPFClient.ViewModel
{
    class MainViewModel : BaseViewModel
    {
        public Action CloseWindow { get; set; }

        private int _selectedIndex;

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                _selectedIndex = value;
                if (_selectedIndex < 0 || _selectedIndex >= ViewModels.Length) return;
                SelectedViewModel = ViewModels[_selectedIndex];
                OnPropertyChanged(nameof(SelectedViewModel));
            }
        }

        private int _selectedOptionIndex = -1;

        public int SelectedOptionIndex
        {
            get => _selectedOptionIndex;
            set
            {
                _selectedOptionIndex = value;
                if (_selectedOptionIndex < 0 || _selectedOptionIndex >= OptionViewModels.Length) return;
                SelectedViewModel = OptionViewModels[_selectedOptionIndex];
                OnPropertyChanged(nameof(SelectedViewModel));
            }
        }

        public object SelectedViewModel { get; set; }

        private object[] ViewModels { get; } =
        {
            new MangaExplorerViewModel(),
            new LibraryManagerViewModel(),
            new TagManagerViewModel(),
            new PlanManagerViewModel(),
            new TaskSchedulerViewModel(),
        };

        private object[] OptionViewModels { get; } =
        {
            new SettingViewModel(),
            new MessageBoxViewModel(),
            new UserManagerViewModel()
        };

        private int _uncheckedMessageNumber = 0;

        public string UncheckedMessageNumber =>
            _uncheckedMessageNumber == 0 ? null : _uncheckedMessageNumber.ToString();

        public async ValueTask OnLoaded()
        {
            await Model.SubscribeMessage();
            await Model.CountMessageUnchecked();
            if (OptionViewModels.Last() is UserManagerViewModel userManagerViewModel)
            {
                userManagerViewModel.CloseWindow = CloseWindow;
            }
        }

        private void ModelOnNumberOfUncheckedMessageChanged(object? sender, int e)
        {
            _uncheckedMessageNumber = e;
            OnPropertyChanged(nameof(UncheckedMessageNumber));
        }

        public MainViewModel()
        {
            SelectedIndex = 0;
            Model.NumberOfUncheckedMessageChanged += ModelOnNumberOfUncheckedMessageChanged;
        }
    }
}