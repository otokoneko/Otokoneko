using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using IdGen;
using log4net;
using Otokoneko.DataType;
using Otokoneko.Server.Utils;
using Otokoneko.Utils;
using SqlSugar;

namespace Otokoneko.Server.UserManage
{
    public class Session
    {
        [SugarColumn(IsIgnore = true)]
        public User User { get; set; }
        [SugarColumn(IsPrimaryKey = true)]
        public string Token { get; set; }
        public long UserId { get; set; }
        public DateTime ExpireTime { get; set; }
    }

    public class UserManager
    {
        private readonly ConcurrentDictionary<string, Session> _sessions;
        private static readonly IdGenerator IdGenerator = new IdGenerator(2);

        private static readonly string DbConnectionString = @"Data Source=./data/user.db;";
        public Func<string, SqlSugarClient> CreateContext { get; set; }
        private SqlSugarClient Context => CreateContext(DbConnectionString);

        public UserManager(Func<string, SqlSugarClient> createContext, ILog logger)
        {
            logger.Info("加载用户信息...");
            CreateContext = createContext;
            _sessions = new ConcurrentDictionary<string, Session>();
            Context.CodeFirst.InitTables(typeof(User), typeof(Invitation), typeof(Session));
            Recover();
            Task.Run(RemoveExpireSessions);
        }

        private void Recover()
        {
            var context = Context;
            var sessionService = new SessionService(context);
            var sessions = sessionService.GetSessions(it => it.ExpireTime > DateTime.UtcNow);
            foreach (var session in sessions)
            {
                _sessions.TryAdd(session.Token, session);
            }
        }

        #region UserManage

        public async ValueTask<RegisterResult> Register(string name, string password, string invitationCode)
        {
            var context = Context;
            try
            {
                context.BeginTran();

                var invitationService = new InvitationService(context);
                var invitation = await invitationService.
                    GetSingleAsync(it => it.InvitationCode == invitationCode);

                if (invitation == null)
                {
                    context.RollbackTran();
                    return RegisterResult.InvitationCodeNotFound;
                }

                if (invitation.ReceiverId > 0)
                {
                    context.RollbackTran();
                    return RegisterResult.InvitationCodeHasBeenUsed;
                }

                var salt = EncryptUtils.GenerateRandomBytes(64);
                password = PasswordHashProvider.CreateHash(salt, password, 16);
                var user = new User
                {
                    Id = IdGenerator.CreateId(),
                    Name = name,
                    Password = password,
                    Salt = salt,
                    Authority = invitation.Authority
                };
                var userService = new UserService(context);
                var result = userService.Insert(user);
                if (result.ErrorList.Count != 0)
                {
                    context.RollbackTran();
                    return RegisterResult.UsernameRepeated;
                }

                var success = await invitationService.UpdateAsync(
                    it => new Invitation() {UsedTime = DateTime.UtcNow, ReceiverId = user.Id},
                    it => it.ObjectId == invitation.ObjectId);

                if (!success)
                {
                    context.RollbackTran();
                    return RegisterResult.InvitationCodeHasBeenUsed;
                }

                context.CommitTran();
                return RegisterResult.Success;
            }
            catch
            {
                context.RollbackTran();
                throw;
            }
        }

        public async ValueTask<string> Login(string name, string password, DateTime expireTime)
        {
            var context = Context;
            var userService = new UserService(context);
            var user = await userService.GetSingleAsync(it => it.Name == name);
            if (user == null) return null;

            password = PasswordHashProvider.CreateHash(user.Salt, password, 16);
            if (!EncryptUtils.ComparePassword(user.Password, password)) return null;

            var token = EncryptUtils.GenerateRandomString(32);
            return await AddSession(token, user, expireTime, context) ? token : null;
        }

        private async ValueTask<bool> AddSession(string token, User user, DateTime expireTime, SqlSugarClient context)
        {
            var session = new Session()
            {
                Token = token,
                ExpireTime = expireTime,
                User = user,
                UserId = user.Id
            };
            if (!_sessions.TryAdd(token, session)) return false;
            var sessionService = new SessionService(context);
            return await sessionService.InsertAsync(session);
        }

