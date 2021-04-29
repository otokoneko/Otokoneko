#if CLIENT
using Google.Protobuf;
using MessagePack;
using Otokoneko.Base.Network;
using Otokoneko.DataType;
using Otokoneko.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;
using Message = Otokoneko.DataType.Message;

namespace Otokoneko.Client
{
    internal class RequestBuilder
    {
        public Request Request => new Request
        {
            Method = Method,
            Token = Token ?? "",
            Data = new Base.Network.Message
            {
                ObjectId = ObjectId,
                Offset = Offset,
                Data = Data != null ? ByteString.CopyFrom(Data) : ByteString.Empty
            }
        };

        public string Method { get; set; }
        public string Token { get; set; }
        public long ObjectId { get; set; }
        public int Offset { get; set; }
        public byte[] Data { get; set; }
    }

    [MessagePackObject]
    public class Session
    {
        [Key(0)]
        public ServerConfig ServerConfig { get; set; }
        [Key(1)]
        public string Token { get; set; }
        [Key(2)]
        public string Username { get; set; }
    }

    public static class Constant
    {
        public const string TimeoutMessage = "连接超时，请检查网络连接后重试";
        public const string LoginFail = "用户名或密码错误，请重试";

        public static string ConvertRegisterResult(RegisterResult result)
        {
            switch (result)
            {
                case DataType.RegisterResult.Unknown:
                    return null;
                case DataType.RegisterResult.Success:
                    return "注册成功";
                case DataType.RegisterResult.InvitationCodeNotFound:
                    return "未找到邀请码，请检查输入";
                case DataType.RegisterResult.InvitationCodeHasBeenUsed:
                    return "邀请码已被使用，请与管理员联系";
                case DataType.RegisterResult.UsernameRepeated:
                    return "用户名重复，请更换用户名";
                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, null);
            }
        }
    }

    public partial class Model
    {
        private const string DbPath = "./tmpDb";
        private const string MetadataDatabaseName = "metadata";

        private Client Client { get; set; }
        private string Token { get; set; }
        public Proxy Proxy { get; set; }

        private static readonly Lazy<Model> Lazy = new Lazy<Model>(() => new Model());

        #region Session

        public Dictionary<string, ServerConfig> ServerConfigs { get; set; }
        public Dictionary<string, Session> Sessions { get; set; }
        public string AutoLoginServerId { get; set; }
        private Session CurrentSession { get; set; }

        #endregion

        private LruCache ImageCache { get; }

        private readonly MessagePackSerializerOptions _lz4Option =
        MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block);

        private Setting _setting;
        public Setting Setting
        {
            get => _setting;
            set
            {
                _setting = value;
                SaveSetting();
            }
        }

        private ObservableCollection<string> _mangaSearchHistory;
        public ObservableCollection<string> MangaSearchHistory => _mangaSearchHistory;

        public ObservableCollection<TagType> TagTypes { get; }

        public int NumberOfUncheckedMessage { get; set; }
        public event EventHandler<int> NumberOfUncheckedMessageChanged;

        private Model()
        {
            TagTypes = new ObservableCollection<TagType>();
            Token = string.Empty;
            LoadServerConfigs();
            LoadSetting();
            ChangeTheme();
            ImageCache = new LruCache(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Otokoneko"), 10 * 1024 * 1024, 20 * 1024 * 1024);
        }

        public static Model Instance => Lazy.Value;


        #region ClientConfig

        private void LoadServerConfigs()
        {
            ServerConfigs = GetObjectFromDb<Dictionary<string, ServerConfig>>(nameof(ServerConfigs), MetadataDatabaseName) ??
                            new Dictionary<string, ServerConfig>();
            Sessions = GetObjectFromDb<Dictionary<string, Session>>(nameof(Sessions), MetadataDatabaseName) ??
                        new Dictionary<string, Session>();
            Proxy = GetObjectFromDb<Proxy>(nameof(Proxy), MetadataDatabaseName);
            AutoLoginServerId = GetObjectFromDb<string>(nameof(AutoLoginServerId), MetadataDatabaseName);
        }

        public void SaveServerConfigs()
        {
            PutObjectToDb(nameof(ServerConfigs), ServerConfigs, MetadataDatabaseName);
            PutObjectToDb(nameof(Proxy), Proxy, MetadataDatabaseName);
            PutObjectToDb(nameof(AutoLoginServerId), AutoLoginServerId, MetadataDatabaseName);
            PutObjectToDb(nameof(Sessions), Sessions, MetadataDatabaseName);
        }

        private void LoadSetting()
        {
            _setting = GetObjectFromDb<Setting>(nameof(Setting), MetadataDatabaseName) ?? new Setting();
        }

        private void SaveSetting()
        {
            PutObjectToDb(nameof(Setting), _setting, MetadataDatabaseName);
        }

        private void LoadServerData()
        {
            _mangaSearchHistory = (GetObjectFromDb<ObservableCollection<string>>(nameof(_mangaSearchHistory),
                                       CurrentSession?.ServerConfig?.ServerId) ??
                                   new ObservableCollection<string>());
        }

        #endregion

        #region Connect

        public void Connect(string serverId)
        {
            if (ServerConfigs[serverId] == CurrentSession?.ServerConfig) return;
            CurrentSession = new Session { ServerConfig = ServerConfigs[serverId] };
            LoadServerData();
            Start(CurrentSession.ServerConfig);
        }

        public void Disconnect()
        {
            Client?.Close();
        }

        public void Logout()
        {
            AutoLoginServerId = null;
            Sessions.Remove(CurrentSession.ServerConfig.ServerId);
            SaveServerConfigs();
            Disconnect();
        }

        private void Start(ServerConfig serverConfig)
        {
            Client?.Close();
            Client = new Client(serverConfig, ProcessServerNotify);
        }

        private async Task ProcessServerNotify(Response response)
        {
            var serverNotify = MessagePackSerializer.Deserialize<ServerNotify>(response.Data.Data.Memory, _lz4Option);
            switch (serverNotify)
            {
                case ServerNotify.Unknown:
                    break;
                case ServerNotify.NewMessageSent:
                    await CountMessageUnchecked();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        #endregion

        public async Task<Tuple<TResult, string>> SendRequest<T, TResult>(long objectId, T obj, int millisecondsTimeout = 60000, [CallerMemberName] string method = null)
        {
            var requestBuilder = new RequestBuilder
            {
                Method = method,
                Token = Token,
                ObjectId = objectId,
                Data = obj == null ? null : MessagePackSerializer.Serialize(obj, _lz4Option)
            };

            var cancellationTokenSource = new CancellationTokenSource(millisecondsTimeout);

            var taskCompletionSource = new TaskCompletionSource<Response>();
            await Client.Send(requestBuilder.Request, taskCompletionSource, millisecondsTimeout);
            try
            {
                var response = await taskCompletionSource.Task.WaitAsync(cancellationTokenSource.Token);
                var result = response.Data.Data != ByteString.Empty
                    ? MessagePackSerializer.Deserialize<TResult>(response.Data.Data.ToByteArray(), _lz4Option)
                    : default;
                return new Tuple<TResult, string>(result, default);
            }
            catch (TaskCanceledException)
            {
                return new Tuple<TResult, string>(default, Constant.TimeoutMessage);
            }
            catch (Exception e)
            {
                throw;
            }
        }

        #region SendRequestAboutUser

        public async ValueTask<Tuple<bool, string>> TryRecover(string serverId, bool autoLogin, int millisecondsTimeout)
        {
            CurrentSession = Sessions[serverId];
            Token = CurrentSession.Token;
            var (result, message) = await CheckAuthority(millisecondsTimeout);
            if (result && autoLogin)
            {
                AutoLoginServerId = serverId;
                SaveServerConfigs();
            }
            return new Tuple<bool, string>(result, message);
        }

        public async ValueTask<Tuple<RegisterResult, string>> Register(string username, string password, string invitationCode, int millisecondsTimeout)
        {
            var (result, message) = await SendRequest<UserHelper, RegisterResult>(
                0,
                new UserHelper()
                {
                    Name = username,
                    Password = PasswordHashProvider.CreateHash(username, password),
                    InvitationCode = invitationCode
                }, 
                millisecondsTimeout);
            return new Tuple<RegisterResult, string>(result, Constant.ConvertRegisterResult(result) ?? message);
        }

        private async ValueTask<Tuple<bool, string>> CheckAuthority(int millisecondsTimeout)
        {
            return await SendRequest<object, bool>(0, null, millisecondsTimeout);
        }

        public async ValueTask<Tuple<bool, string>> Login(string username, string password, int millisecondsTimeout, bool storeToken, bool autoLogin)
        {
            var (token, message) = await SendRequest<UserHelper, string>(
                0,
                new UserHelper
                {
                    Name = username,
                    Password = PasswordHashProvider.CreateHash(username, password),
                    SessionKeepTime = SessionKeepTime.OneMonth
                },
                millisecondsTimeout);
            var result = !(string.IsNullOrEmpty(token));
            if (!result) return new Tuple<bool, string>(false, message ?? Constant.LoginFail);
            Token = token;

            CurrentSession.Token = Token;
            CurrentSession.Username = username;
            
            if (storeToken)
            {
                Sessions[CurrentSession.ServerConfig.ServerId] = CurrentSession;
            }

            AutoLoginServerId = autoLogin ? CurrentSession.ServerConfig.ServerId : null;

            SaveServerConfigs();
            return new Tuple<bool, string>(true, message);
        }

        public async ValueTask<User> GetUserInfo()
        {
            var (result, message) = await SendRequest<object, User>(0, null);
            return result;
        }

        public async ValueTask<List<Invitation>> GetAllInvitations()
        {
            var (result, message) = await SendRequest<object, List<Invitation>>(0, null);
            return result;
        }

        public async ValueTask<bool> GenerateInvitation(UserAuthority authority)
        {
            var (result, message) = await SendRequest<UserAuthority, bool>(0, authority);
            return result;
        }

        public async ValueTask<List<User>> GetAllUsers()
        {
            var (result, message) = await SendRequest<object, List<User>>(0, null);
            return result;
        }

        public async ValueTask ChangeAuthority(User user)
        {
            var (result, message) = await SendRequest<User, object>(0, user);
        }

        #endregion

        #region SendRequestAboutLibrary

        public async ValueTask<List<FileTreeRoot>> GetLibraries()
        {
            var (result, status) = await SendRequest<object, List<FileTreeRoot>>(0, null);
            return result;
        }

        public async ValueTask<FileTreeRoot> GetLibrary(long objectId)
        {
            var (result, status) = await SendRequest<object, FileTreeRoot>(objectId, null);
            return result;
        }

        public async ValueTask<FileTreeRoot> AddLibrary(FileTreeRoot library)
        {
            var (result, status) = await SendRequest<FileTreeRoot, FileTreeRoot>(0, library);
            return result;
        }

        public async ValueTask<bool> UpdateLibrary(FileTreeRoot library)
        {
            var (result, status) = await SendRequest<FileTreeRoot, bool>(0, library);
            return result;
        }

        public async ValueTask ScanLibraryAndCreateMangaItems(long libraryId)
        {
            var (result, status) = await SendRequest<object, bool>(libraryId, null);
        }

        public async ValueTask DeleteLibrary(long libraryId)
        {
            var (result, status) = await SendRequest<object, bool>(libraryId, null);
        }

#endregion

        #region SendRequestAboutTag

        public async ValueTask<int> CountTags(string queryString, long typeId, int millisecondsTimeout = 10000)
        {
            var (result, status) = await SendRequest<TagQueryHelper, int>(
                0,
                new TagQueryHelper()
                {
                    QueryString = queryString,
                    TypeId = typeId
                });
            return result;
        }

        public async ValueTask<List<Tag>> ListTags(string queryString, long typeId, int offset, int limit, int millisecondsTimeout = 10000)
        {
            var (result, status) = await SendRequest<TagQueryHelper, List<Tag>>(
                0,
                new TagQueryHelper()
                {
                    QueryString = queryString,
                    Offset = offset,
                    Limit = limit,
                    TypeId = typeId
                });
            return result;
        }

        public async ValueTask ListTagTypes()
        {
            var (result, status) = await SendRequest<object, List<TagType>>(0, null);
            if (result != null)
            {
                foreach (var tagType in TagTypes.ToList())
                {
                    var newItem = result.FirstOrDefault(it => it.ObjectId == tagType.ObjectId);
                    if (newItem == null)
                    {
                        TagTypes.Remove(tagType);
                    }
                    else if(newItem.Name != tagType.Name)
                    {
                        TagTypes[TagTypes.IndexOf(tagType)] = newItem;
                    }
                }

                foreach (var tagType in result)
                {
                    if (TagTypes.Any(it => it.ObjectId == tagType.ObjectId)) continue;
                    TagTypes.Add(tagType);
                }
            }
        }

        public async ValueTask<long> AddTagType(string name)
        {
            var (result, status) = await SendRequest<TagType, long>(
                0,
                new TagType
                {
                    Name = name
                });
            return result;
        }

        public async ValueTask<bool> DeleteTagType(long typeId)
        {
            var (result, status) = await SendRequest<object, bool>(typeId, null);
            return result;
        }

        public async ValueTask<Tag> AddTag(Tag tag)
        {
            var (result, status) = await SendRequest<Tag, Tag>(0, tag);
            return result;
        }

        public async ValueTask<Tag> GetTag(long tagId)
        {
            var (result, status) = await SendRequest<object, Tag>(tagId, null);
            return result;
        }

        public async ValueTask<bool> UpdateTag(Tag tag)
        {
            var (result, status) = await SendRequest<Tag, bool>(0, tag);
            return result;
        }

        public async ValueTask<bool> DeleteTag(long tagId)
        {
            var (result, status) = await SendRequest<object, bool>(tagId, null);
            return result;
        }

#endregion

        #region SendRequestAboutManga

                public async ValueTask<int> CountMangas(MangaQueryHelper mangaQuery, int millisecondsTimeout = 10000)
                {
                    var (result, status) = await 
                        SendRequest<MangaQueryHelper, int>(
                        0,
                        mangaQuery, 
                        millisecondsTimeout);
                    return result;
                }

                public async ValueTask<List<Manga>> ListMangas(MangaQueryHelper mangaQuery, int millisecondsTimeout = 10000)
                {
                    var (result, status) = await
                        SendRequest<MangaQueryHelper, List<Manga>>(
                            0,
                            mangaQuery,
                            millisecondsTimeout);
                    return result;
                }

                public async ValueTask<Manga> GetManga(long mangaId)
                {
                    var (result, status) = await
                        SendRequest<object, Manga>(mangaId, null);
                    return result;
                }

                public async ValueTask<Chapter> GetChapter(long chapterId)
                {
                    var (result, status) = await
                        SendRequest<object, Chapter>(chapterId, null);
                    return result;
                }

                public async ValueTask<byte[]> GetImage(long imageId)
                {
                    if (await ImageCache.Contain(imageId))
                    {
                        return await ImageCache.Get(imageId);
                    }

                    // lock (_requestQueue)
                    // {
                    //     List<Action<byte[]>> callbacks;
                    //     if (_requestQueue.ContainsKey(imageId))
                    //     {
                    //         callbacks = _requestQueue[imageId];
                    //         lock (callbacks)
                    //         {
                    //             callbacks.Add(callback);
                    //         }
                    //         return;
                    //     }
                    //     callbacks = new List<Action<byte[]>>();
                    //     _requestQueue.Add(imageId, callbacks);
                    //     lock (callbacks)
                    //     {
                    //         callbacks.Add(callback);
                    //     }
                    // }

                    var (result, status) = await SendRequest<object, byte[]>(imageId, null);
                    if (result != null)
                    {
                        await ImageCache.Add(imageId, result);
                    }

                    return result;
                }

                public async ValueTask<bool> DeleteManga(long mangaId, int millisecondsTimeout = 10000)
                {
                    var (result, status) = await SendRequest<object, bool>(mangaId, null, millisecondsTimeout);
                    return result;
                }

                public async ValueTask<bool> UpdateManga(Manga manga, int millisecondsTimeout = 10000)
                {
                    var (result, status) = await SendRequest<object, bool>(0, manga, millisecondsTimeout);
                    return result;
                }

                public async ValueTask<bool> RecordReadProgress(long chapterId, int progress, int millisecondsTimeout = 10000)
                {
                    var (result, status) = await SendRequest<object, bool>(
                        0,
                        new ReadProgress
                        {
                            ChapterId = chapterId,
                            Progress = progress
                        },
                        millisecondsTimeout);
                    return result;
                }

                public async ValueTask<ReadProgress> GetChapterReadProgress(long chapterId)
                {
                    var (result, status) = await SendRequest<object, ReadProgress>(chapterId, null);
                    return result;
                }

                public async ValueTask AddMangaFavorite(long mangaId)
                {
                    var (result, status) = await SendRequest<object, bool>(mangaId, null);
                }

                public async ValueTask RemoveMangaFavorite(long mangaId)
                {
                    var (result, status) = await SendRequest<object, bool>(mangaId, null);
                }

                public async ValueTask AddComment(Comment comment)
                {
                    var (result, status) = await SendRequest<Comment, bool>(0, comment);
                }

        #endregion

        #region SendRequestAboutScheduleTask

        public async ValueTask<List<DisplayTask>> GetSubScheduleTasks(long parentId)
        {
            var (result, status) = await SendRequest<object, List<DisplayTask>>(parentId, null);
            return result;
        }

        public async ValueTask<List<Plan>> GetPlans()
        {
            var (result, status) = await SendRequest<object, List<Plan>>(0, null);
            
            return result;
        }

        public async ValueTask<Plan> GetPlan(long planId)
        {
            var (result, status) = await SendRequest<object, Plan>(planId, null);
            return result;
        }

        public async ValueTask AddPlan(Plan plan)
        {
            var (result, status) = await SendRequest<Plan, bool>(0, plan);
        }

        public async ValueTask UpdatePlan(Plan plan)
        {
            var (result, status) = await SendRequest<Plan, bool>(0, plan);
        }

        public async ValueTask RemovePlan(long planId)
        {
            var (result, status) = await SendRequest<object, bool>(planId, null);
        }

        public async ValueTask TriggerPlan(long planId)
        {
            var (result, status) = await SendRequest<object, bool>(planId, null);
        }

        public async ValueTask RestartScheduleTask(long objectId)
        {
            var (result, status) = await SendRequest<object, bool>(objectId, null);
        }

        public async ValueTask RemoveScheduleTask(long objectId)
        {
            var (result, status) = await SendRequest<object, bool>(objectId, null);
        }

        #endregion

        #region SendRequestAboutMessage

        public async ValueTask SubscribeMessage()
        {
            var (result, status) = await SendRequest<object, int>(0, null);
        }

        public async ValueTask<int> CountMessageUnchecked()
        {
            var (result, status) = await SendRequest<object, int>(0, null);
            NumberOfUncheckedMessage = result;
            NumberOfUncheckedMessageChanged?.Invoke(this, result);
            return result;
        }

        public async ValueTask<List<Message>> GetMessages()
        {
            var (result, status) = await SendRequest<object, List<Message>>(0, null);
            return result;
        }

        public async ValueTask ClearCheckedMessage()
        {
            var (result, status) = await SendRequest<object, object>(0, null);
        }

        public async ValueTask<bool> Check(List<long> messageIds)
        {
            var (result, status) = await SendRequest<List<long>, bool>(0, messageIds);
            return result;
        }

        #endregion

        #region SendRequestAboutPlugin

        public async ValueTask<List<PluginDetail>> GetPluginDetails()
        {
            var (result, status) = await SendRequest<object, List<PluginDetail>>(0, null);
            return result;
        }

        public async ValueTask<PluginDetail> GetPluginDetail(string pluginType)
        {
            var (result, status) = await SendRequest<string, PluginDetail>(0, pluginType);
            return result;
        }

        public async ValueTask<bool> SetPluginParameters(PluginDetail detail)
        {
            var (result, status) = await SendRequest<PluginDetail, bool>(0, detail);
            return result;
        }

        public async ValueTask<PluginDetail> ResetPluginParameters(string pluginType)
        {
            var (result, status) = await SendRequest<string, PluginDetail>(0, pluginType);
            return result;
        }

        public async ValueTask<List<string>> GetScrapers()
        {
            var (result, status) = await SendRequest<object, List<string>>(0, null);
            return result;
        }

        #endregion

        public void AddMangaSearchHistory(string keywords)
        {
            if (_mangaSearchHistory.Contains(keywords))
            {
                _mangaSearchHistory.Remove(keywords);
            }
            _mangaSearchHistory.Insert(0, keywords);

            PutObjectToDb(nameof(_mangaSearchHistory), _mangaSearchHistory, CurrentSession.ServerConfig.ServerId);
        }

        public void RemoveMangaSearchHistory(string keywords)
        {
            _mangaSearchHistory.Remove(keywords);

            PutObjectToDb(nameof(_mangaSearchHistory), _mangaSearchHistory, CurrentSession.ServerConfig.ServerId);
        }
    }
}
#endif