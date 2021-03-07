using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Otokoneko.Client.WPFClient.ViewModel
{
    abstract class ExplorerContent: BaseViewModel
    {
        public ExplorerHeader ExplorerHeader { get; set; }
    }

    abstract class ExplorerViewModel<TContent> : BaseViewModel where TContent : ExplorerContent, new()
    {
        public ObservableCollection<TContent> Explorer { get; } = new ObservableCollection<TContent>();
        public bool CouldCreateNewTab { get; }

        public abstract ValueTask OnLoaded();

        public void Move(object source, object target)
        {
            var sourceIndex = Explorer.IndexOf(source as TContent);
            var targetIndex = Explorer.IndexOf(target as TContent);
            if (sourceIndex == -1 ||
                targetIndex == -1 ||
                sourceIndex == Explorer.Count - 1 || 
                targetIndex == Explorer.Count - 1 ||
                sourceIndex == targetIndex) return;
            Explorer.Move(sourceIndex, targetIndex);
        }

        public bool IsDraggable(object a)
        {
            var header = a as TContent;
            return header?.ExplorerHeader?.CloseButtonEnabled == true;
        }

        public abstract ValueTask CreateNewTab(long objectId = -1);

        protected ExplorerViewModel(bool couldCreateNewTab=true)
        {
            CouldCreateNewTab = couldCreateNewTab;
            Explorer.Add(new TContent()
            {
                ExplorerHeader = new ExplorerHeader()
                {
                    Header = "+",
                    CloseButtonEnabled = false,
                    Visible = couldCreateNewTab
                }
            });
        }
    }
}