        private async Task RemoveExpireSessions()
        {
            while (true)
            {
                for (int i = 0; i < 100; i++)
                {
                    foreach (var session in _sessions.Values)
                    {
                        if (session.ExpireTime > DateTime.UtcNow) continue;
                        _sessions.TryRemove(session.Token, out _);
                    }

                    await Task.Delay(30000);
                }

                var context = Context;
                var sessionService = new SessionService(context);
                context.BeginTran();
                await sessionService.DeleteAsync(it => it.ExpireTime < DateTime.UtcNow);
                context.CommitTran();
            }
        }

        public async ValueTask UpdateAuthority(User user)
        {
            if (GetUserIds(UserAuthority.Root).Contains(user.Id)) return;
            var context = Context;
            var userService = new UserService(context);
            await userService.UpdateAuthority(user);
        }

        #endregion

        #region CheckAuthority

        public User GetUserByToken(string token)
        {
            _sessions.TryGetValue(token, out var session);
            return session?.User;
        }

        private UserAuthority GetAuthority(string token)
        {
            return GetUserByToken(token)?.Authority ?? UserAuthority.Visitor;
        }

        public bool CheckAuthority(string token, UserAuthority authority)
        {
            return GetAuthority(token) <= authority;
        }

        public long GetUserId(string token)
        {
            return GetUserByToken(token)?.Id ?? -1;
        }

        #endregion

        #region GenerateInvitationCode

        public async ValueTask<string> GenerateInvitationCode(long senderId, UserAuthority authority)
        {
            var context = Context;
            var userService = new UserService(context);
            var user = await userService.GetSingleAsync(it => it.Id == senderId);
            if (user == null || user.Authority >= authority) return null;
            return await GenerateInvitationCode(context, senderId, authority);
        }

        private async ValueTask<string> GenerateInvitationCode(SqlSugarClient context, long senderId, UserAuthority authority)
        {
            try
            {
                context.BeginTran();
                var invitationService = new InvitationService(context);
                var invitation = new Invitation()
                {
                    ObjectId = IdGenerator.CreateId(),
                    Authority = authority,
                    CreateTime = DateTime.UtcNow,
                    InvitationCode = EncryptUtils.GenerateRandomString(32),
                    ReceiverId = -1,
                    SenderId = senderId,
                    UsedTime = DateTime.MinValue
                };
                var success = await invitationService.InsertAsync(invitation);
                context.CommitTran();
                return success ? invitation.InvitationCode : null;
            }
            catch (Exception e)
            {
                context.RollbackTran();
                throw;
            }
        }

        public async ValueTask<string> CreateRootUser()
        {
            var context = Context;
            var invitationService = new InvitationService(context);
            var userService = new UserService(context);
            if (await userService.CountAsync(it => true) == 0 &&
                await invitationService.CountAsync(it => true) == 0)
            {
                var invitationCode = await GenerateInvitationCode(context, -1, UserAuthority.Root);
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("\nRoot用户邀请码:");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(invitationCode);
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine();
            }
            return null;
        }

        #endregion

        public ValueTask<User> GetUser(long userId)
        {
            var context = Context;
            var userService = new UserService(context);
            return userService.GetUserWithoutPassword(it => it.Id == userId);
        }

        public ValueTask<List<Invitation>> GetAllInvitations()
        {
            var context = Context;
            var invitationService = new InvitationService(context);
            return invitationService.GetAllInvitations();
        }

        public ValueTask<List<User>> GetAllUsers()
        {
            var context = Context;
            var userService = new UserService(context);
            return userService.GetAllUserWithoutPassword();
        }

        public List<long> GetUserIds(UserAuthority authority)
        {
            var context = Context;
            var userService = new UserService(context);
            return userService.GetUserIds(it => it.Authority == authority);
        }
    }
}