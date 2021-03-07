using System.Collections.Generic;

namespace Otokoneko.Client.WPFClient.ViewModel
{
    public interface INavigationState
    {
        public INavigationViewModel GetViewModel();
    }

    public interface INavigationViewModel
    {
        public INavigationState GetState();
    }

    public class NavigationService : BaseViewModel
    {
        private INavigationViewModel _selectedViewModel;

        public INavigationViewModel SelectedViewModel
        {
            get => _selectedViewModel;
            set { _selectedViewModel = value; OnPropertyChanged(nameof(SelectedViewModel)); }
        }

        private readonly List<INavigationState> _backwardStack = new List<INavigationState>();
        private readonly List<INavigationState> _forwardStack = new List<INavigationState>();

        public bool ForwardEnable => _forwardStack.Count != 0;
        public bool BackwardEnable => _backwardStack.Count != 0;

        public void Refresh()
        {
            var state = _selectedViewModel.GetState();
            SelectedViewModel = state.GetViewModel();
        }

        public void NavigateBack()
        {
            if (_backwardStack.Count == 0) return;
            var back = _backwardStack[^1];
            _backwardStack.RemoveAt(_backwardStack.Count - 1);
            _forwardStack.Add(_selectedViewModel.GetState());
            SelectedViewModel = back.GetViewModel();
            OnPropertyChanged(nameof(ForwardEnable));
            OnPropertyChanged(nameof(BackwardEnable));
        }

        public void NavigateForward()
        {
            if (_forwardStack.Count == 0) return;
            var back = _forwardStack[^1];
            _forwardStack.RemoveAt(_forwardStack.Count - 1);
            _backwardStack.Add(_selectedViewModel.GetState());
            SelectedViewModel = back.GetViewModel();
            OnPropertyChanged(nameof(ForwardEnable));
            OnPropertyChanged(nameof(BackwardEnable));
        }

        public void Navigate(INavigationViewModel viewModel, bool addToBackwardStack=true)
        {
            _forwardStack.Clear();
            if (_selectedViewModel != null && addToBackwardStack) _backwardStack.Add(_selectedViewModel.GetState());
            SelectedViewModel = viewModel;
            OnPropertyChanged(nameof(ForwardEnable));
            OnPropertyChanged(nameof(BackwardEnable));
        }
    }
}