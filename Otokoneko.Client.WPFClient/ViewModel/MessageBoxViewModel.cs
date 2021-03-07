using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Otokoneko.DataType;

namespace Otokoneko.Client.WPFClient.ViewModel
{
    public static partial class Constant
    {
        public const string UncheckedMessage = "未读消息";
        public const string CheckedMessage = "已读消息";
    }

    class MessageBoxViewModel: BaseViewModel
    {
        public ObservableCollection<Tuple<string, List<Message>>> MessageBoxes { get; set; }
        public int SelectedIndex { get; set; }

        public ICommand ClearCheckedMessageCommand => new AsyncCommand(async () =>
        {
            await Model.ClearCheckedMessage();
            await OnLoaded();
        });

        public async ValueTask Check()
        {
            await Model.Check(MessageBoxes[0].Item2.Where(it => !it.Checked).Select(it => it.ObjectId).ToList());
            await Model.CountMessageUnchecked();
        }

        public async ValueTask OnLoaded()
        {
            var messages = await Model.GetMessages();
            MessageBoxes = new ObservableCollection<Tuple<string, List<Message>>>
            {
                new Tuple<string, List<Message>>(Constant.UncheckedMessage, messages.Where(it => !it.Checked).ToList()),
                new Tuple<string, List<Message>>(Constant.CheckedMessage, messages.Where(it => it.Checked).ToList())
            };
            OnPropertyChanged(nameof(MessageBoxes));
            SelectedIndex = 0;
            OnPropertyChanged(nameof(SelectedIndex));
        }
    }
}
