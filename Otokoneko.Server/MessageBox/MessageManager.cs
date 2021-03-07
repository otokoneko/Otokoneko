using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdGen;
using Otokoneko.DataType;
using Otokoneko.Server.UserManage;
using SqlSugar;

namespace Otokoneko.Server.MessageBox
{
    public class MessageManager
    {
        private static IdGenerator IdGenerator { get; } = new IdGenerator(5);
        private static readonly string DbConnectionString = @"Data Source=./data/message.db;";
        private SqlSugarClient Context => CreateContext(DbConnectionString);
        public Func<string, SqlSugarClient> CreateContext { get; }
        public UserManager UserManager { get; set; }

        public event EventHandler<Message> MessageSent;

        public MessageManager(Func<string, SqlSugarClient> createContext)
        {
            CreateContext = createContext;
            Context.CodeFirst.InitTables(
                typeof(Message),
                typeof(MessageUserMapping));
        }

        public async ValueTask<bool> Send(Message message, HashSet<UserAuthority> receiverGroups)
        {
            var receivers = receiverGroups.SelectMany(it => UserManager.GetUserIds(it)).ToHashSet();
            return await Send(message, receivers);
        }

        public async ValueTask<bool> Send(Message message, HashSet<long> receivers)
        {
            using var context = Context;
            message.ObjectId = IdGenerator.CreateId();
            message.CreateUtcTime = DateTime.UtcNow;
            message.Receivers = receivers.ToList();

            var messageBox = new MessageBox(context);
            context.BeginTran();
            try
            {
                if (!await messageBox.AddMessage(message) ||
                    !await messageBox.SendMessage(message.ObjectId, message.Receivers))
                {
                    context.RollbackTran();
                    return false;
                }
                context.CommitTran();
                MessageSent?.Invoke(this, message);
                return true;
            }
            catch (Exception e)
            {
                context.RollbackTran();
                throw;
            }
        }

        public async ValueTask<int> CountUncheckedMessage(long userId)
        {
            using var context = Context;
            var messageBox = new MessageBox(context);
            return await messageBox.CountUncheckedMessage(userId);
        }

        public async ValueTask<List<Message>> GetMessages(long userId)
        {
            using var context = Context;
            var messageBox = new MessageBox(context);
            return await messageBox.GetMessages(userId);
        }

        public async ValueTask<bool> Check(long messageId, long userId)
        {
            using var context = Context;
            var messageBox = new MessageBox(context);
            return await messageBox.Check(messageId, userId);
        }

        public async ValueTask ClearCheckedMessage(long userId)
        {
            using var context = Context;
            var messageBox = new MessageBox(context);
            await messageBox.ClearCheckedMessage(userId);
        }
    }

    public static class MessageTemplate
    {
        public const string MangaUpdateMessage = "您收藏的漫画 {0} 更新了新章节 {0}。";
        public const string LibraryScanResultMessage = "扫描任务 {0} 完成，预计新建漫画 {1} 本，更新漫画 {2} 本。";
        public const string LibraryScanExceptionMessage = "扫描任务 {0} 失败，异常原因 {1}。";
        public const string DownloadExceptionMessage = "下载 {0} 失败，异常原因 {1}。";
        public const string PlanTriggeredMessage = "计划 {0} 触发，成功添加任务 {1} 个。";
    }
}
