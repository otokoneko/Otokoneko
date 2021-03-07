using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Otokoneko.DataType;
using SqlSugar;

namespace Otokoneko.Server.UserManage
{
    public class UserService : SimpleClient<User>
    {
        public UserService(ISqlSugarClient context) : base(context) { }

        public new StorageableResult<User> Insert(User user)
        {
            var result = Context.Storageable(user)
                .SplitError(it => it.Any(u => u.Name == it.Item.Name))
                .SplitInsert(it => true)
                .WhereColumns(it => it.Name)
                .ToStorage();

            if (result.InsertList.Count != 0)
            {
                result.AsInsertable.ExecuteCommand();
            }

            return result;
        }

        public List<long> GetUserIds(Expression<Func<User, bool>> whereExpression)
        {
            return Context.Queryable<User>().Where(whereExpression).Select(it => it.Id).ToList();
        }

        public async ValueTask<User> GetUserWithoutPassword(Expression<Func<User, bool>> whereExpression)
        {
            return await Context.Queryable<User>()
                .Where(whereExpression)
                .Select(it => new User()
                {
                    Id = it.Id,
                    Authority = it.Authority,
                    Name = it.Name
                })
                .SingleAsync();
        }

        public async ValueTask<List<User>> GetAllUserWithoutPassword()
        {
            return await Context.Queryable<User>()
                .Select(it => new User()
                {
                    Id = it.Id,
                    Authority = it.Authority,
                    Name = it.Name
                })
                .ToListAsync();
        }

        public async ValueTask UpdateAuthority(User user)
        {
            await Context.Updateable<User>()
                .Where(it => it.Id == user.Id)
                .SetColumns(it => it.Authority == user.Authority)
                .ExecuteCommandAsync();
        }
    }

    public class InvitationService : SimpleClient<Invitation>
    {
        public InvitationService(ISqlSugarClient context) : base(context) { }

        public async ValueTask<List<Invitation>> GetAllInvitations()
        {
            return await Context.Queryable<Invitation>()
                .Mapper(it => it.Sender, it => it.Sender.Id, it => it.SenderId)
                .Mapper(it => it.Receiver, it => it.Receiver.Id, it => it.ReceiverId)
                .ToListAsync();
        }
    }

    public class SessionService : SimpleClient<Session>
    {
        public SessionService(ISqlSugarClient context) : base(context) { }

        public List<Session> GetSessions(Expression<Func<Session, bool>> whereExpression)
        {
            return Context.Queryable<Session>()
                .Where(whereExpression)
                .Mapper(it => it.User, it => it.UserId, it => it.User.Id)
                .ToList();
        }
    }
}