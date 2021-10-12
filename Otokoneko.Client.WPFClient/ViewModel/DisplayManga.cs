using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Otokoneko.Client.WPFClient.ViewModel
{
    public class DisplayManga : BaseViewModel
    {
        public long ObjectId { get; set; }
        public long CoverId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }

        private bool Accessed { get; set; } = false;
        private BitmapImage _cover;
        public BitmapImage Cover
        {
            get
            {
                if (_cover != null || Accessed) return _cover;
                Accessed = true;
                LoadCover();
                return _cover;
            }
            set
            {
                _cover = value;
                OnPropertyChanged(nameof(Cover));
            }
        }

        private async Task LoadCover()
        {
            var imageContent = await Model.GetImage(CoverId);
            Cover = Utils.ImageUtils.Convert(imageContent);
        }

        public ICommand ClickCommand { get; set; }
    }

}
