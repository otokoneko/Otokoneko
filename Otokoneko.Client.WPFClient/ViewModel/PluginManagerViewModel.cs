using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Otokoneko.DataType;

namespace Otokoneko.Client.WPFClient.ViewModel
{
    public static partial class Constant
    {
        public const string ResetPluginTemplate = "确定重置插件 {0} 的所有参数？";
    }

    class DisplayPlugin : BaseViewModel
    {
        public PluginDetail PluginDetail { get; private set; }
        public ObservableCollection<PluginParameter> Parameters { get; set; }

        public ICommand SaveCommand => new AsyncCommand(async () =>
        {
            PluginDetail.RequiredParameters = Parameters.ToList();
            var success = await Model.SetPluginParameters(PluginDetail);
        });

        public ICommand ResetCommand => new AsyncCommand(async () =>
        {
            var result = MessageBox.Show(string.Format(Constant.ResetPluginTemplate, PluginDetail.Name),
                Constant.OperateNotice, MessageBoxButton.YesNo);
            if (result != MessageBoxResult.Yes) return;
            PluginDetail = await Model.ResetPluginParameters(PluginDetail.Type);
            Parameters = new ObservableCollection<PluginParameter>(PluginDetail.RequiredParameters);
            OnPropertyChanged(nameof(PluginDetail));
            OnPropertyChanged(nameof(Parameters));
        });

        public DisplayPlugin(PluginDetail pluginDetail)
        {
            PluginDetail = pluginDetail;
            Parameters = new ObservableCollection<PluginParameter>(PluginDetail.RequiredParameters);
        }
    } 

    class PluginManagerViewModel: BaseViewModel
    {
        public ObservableCollection<DisplayPlugin> Plugins { get; set; }

        public async ValueTask OnLoaded()
        {
            var pluginDetails = await Model.GetPluginDetails();
            if (pluginDetails == null) return;
            Plugins = new ObservableCollection<DisplayPlugin>(pluginDetails.Select(it => new DisplayPlugin(it)));
            OnPropertyChanged(nameof(Plugins));
        }
    }
}
