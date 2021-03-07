using System.ComponentModel;

namespace Otokoneko.Client.WPFClient.ViewModel
{
    public static partial class Constant
    {
        public const string OperateNotice = "操作提示";

        public const string DeleteSuccess = "删除成功";
        public const string DeleteFail = "删除失败";

        public const string UpdateSuccess = "更新成功";
        public const string UpdateFail = "更新失败";

        public const string Null = "无";
    }

    public abstract class BaseViewModel : INotifyPropertyChanged
    {
        protected static Otokoneko.Client.Model Model { get; } = Otokoneko.Client.Model.Instance;

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}