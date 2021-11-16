using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Otokoneko.Base.Network;
using Otokoneko.Client.WPFClient.View;
using Otokoneko.DataType;

namespace Otokoneko.Client.WPFClient.ViewModel
{
    class LoginDetail : BaseViewModel
    {
        private string _username;
        public string Username
        {
            get => _username;
            set
            {
                _username = value;
                OnPropertyChanged(nameof(Username));
            }
        }

        private string _password;
        public string Password
        {
            get => _password;
            set
            {
                _password = value;
                if (!string.IsNullOrEmpty(_password))
                    PlaceHolderVisibility = Visibility.Collapsed;
            }
        }

        private bool _enableStoreToken;
        private bool _enableAutoLogin;
        public bool EnableStoreToken
        {
            get => _enableStoreToken;
            set
            {
                if (value == _enableStoreToken) return;
                _enableStoreToken = value;
                OnPropertyChanged(nameof(EnableStoreToken));
                if (_enableStoreToken) return;
                _enableAutoLogin = false;
                OnPropertyChanged(nameof(EnableAutoLogin));
            }
        }

        public bool EnableAutoLogin
        {
            get => _enableAutoLogin;
            set
            {
                if (value == _enableAutoLogin) return;
                _enableAutoLogin = value;
                OnPropertyChanged(nameof(EnableAutoLogin));
                if (!_enableAutoLogin) return;
                _enableStoreToken = true;
                OnPropertyChanged(nameof(EnableStoreToken));
            }
        }

        private Visibility _placeHolderVisibility = Visibility.Collapsed;

        public Visibility PlaceHolderVisibility
        {
            get => _placeHolderVisibility;
            set
            {
                _placeHolderVisibility = value;
                OnPropertyChanged(nameof(PlaceHolderVisibility));
            }
        }
    }

