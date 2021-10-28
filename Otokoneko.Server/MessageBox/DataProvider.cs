using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Otokoneko.DataType;
using SqlSugar;

namespace Otokoneko.Server.MessageBox
{
    public class MessageBox
    {
        public SqlSugarClient Context { get; }

        public MessageBox(SqlSugarClient context)
        {
            Context = context;
        }

        public async ValueTask ClearCheckedMessage(long userId)
        {
            await Context.Deleteable<MessageUserMapping>()
                .Where(it => it.ReceiverId == userId && it.Checked)
                .ExecuteCommandAsync();
            await Context.Deleteable<Message>()
                .Where(it =>
                    SqlFunc.Subqueryable<MessageUserMapping>().Where(mapping => mapping.MessageId == it.ObjectId)
                        .NotAny())
                .ExecuteCommandAsync();
        }

        public async ValueTask<List<Message>> GetMessages(long userId)
        {
            return await Context.Queryable<Message, MessageUserMapping>((message, mapping) =>
                    new JoinQueryInfos(
                        JoinType.Inner, mapping.MessageId == message.ObjectId))
                .Where((message, mapping) => mapping.ReceiverId == userId)
                .OrderBy((message, mapping) => message.CreateUtcTime, OrderByType.Desc)
                .Select((message, mapping) => new Message()
                {
                    ObjectId = message.ObjectId,
                    Data = message.Data,
                    CreateUtcTime = message.CreateUtcTime,
                    Checked = mapping.Checked,
                    SenderId = message.SenderId
                })
                .ToListAsync();
        }

        public async ValueTask<int> CountUncheckedMessage(long userId)
        {
            return await Context.Queryable<Message, MessageUserMapping>((message, mapping) =>
                    new JoinQueryInfos(
                        JoinType.Inner, mapping.MessageId == message.ObjectId))
                .Where((message, mapping) => mapping.ReceiverId == userId && mapping.Checked == false)
                .CountAsync();
        }

        public async ValueTask<bool> CheckMessages(List<long> messageIds, long userId)
        {
            return await Context.Updateable<MessageUserMapping>()
                .Where(it => it.ReceiverId == userId && messageIds.Contains(it.MessageId))
                .SetColumns(it => it.Checked == true)
                .ExecuteCommandAsync() == messageIds.Count;
        }

        public async ValueTask<bool> AddMessage(Message message)
        {
            return await Context.Insertable(message)
                .ExecuteCommandAsync() == 1;
        }

        public async ValueTask<bool> SendMessage(long messageId, List<long> receiverIds)
        {
            return await Context.Insertable(receiverIds.Select(it => new MessageUserMapping()
                {
                    Checked = false, 
                    MessageId = messageId, 
                    ReceiverId = it
                }).ToList())
                .ExecuteCommandAsync() == receiverIds.Count;
        }
    }
}