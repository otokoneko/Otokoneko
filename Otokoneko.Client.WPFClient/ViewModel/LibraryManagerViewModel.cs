using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Otokoneko.Client.WPFClient.View;
using Otokoneko.DataType;

namespace Otokoneko.Client.WPFClient.ViewModel
{
    class Library
    {
        public delegate void DialogShowedHandler();

        public DialogShowedHandler OnDialogShowed;
        public long ObjectId { get; set; }
        public LibraryType LibraryType { get; set; }
        public string Path { get; set; }
        public string Name { get; set; }

        public ICommand ClickCommand => new AsyncCommand(async () =>
        {
            var libraryDetailWindow = new LibraryDetailWindow(ObjectId);
            libraryDetailWindow.ShowDialog();
            OnDialogShowed();
        });

        public Library(FileTreeRoot library)
        {
            if (library == null)
            {
                LibraryType = LibraryType.None;
                return;
            }
            ObjectId = library.ObjectId;
            LibraryType = library.Scheme switch
            {
                "file" => LibraryType.Local,
                "ftp" => LibraryType.Ftp,
                "ftps" => LibraryType.Ftp,
                "sftp" => LibraryType.Ftp,
                _ => LibraryType
            };
            Name = library.Name;
            Path = library.Path;
        }
    }

    class LibraryManagerViewModel: BaseViewModel
    {
        public ObservableCollection<Library> Libraries { get; set; }

        private bool _loaded = false;

        public ICommand RefreshCommand => new AsyncCommand(async () =>
          {
              await Load();
          });

        private async ValueTask Load()
        {
            Libraries = new ObservableCollection<Library>()
            {
                new Library(null),
            };
            var libraries = await Model.GetLibraries();
            if (libraries == null) return;
            foreach (var library in libraries)
            {
                Libraries.Insert(0, new Library(library));
            }

            foreach(var library in Libraries)
            {
                library.OnDialogShowed += () => Load();
            }

            OnPropertyChanged(nameof(Libraries));
        }

        public async ValueTask OnLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            await Load();
        }
    }
}
