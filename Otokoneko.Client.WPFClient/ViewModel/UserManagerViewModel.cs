using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Otokoneko.DataType;

namespace Otokoneko.Client.WPFClient.ViewModel
{
    public static partial class Constant
    {
        public const string GenerateInvitationSuccess = "生成邀请码成功";
        public const string GenerateInvitationFail = "生成邀请码失败";
    }

    class UserManagerViewModel : BaseViewModel
    {
        public Action CloseWindow { get; set; }

        public User User { get; set; }

        public ObservableCollection<Invitation> Invitations { get; set; }

        public ObservableCollection<User> Users { get; set; }

        public ICommand LogoutCommand => new AsyncCommand(async () =>
        {
            Model.Logout();
            CloseWindow();
        });

        public ICommand GenerateAdminInvitationCommand => new AsyncCommand(async () =>
        {
            var success = await Model.GenerateInvitation(UserAuthority.Admin);
            MessageBox.Show(success ? Constant.GenerateInvitationSuccess : Constant.GenerateInvitationFail);
            if (!success) return;
            await LoadInvitations();
        });

        public ICommand GenerateUserInvitationCommand => new AsyncCommand(async () =>
        {
            var success = await Model.GenerateInvitation(UserAuthority.User);
            MessageBox.Show(success ? Constant.GenerateInvitationSuccess : Constant.GenerateInvitationFail);
            if (!success) return;
            await LoadInvitations();
        });

        public ICommand BanUserCommand => new AsyncCommand<User>(async (user) =>
        {
            user.Authority = UserAuthority.Banned;
            await Model.ChangeAuthority(user);
            await LoadUsers();
        });

        public ICommand ChangeAuthorityAsUserCommand => new AsyncCommand<User>(async (user) =>
        {
            user.Authority = UserAuthority.User;
            await Model.ChangeAuthority(user);
            await LoadUsers();
        });

        public ICommand ChangeAuthorityAsAdminCommand => new AsyncCommand<User>(async (user) =>
        {
            user.Authority = UserAuthority.Admin;
            await Model.ChangeAuthority(user);
            await LoadUsers();
        });

        private async Task LoadUser()
        {
            User = await Model.GetUserInfo();
            OnPropertyChanged(nameof(User));
        }

        private async Task LoadInvitations()
        {
            var invitations = await Model.GetAllInvitations();
            if (invitations != null)
            {
                Invitations = new ObservableCollection<Invitation>(invitations);
                OnPropertyChanged(nameof(Invitations));
            }
        }

        private async Task LoadUsers()
        {
            var users = await Model.GetAllUsers();
            if (users != null)
            {
                Users = new ObservableCollection<User>(users);
                OnPropertyChanged(nameof(Users));
            }
        }

        public async ValueTask OnLoaded()
        {
            LoadUser();
            LoadInvitations();
            LoadUsers();
        }
    }
}