    class RegisterDetail: BaseViewModel
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string InvitationCode { get; set; }
    }

    public static partial class Constant
    {
        public const string AddServer = "添加服务器";
        public const string AddServerNotice = "请输入服务器配置字符串：";
        public const string AddServerConfigFail = "添加服务器配置失败，请检查输入后重试";
        public const string DeleteServerConfigTemplate = "确定删除服务器 {0}？";
        public const string CoverServerConfig = "发现同名服务器, 本操作将覆盖这一服务器的地址设置, 是否继续？";

        public const string RecoverSessionFail = "会话已过期";

        public const string SelectedServerShouldNotBeEmpty = "请选择需要连接的服务器";
        public const string UsernameShouldNotBeEmpty = "用户名不能为空";
        public const string PasswordShouldNotBeEmpty = "密码不能为空";
        public const string InvitationCoShouldNotBeEmpty = "邀请码不能为空";
    }

    class LoginViewModel : BaseViewModel
    {
        private Model Model { get; } = Otokoneko.Client.Model.Instance;

        public Action CloseWindow { get; set; }

        public object SelectDetailViewModel { get; set; }
        public ObservableCollection<ServerConfig> Servers { get; set; }
        public bool ProgressRingEnable { get; set; }
        public bool OtherControlEnable { get; set; }

        private int _selectedComboBoxIndex = -1;
        public int SelectedComboBoxIndex
        {
            get => _selectedComboBoxIndex;
            set
            {
                _selectedComboBoxIndex = value;
                CheckSelectedComboBoxIndex();
            }
        }

        private readonly LoginDetail _loginDetail;
        private readonly RegisterDetail _registerDetail;

        public ICommand SwitchRegisterLoginCommand => new AsyncCommand(async () =>
        {
            SelectDetailViewModel = SelectDetailViewModel == _registerDetail ? (object)_loginDetail : _registerDetail;
            OnPropertyChanged(nameof(SelectDetailViewModel));
        });

        public ICommand LoginCommand => new AsyncCommand(async () =>
        {
            if (!CheckInput()) return;
            ProgressRingEnable = true;
            OtherControlEnable = false;
            OnPropertyChanged(nameof(ProgressRingEnable));
            OnPropertyChanged(nameof(OtherControlEnable));

            Model.Connect(Servers[SelectedComboBoxIndex].ServerId);

            bool result = false;
            string message = null;

            if (string.IsNullOrEmpty(_loginDetail.Password))
            {
                (result, message) = await Model.TryRecover(
                    Servers[SelectedComboBoxIndex].ServerId, 
                    _loginDetail.EnableAutoLogin,
                    10000);
                message ??= Constant.RecoverSessionFail;
                if (!result)
                {
                    Model.AutoLoginServerId = null;
                    Model.Sessions.Remove(Servers[SelectedComboBoxIndex].ServerId);
                    Model.SaveServerConfigs();
                    CheckSelectedComboBoxIndex();
                }
            }
            else
            {
                (result, message) = await Model.Login(
                    _loginDetail.Username,
                    _loginDetail.Password,
                    20000,
                    _loginDetail.EnableStoreToken,
                    _loginDetail.EnableAutoLogin);
            }

            if (!result) MessageBox.Show(message);
            else
            {
                var main = new MainWindow();
                main.Show();
                CloseWindow();
            }

            ProgressRingEnable = false;
            OtherControlEnable = true;
            OnPropertyChanged(nameof(ProgressRingEnable));
            OnPropertyChanged(nameof(OtherControlEnable));
        });

        public ICommand RegisterCommand => new AsyncCommand(async () =>
        {
            if (!CheckInput()) return;
            ProgressRingEnable = true;
            OtherControlEnable = false;
            OnPropertyChanged(nameof(ProgressRingEnable));
            OnPropertyChanged(nameof(OtherControlEnable));

            Model.Connect(Servers[SelectedComboBoxIndex].ServerId);
            var (result, message) = await Model.Register(
                _registerDetail.Username, 
                _registerDetail.Password,
                _registerDetail.InvitationCode,
                20000);

            MessageBox.Show(message);
            if (result == RegisterResult.Success)
            {
                SwitchRegisterLoginCommand.Execute(null);
            }

            ProgressRingEnable = false;
            OtherControlEnable = true;
            OnPropertyChanged(nameof(ProgressRingEnable));
            OnPropertyChanged(nameof(OtherControlEnable));
        });

        public ICommand CancelCommand => new AsyncCommand(async () =>
        {
            CloseWindow();
        });

        public ICommand AddServerCommand => new AsyncCommand(async () =>
        {
            AddNewServerConfig();
            RefreshServerConfigs();
        });

        public ICommand DeleteServerCommand => new AsyncCommand<ServerConfig>(async (serverConfig) =>
        {
            var result = MessageBox.Show(string.Format(Constant.DeleteServerConfigTemplate, serverConfig.ServerName),
                Constant.OperateNotice, MessageBoxButton.YesNo);
            if (result != MessageBoxResult.Yes) return;
            Model.ServerConfigs.Remove(serverConfig.ServerId);
            RefreshServerConfigs();
        });

        private void CheckSelectedComboBoxIndex()
        {
            if (SelectedComboBoxIndex >= 0 && SelectedComboBoxIndex < Servers.Count)
            {
                if (Model.Sessions.TryGetValue(Servers[SelectedComboBoxIndex].ServerId, out var session))
                {
                    _loginDetail.Username = session.Username;
                    _loginDetail.EnableStoreToken = true;
                    _loginDetail.PlaceHolderVisibility = Visibility.Visible;
                    return;
                }
            }
            _loginDetail.Username = null;
            _loginDetail.PlaceHolderVisibility = Visibility.Collapsed;
        }

        private void AddNewServerConfig()
        {

            var inputForm = new InputForm(Constant.AddServer, Constant.AddServerNotice, null);
            var res = inputForm.ShowDialog();
            if (res != true) return;
            try
            {
                var serverConfig = new ServerConfig(inputForm.InputMessage);
                if (Model.ServerConfigs.ContainsKey(serverConfig.ServerId))
                {
                    var result = MessageBox.Show(Constant.CoverServerConfig, Constant.OperateNotice, MessageBoxButton.YesNo);
                    if (result != MessageBoxResult.Yes) return;
                }

                Model.ServerConfigs[serverConfig.ServerId] = serverConfig;
                Model.SaveServerConfigs();
            }
            catch (Exception exception)
            {
                Trace.WriteLine(exception);
                MessageBox.Show(Constant.AddServerConfigFail);
            }
        }

        private void RefreshServerConfigs()
        {
            Servers = new ObservableCollection<ServerConfig>();
            OnPropertyChanged(nameof(Servers));
            Servers = new ObservableCollection<ServerConfig>(Model.ServerConfigs.Values);
            OnPropertyChanged(nameof(Servers));
            SelectedComboBoxIndex = -1;
            OnPropertyChanged(nameof(SelectedComboBoxIndex));
        }

        private bool CheckInput()
        {
            if (!(SelectedComboBoxIndex >= 0 && SelectedComboBoxIndex < Servers.Count))
            {
                MessageBox.Show(Constant.SelectedServerShouldNotBeEmpty);
                return false;
            }

            if (SelectDetailViewModel == _loginDetail)
            {
                if (string.IsNullOrEmpty(_loginDetail.Username?.Trim()))
                {
                    MessageBox.Show(Constant.UsernameShouldNotBeEmpty);
                    return false;
                }
                if (string.IsNullOrEmpty(_loginDetail.Password?.Trim()) && _loginDetail.PlaceHolderVisibility != Visibility.Visible)
                {
                    MessageBox.Show(Constant.PasswordShouldNotBeEmpty);
                    return false;
                }
            }

            if (SelectDetailViewModel == _registerDetail)
            {
                if (string.IsNullOrEmpty(_registerDetail.Username?.Trim()))
                {
                    MessageBox.Show(Constant.UsernameShouldNotBeEmpty);
                    return false;
                }
                if (string.IsNullOrEmpty(_registerDetail.Password?.Trim()))
                {
                    MessageBox.Show(Constant.PasswordShouldNotBeEmpty);
                    return false;
                }
                if (string.IsNullOrEmpty(_registerDetail.InvitationCode?.Trim()))
                {
                    MessageBox.Show(Constant.InvitationCoShouldNotBeEmpty);
                    return false;
                }
            }

            return true;
        }

        public LoginViewModel()
        {
            _loginDetail = new LoginDetail();
            _registerDetail = new RegisterDetail();
            SelectDetailViewModel = _loginDetail;
            ProgressRingEnable = false;
            OtherControlEnable = true;
        }

        public async ValueTask OnLoaded()
        {
            RefreshServerConfigs();
            if (Model.AutoLoginServerId != null)
            {
                SelectedComboBoxIndex =
                    Servers.IndexOf(Servers.FirstOrDefault(it => it.ServerId == Model.AutoLoginServerId));
                OnPropertyChanged(nameof(SelectedComboBoxIndex));
                _loginDetail.EnableAutoLogin = true;
                LoginCommand.Execute(null);
            }
        }
    }
}
