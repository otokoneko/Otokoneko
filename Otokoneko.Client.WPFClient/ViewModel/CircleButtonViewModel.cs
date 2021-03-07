using System.Windows.Input;

namespace Otokoneko.Client.WPFClient.ViewModel
{
    public class CircleButtonViewModel : BaseViewModel
    {
        public string Image { get; set; }
        public ICommand Command { get; set; }

        private bool _isEnable;

        public bool IsEnable
        {
            get => _isEnable;
            set
            {
                _isEnable = value;
                OnPropertyChanged(nameof(IsEnable));
            }
        }
    }

}