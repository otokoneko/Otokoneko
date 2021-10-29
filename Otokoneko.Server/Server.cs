using Google.Protobuf;
using MessagePack;
using Otokoneko.DataType;
using Otokoneko.Server.MangaManage;
using Otokoneko.Server.ScheduleTaskManage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using log4net;
using Otokoneko.Base.Network;
using Otokoneko.Server.Config;
using Otokoneko.Server.MessageBox;
using Otokoneko.Server.PluginManage;
using Otokoneko.Server.SearchService;
using Otokoneko.Server.UserManage;
using SuperSocket;
using Message = Otokoneko.DataType.Message;
using ServerConfig = Otokoneko.Server.Config.ServerConfig;
using Otokoneko.Server.Utils;
using System.Buffers;
using System.Diagnostics;

namespace Otokoneko.Server
{
    [AttributeUsage(AttributeTargets.Method)]
    public class RequestProcessMethodAttribute : Attribute
    {
        public int NumberOfExtraParameters { get; }
        public int NumberOfParameters { get; set; }
        public UserAuthority RequiredAuthority { get; }
        public bool RequestUserId { get; }
        public bool RequestObjectId { get; }
        public bool RequestSession { get; }
        public MethodInfo Method { get; set; }
        public Type MethodParameterType { get; set; }

        public RequestProcessMethodAttribute(UserAuthority requiredAuthority, bool requestUserId = false, bool requestObjectId = false, bool requestSession = false)
        {
            NumberOfExtraParameters = 0;
            RequiredAuthority = requiredAuthority;
            RequestUserId = requestUserId;
            NumberOfExtraParameters += (requestUserId ? 1 : 0);
            RequestObjectId = requestObjectId;
            NumberOfExtraParameters += (requestObjectId ? 1 : 0);
            RequestSession = requestSession;
            NumberOfExtraParameters += (requestSession ? 1 : 0);
        }
    }

    public partial class Server
    {
        public IConsoleColorConfig ConsoleColorSetter { get; set; }
        public ILog Logger { get; set; }

        private static ArrayPool<byte> BufferPool => ArrayPool<byte>.Shared;
        public MangaManager MangaManager { get; set; }
        public Scheduler Scheduler { get; set; }
        public UserManager UserManager { get; set; }
        public LibraryManager LibraryManager { get; set; }
        public PlanManager PlanManager { get; set; }
        public MessageManager MessageManager { get; set; }
        public PluginManager PluginManager { get; set; }

        public FavoriteMangaSearchService FavoriteMangaSearchService { get; set; }
        public MangaKeywordSearchService MangaKeywordSearchService { get; set; }
        public MangaReadHistorySearchService MangaReadHistorySearchService { get; set; }
        public TagKeywordSearchService TagKeywordSearchService { get; set; }
        public MangaFtsIndexService MangaFtsIndexService { get; set; }
        public TagFtsIndexService TagFtsIndexService { get; set; }

        public string Id { get; private set; }
        public string Name { get; private set; }
        public ushort Port { get; private set; }
        public string CertificateHash { get; private set; }
        private X509Certificate _certificate;
        private List<ServerConfig> ServerConfigs { get; set; }
        
        private static MessagePackSerializerOptions SerializerOptions { get; } =
            MessagePackSerializerOptions.Standard
                .WithCompression(MessagePackCompression.Lz4Block)
                .WithSecurity(MessagePackSecurity.UntrustedData);

        private readonly Dictionary<string, RequestProcessMethodAttribute> _requestProcessMethodDictionary;
        private readonly Dictionary<long, object> _mutexes;
        private readonly Dictionary<long, HashSet<IAppSession>> _subscribers;

        public Server(ConfigLoader configLoader, MessageManager messageManager)
        {
            LoadConfig(configLoader);
            _requestProcessMethodDictionary = GetRequestProcessMethodDictionary();
            _mutexes = new Dictionary<long, object>();
            _subscribers = new Dictionary<long, HashSet<IAppSession>>();
            messageManager.MessageSent += MessageManagerOnMessageSent;
            CreateHost(Port, _certificate);
        }

        private async ValueTask<object> ProcessRequest(Request request, IAppSession session)
        {
            if (_requestProcessMethodDictionary.TryGetValue(request.Method, out var attribute))
            {
                try
                {
                    var sw = new Stopwatch();
                    sw.Start();

                    if (!UserManager.CheckAuthority(request.Token, attribute.RequiredAuthority))
                    {
                        Logger.Debug($"[{session.SessionID}] [{UserManager.GetUserByToken(request.Token)?.Name}] {request.Method} {ResponseStatus.Forbidden}");
                        return CreateResponse(request, ResponseStatus.Forbidden);
                    }
                    var parameters = new List<object>();

                    if (attribute.MethodParameterType != null)
                    {
                        parameters.Add(MessagePackSerializer.Deserialize(attribute.MethodParameterType,
                            request.Data.Data.Memory, SerializerOptions));
                    }

                    if (attribute.RequestUserId)
                    {
                        parameters.Add(UserManager.GetUserId(request.Token));
                    }

                    if (attribute.RequestObjectId)
                    {
                        parameters.Add(request.Data.ObjectId);
                    }

                    if (attribute.RequestSession)
                    {
                        parameters.Add(session);
                    }

                    var returnValue = attribute.Method.Invoke(this, parameters.ToArray());

                    if (returnValue is ValueTask<Tuple<ResponseStatus, object>> resp)
                    {
                        var (status, result) = await resp;
                        sw.Stop();

                        Logger.Debug($"[{session.SessionID}] [{UserManager.GetUserByToken(request.Token)?.Name}] [{sw.ElapsedMilliseconds}ms] {request.Method} {status}");
                        
                        if(result is IAsyncEnumerable<object> streamResult)
                        {
                            return new Responses(streamResult,
                            (o, b) => CreateResponse(request, status, o, b));
                        }
                        
                        return CreateResponse(request, status, result);
                    }
                }
                catch (Exception e)
                {
                    Logger.Warn(e);
                }
            }

            Logger.Debug($"[{session.SessionID}] [{UserManager.GetUserByToken(request.Token)?.Name}] {request.Method} {ResponseStatus.BadRequest}");
            return new Response
            {
                Id = request.Id,
                Completed = true,
                Data = new Base.Network.Message()
                {
                    ObjectId = 0,
                    Offset = 0
                },
                Status = ResponseStatus.BadRequest
            };
        }

        private static Response CreateResponse(Request request, ResponseStatus status, object data = null, bool completed = true)
        {
            var response  = new Response
            {
                Id = request.Id,
                Completed = completed,
                Status = status,
                Data = new Base.Network.Message()
                {
                    ObjectId = 0,
                    Offset = 0,
                }
            };
            if (data is Plan plan)
            {
                response.Data.Data = ByteString.CopyFrom(MessagePackSerializer.Serialize(plan, SerializerOptions));
            }
            //else if(data is Stream stream)
            //{
            //    var temp = BufferPool.Rent((int)stream.Length);
            //    stream.Read(temp, 0, temp.Length);
            //    response.Data.Data = ByteString.CopyFrom(MessagePackSerializer.Serialize(temp, SerializerOptions));
            //    BufferPool.Return(temp);
            //}
            else
            {
                response.Data.Data = (data != null
                    ? ByteString.CopyFrom(MessagePackSerializer.Serialize(data, SerializerOptions))
                    : ByteString.Empty);
            }

            return response;
        }

        private void LoadConfig(ConfigLoader configLoader)
        {
            Id = configLoader.Config.Id;
            Name = configLoader.Config.Name;
            Port = configLoader.Config.Port;
            ServerConfigs = configLoader.Config.Servers;
            var certificatePath = configLoader.Config.Certificate.Path;
            var certificatePassword = configLoader.Config.Certificate.Password;
            _certificate = CertificateUtils.GetCertificate(certificatePath, certificatePassword);
            CertificateHash = _certificate.GetCertHashString();
        }

        private Dictionary<string, RequestProcessMethodAttribute> GetRequestProcessMethodDictionary()
        {
            var methods = (from method in GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                           .Where(e => e.GetCustomAttribute<RequestProcessMethodAttribute>() != null)
                           select method).ToList();

            var results = new Dictionary<string, RequestProcessMethodAttribute>();
            foreach (var method in methods)
            {
                var attribute = method.GetCustomAttribute<RequestProcessMethodAttribute>();
                if (attribute == null) continue;
                var parameters = method.GetParameters();

                if (parameters.Length > attribute.NumberOfExtraParameters + 1)
                {
                    throw new TargetParameterCountException(
                        $"{nameof(RequestProcessMethodAttribute)} not support method with more than {attribute.NumberOfExtraParameters + 1} parameter.");
                }

                attribute.MethodParameterType = 
                    parameters.Length == attribute.NumberOfExtraParameters
                        ? null
                        : parameters.First().ParameterType;
                attribute.NumberOfParameters = parameters.Length;
                attribute.Method = method;
                results.Add(method.Name, attribute);
            }

            return results;
        }

        #region ProcessRequestAboutUser

        [RequestProcessMethod(UserAuthority.User)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> CheckAuthority()
        {
            return new Tuple<ResponseStatus, object>(
                ResponseStatus.Success,
                true);
        }

        [RequestProcessMethod(UserAuthority.Visitor)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> Login(UserHelper userHelper)
        {
            var expireTime = userHelper.SessionKeepTime switch
            {
                SessionKeepTime.OneDay => DateTime.UtcNow.AddDays(1),
                SessionKeepTime.OneWeek => DateTime.UtcNow.AddDays(7),
                SessionKeepTime.OneMonth => DateTime.UtcNow.AddMonths(1),
                SessionKeepTime.OneYear => DateTime.UtcNow.AddYears(1),
                _ => throw new ArgumentOutOfRangeException()
            };
            var token = await UserManager.Login(userHelper.Name, userHelper.Password, expireTime);
            return new Tuple<ResponseStatus, object>(
                token != string.Empty
                    ? ResponseStatus.Success
                    : ResponseStatus.BadRequest,
                token);
        }

        [RequestProcessMethod(UserAuthority.Visitor)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> Register(UserHelper userHelper)
        {
            var result = await UserManager.Register(
                userHelper.Name,
                userHelper.Password,
                userHelper.InvitationCode);
            return new Tuple<ResponseStatus, object>(
                result == RegisterResult.Success
                    ? ResponseStatus.Success
                    : ResponseStatus.BadRequest,
                result);
        }

        [RequestProcessMethod(UserAuthority.User, requestUserId: true)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> GetUserInfo(long userId)
        {
            var user = await UserManager.GetUser(userId);
            return new Tuple<ResponseStatus, object>(
                user != null
                    ? ResponseStatus.Success
                    : ResponseStatus.BadRequest,
                user);
        }

        [RequestProcessMethod(UserAuthority.Root)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> GetAllInvitations()
        {
            var invitations = await UserManager.GetAllInvitations();
            return new Tuple<ResponseStatus, object>(
                invitations != null
                    ? ResponseStatus.Success
                    : ResponseStatus.BadRequest,
                invitations);
        }

        [RequestProcessMethod(UserAuthority.Root, requestUserId:true)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> GenerateInvitation(UserAuthority authority, long userId)
        {
            var invitation = await UserManager.GenerateInvitationCode(userId, authority);
            return new Tuple<ResponseStatus, object>(
                invitation != null
                    ? ResponseStatus.Success
                    : ResponseStatus.BadRequest,
                invitation != null);
        }

        [RequestProcessMethod(UserAuthority.Root)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> GetAllUsers()
        {
            var users = await UserManager.GetAllUsers();
            return new Tuple<ResponseStatus, object>(
                users != null
                    ? ResponseStatus.Success
                    : ResponseStatus.BadRequest,
                users);
        }

        [RequestProcessMethod(UserAuthority.Root)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> ChangeAuthority(User user)
        {
            await UserManager.UpdateAuthority(user);
            return new Tuple<ResponseStatus, object>(
                ResponseStatus.Success,
                null);
        }

        #endregion

        #region ProcessRequestAboutManga

        [RequestProcessMethod(UserAuthority.Admin)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> UpdateManga(Manga manga)
        {
            var success = await MangaManager.Update(manga, false);
            return new Tuple<ResponseStatus, object>(
                success
                    ? ResponseStatus.Success
                    : ResponseStatus.BadRequest,
                success);
        }

        [RequestProcessMethod(UserAuthority.Admin, requestObjectId: true)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> DeleteManga(long mangaId)
        {
            var manga = await MangaManager.GetManga(mangaId, -1);

            if (manga == null)
            {
                return new Tuple<ResponseStatus, object>(
                    ResponseStatus.NotFound,
                    null);
            }

            var path = LibraryManager.GeFileTreeNode(manga.PathId);

            var success = await MangaManager.DeleteManga(mangaId) && LibraryManager.Delete(path);

            return new Tuple<ResponseStatus, object>(
                success
                    ? ResponseStatus.Success
                    : ResponseStatus.BadRequest,
                success);
        }

        [RequestProcessMethod(UserAuthority.User, requestUserId: true, requestObjectId: true)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> GetManga(long userId, long mangaId)
        {
            var manga = await MangaManager.GetManga(mangaId, userId);
            return new Tuple<ResponseStatus, object>(
                manga != null
                    ? ResponseStatus.Success
                    : ResponseStatus.NotFound,
                manga);
        }

        [RequestProcessMethod(UserAuthority.User, requestUserId: true, requestObjectId: true)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> GetChapter(long userId, long chapterId)
        {
            var chapter = await MangaManager.GetChapter(chapterId, userId);
            return new Tuple<ResponseStatus, object>(
                chapter != null
                    ? ResponseStatus.Success
                    : ResponseStatus.NotFound,
                chapter);
        }

        [RequestProcessMethod(UserAuthority.User, requestUserId: false, requestObjectId: true)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> GetImage(long imageId)
        {
            var image = await MangaManager.GetImage(imageId);
            FileTreeNode path = null;

            if (image != null)
            {
                path = LibraryManager.GeFileTreeNode(image.PathId);
            }
            
            if (path == null)
            {
                return new Tuple<ResponseStatus, object>(
                    ResponseStatus.NotFound,
                    null);
            }

            var data = await path.ReadAllBytes();

            return new Tuple<ResponseStatus, object>(
                data != null
                    ? ResponseStatus.Success
                    : ResponseStatus.NotFound,
                data);
        }

        [RequestProcessMethod(UserAuthority.User, requestUserId: true)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> CountMangas(MangaQueryHelper query, long userId)
        {
            var mangaIds = query.QueryType switch
            {
                QueryType.Keyword => await MangaKeywordSearchService.Search(query.QueryString),
                QueryType.Favorite => await FavoriteMangaSearchService.Search(userId),
                QueryType.History => await MangaReadHistorySearchService.Search(userId),
                _ => throw new ArgumentOutOfRangeException()
            };
            return new Tuple<ResponseStatus, object>(ResponseStatus.Success, mangaIds.Count);
        }

        [RequestProcessMethod(UserAuthority.User, requestUserId: true)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> ListMangas(MangaQueryHelper query, long userId)
        {
            var mangaIds = query.QueryType switch
            {
                QueryType.Keyword => await MangaKeywordSearchService.Search(query.QueryString),
                QueryType.Favorite => await FavoriteMangaSearchService.Search(userId),
                QueryType.History => await MangaReadHistorySearchService.Search(userId),
                _ => throw new ArgumentOutOfRangeException()
            };
            var mangas = await MangaManager.GetMangas(mangaIds, query.Offset, query.Limit, query.OrderType, query.Asc);
            return new Tuple<ResponseStatus, object>(
                mangas != null
                    ? ResponseStatus.Success
                    : ResponseStatus.NotFound,
                mangas);
        }

        [RequestProcessMethod(UserAuthority.User, requestUserId: true)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> RecordReadProgress(ReadProgress readProgress, long userId)
        {
            readProgress.UserId = userId;
            await MangaManager.Upsert(readProgress);
            return new Tuple<ResponseStatus, object>(
                ResponseStatus.Success,
                null);
        }

        [RequestProcessMethod(UserAuthority.User, requestUserId: true, requestObjectId:true)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> GetChapterReadProgress(long userId, long chapterId)
        {
            var readProgress = await MangaManager.GetReadProgress(userId, chapterId);
            return new Tuple<ResponseStatus, object>(
                readProgress != null
                    ? ResponseStatus.Success
                    : ResponseStatus.NotFound,
                readProgress);
        }

        [RequestProcessMethod(UserAuthority.User, requestUserId: true, requestObjectId: true)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> AddMangaFavorite(long userId, long mangaId)
        {
            await MangaManager.AddMangaFavorite(userId, mangaId);

            return new Tuple<ResponseStatus, object>(
                ResponseStatus.Success,
                null);
        }

        [RequestProcessMethod(UserAuthority.User, requestUserId: true, requestObjectId: true)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> RemoveMangaFavorite(long userId, long mangaId)
        {
            await MangaManager.RemoveMangaFavorite(userId, mangaId);

            return new Tuple<ResponseStatus, object>(
                ResponseStatus.Success,
                null);
        }

        [RequestProcessMethod(UserAuthority.User, requestUserId: true)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> AddComment(Comment comment, long userId)
        {
            comment.UserId = userId;
            var success = await MangaManager.Upsert(comment);

            return new Tuple<ResponseStatus, object>(
                success
                    ? ResponseStatus.Success
                    : ResponseStatus.BadRequest,
                success);
        }

        [RequestProcessMethod(UserAuthority.User, requestObjectId: true)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> DownloadManga(long mangaId)
        {
            var files = new List<Tuple<string, FileTreeNode>>();
            var manga = await MangaManager.GetManga(mangaId, 0);
            manga.Cover = await MangaManager.GetImage(manga.CoverId);
            manga.Cover.Path = LibraryManager.GeFileTreeNode(manga.Cover.PathId);

            files.Add(new Tuple<string, FileTreeNode>($"{manga.Title}/{System.IO.Path.GetFileName(manga.Cover.Path.FullName)}", manga.Cover.Path));
            foreach (var chapter in manga.Chapters)
            {
                chapter.Images = await MangaManager.GetImages(chapter.ObjectId);
                foreach(var image in chapter.Images)
                {
                    image.Path = LibraryManager.GeFileTreeNode(image.PathId);
                    var key = $"{manga.Title}/{chapter.ChapterClass}/{chapter.Title}/{System.IO.Path.GetFileName(image.Path.FullName)}";
                    files.Add(new Tuple<string, FileTreeNode>(key, image.Path));
                }
            }

            return new Tuple<ResponseStatus, object>(ResponseStatus.Success, new ArchiveFileDataGenerator(files, 2 * 1024 * 1024));
        }

        #endregion

        #region ProcessRequestAboutTag

        [RequestProcessMethod(UserAuthority.User, requestObjectId: true)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> GetTag(long tagId)
        {
            var tag = await MangaManager.GetTag(tagId);

            return new Tuple<ResponseStatus, object>(
                tag != null
                    ? ResponseStatus.Success
                    : ResponseStatus.NotFound,
                tag);
        }

        [RequestProcessMethod(UserAuthority.Admin, requestObjectId: true)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> DeleteTag(long tagId)
        {
            var success = await MangaManager.DeleteTag(tagId);

            return new Tuple<ResponseStatus, object>(
                success
                    ? ResponseStatus.Success
                    : ResponseStatus.NotFound,
                success);
        }

        [RequestProcessMethod(UserAuthority.User)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> CountTags(TagQueryHelper query)
        {
            var tagIds = await TagKeywordSearchService.Search(query.QueryString, query.TypeId);

            return new Tuple<ResponseStatus, object>(
                ResponseStatus.Success,
                tagIds.Count);
        }

        [RequestProcessMethod(UserAuthority.User)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> ListTags(TagQueryHelper query)
        {
            var tagIds = await TagKeywordSearchService.Search(query.QueryString, query.TypeId);
            var tags = await MangaManager.GetTags(tagIds, query.Offset, query.Limit);

            return new Tuple<ResponseStatus, object>(
                tags != null
                    ? ResponseStatus.Success
                    : ResponseStatus.NotFound,
                tags);
        }

        [RequestProcessMethod(UserAuthority.User)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> ListTagTypes()
        {
            var tagTypes = await MangaManager.GetTagTypes();

            return new Tuple<ResponseStatus, object>(
                tagTypes != null
                    ? ResponseStatus.Success
                    : ResponseStatus.NotFound,
                tagTypes);
        }

        [RequestProcessMethod(UserAuthority.Admin)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> AddTagType(TagType tagType)
        {
            var objectId = await MangaManager.Insert(tagType);

            return new Tuple<ResponseStatus, object>(
                objectId > 0
                    ? ResponseStatus.Success
                    : ResponseStatus.BadRequest,
                objectId);
        }

        [RequestProcessMethod(UserAuthority.Admin, requestObjectId: true)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> DeleteTagType(long typeId)
        {
            var success = await MangaManager.DeleteTagType(typeId);

            return new Tuple<ResponseStatus, object>(
                success
                    ? ResponseStatus.Success
                    : ResponseStatus.BadRequest,
                success);
        }

        [RequestProcessMethod(UserAuthority.Admin)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> AddTag(Tag tag)
        {
            tag = await MangaManager.Insert(tag);

            return new Tuple<ResponseStatus, object>(
                tag != null
                    ? ResponseStatus.Success
                    : ResponseStatus.BadRequest,
                tag);
        }

        [RequestProcessMethod(UserAuthority.Admin)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> UpdateTag(Tag tag)
        {
            var success = await MangaManager.Update(tag);

            return new Tuple<ResponseStatus, object>(
                success
                    ? ResponseStatus.Success
                    : ResponseStatus.BadRequest,
                success);
        }

        #endregion

        #region ProcessRequestAboutLibrary

        [RequestProcessMethod(UserAuthority.Admin)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> AddLibrary(FileTreeRoot library)
        {
            library = LibraryManager.AddLibrary(library);

            return new Tuple<ResponseStatus, object>(
                library != null
                    ? ResponseStatus.Success
                    : ResponseStatus.BadRequest,
                library);
        }

        [RequestProcessMethod(UserAuthority.Admin)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> UpdateLibrary(FileTreeRoot library)
        {
            var success = LibraryManager.UpdateLibrary(library);

            return new Tuple<ResponseStatus, object>(
                success
                    ? ResponseStatus.Success
                    : ResponseStatus.BadRequest,
                success);
        }

        [RequestProcessMethod(UserAuthority.Admin)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> GetLibraries()
        {
            var libraries = LibraryManager.GetLibraries();

            return new Tuple<ResponseStatus, object>(
                libraries != null
                    ? ResponseStatus.Success
                    : ResponseStatus.NotFound,
                libraries);
        }

        [RequestProcessMethod(UserAuthority.Admin, requestObjectId: true)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> GetLibrary(long libraryId)
        {
            var library = LibraryManager.GetLibrary(libraryId);

            return new Tuple<ResponseStatus, object>(
                library != null
                    ? ResponseStatus.Success
                    : ResponseStatus.NotFound,
                library);
        }

        [RequestProcessMethod(UserAuthority.Admin, requestObjectId: true)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> ScanLibraryAndCreateMangaItems(long libraryId)
        {
            var scanTask = new ScanLibraryTask(libraryId, $"扫描-{LibraryManager.GetLibrary(libraryId).Name}");
            var success = Scheduler.ScheduleAndStart(scanTask);

            return new Tuple<ResponseStatus, object>(
                success
                    ? ResponseStatus.Success
                    : ResponseStatus.BadRequest,
                success);
        }

        [RequestProcessMethod(UserAuthority.Admin, requestObjectId: true)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> DeleteLibrary(long libraryId)
        {
            var library = LibraryManager.GetLibrary(libraryId);
            if (library == null)
            {
                return new Tuple<ResponseStatus, object>(
                    ResponseStatus.NotFound,
                    false);
            }

            var mangaPathIds = library.Repository.GetAllNodeObjectId();
            var mangaIds = await MangaManager.GetMangaIdByFileTreeNodeId(mangaPathIds);
            foreach (var mangaId in mangaIds)
            {
                await MangaManager.DeleteManga(mangaId);
            }

            LibraryManager.Delete(library);
            return new Tuple<ResponseStatus, object>(
                ResponseStatus.Success,
                true);
        }

        #endregion

        #region ProcessRequestAboutScheduleTask

        [RequestProcessMethod(UserAuthority.Admin, requestObjectId:true)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> TriggerPlan(long planId)
        {
            PlanManager.TriggerPlan(planId);
            return new Tuple<ResponseStatus, object>(
                ResponseStatus.Success,
                null);
        }

        [RequestProcessMethod(UserAuthority.Admin, requestObjectId: true)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> GetSubScheduleTasks(long taskId)
        {
            var res = Scheduler.GetSubTasks(taskId);
            return new Tuple<ResponseStatus, object>(
                res != null?
                ResponseStatus.Success:
                ResponseStatus.NotFound, 
                res);
        }

        [RequestProcessMethod(UserAuthority.Admin, requestObjectId: true)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> RestartScheduleTask(long taskId)
        {
            Scheduler.Restart(taskId);
            return new Tuple<ResponseStatus, object>(
                ResponseStatus.Success,
                null);
        }

        [RequestProcessMethod(UserAuthority.Admin, requestObjectId: true)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> RemoveScheduleTask(long taskId)
        {
            Scheduler.Cancel(taskId);
            return new Tuple<ResponseStatus, object>(
                ResponseStatus.Success,
                null);
        }

        [RequestProcessMethod(UserAuthority.Admin)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> GetPlans()
        {
            var res = PlanManager.GetPlans();
            return new Tuple<ResponseStatus, object>(
                res != null 
                    ? ResponseStatus.Success 
                    : ResponseStatus.BadRequest,
                res);
        }

        [RequestProcessMethod(UserAuthority.Admin, requestObjectId:true)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> GetPlan(long planId)
        {
            var res = PlanManager.GetPlan(planId);
            return new Tuple<ResponseStatus, object>(
                res != null 
                    ? ResponseStatus.Success 
                    : ResponseStatus.BadRequest,
                res);
        }

        [RequestProcessMethod(UserAuthority.Admin)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> AddPlan(Plan plan)
        {
            var planId = PlanManager.InsertPlan(plan);
            return new Tuple<ResponseStatus, object>(
                planId > 0 ? ResponseStatus.Success : ResponseStatus.BadRequest,
                planId);
        }

        [RequestProcessMethod(UserAuthority.Admin)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> UpdatePlan(Plan plan)
        {
            PlanManager.UpdatePlan(plan);
            return new Tuple<ResponseStatus, object>(
                ResponseStatus.Success,
                null);
        }

        [RequestProcessMethod(UserAuthority.Admin, requestObjectId: true)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> RemovePlan(long planId)
        {
            PlanManager.RemovePlan(planId);
            return new Tuple<ResponseStatus, object>(
                ResponseStatus.Success,
                null);
        }

        #endregion

        #region ProcessRequestAboutMessage

        [RequestProcessMethod(UserAuthority.User, requestUserId: true)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> CountMessageUnchecked(long userId)
        {
            var count = await MessageManager.CountUncheckedMessage(userId);
            return new Tuple<ResponseStatus, object>(
                ResponseStatus.Success,
                count);
        }

        [RequestProcessMethod(UserAuthority.User, requestUserId: true)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> GetMessages(long userId)
        {
            var messages = await MessageManager.GetMessages(userId);
            return new Tuple<ResponseStatus, object>(
                messages != null ? ResponseStatus.Success : ResponseStatus.BadRequest,
                messages);
        }

        [RequestProcessMethod(UserAuthority.User, requestUserId: true)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> ClearCheckedMessage(long userId)
        {
            await MessageManager.ClearCheckedMessage(userId);
            return new Tuple<ResponseStatus, object>(
                ResponseStatus.Success,
                null);
        }

        [RequestProcessMethod(UserAuthority.User, requestUserId: true)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> Check(List<long> messageIds, long userId)
        {
            var success = await MessageManager.CheckMessages(messageIds, userId);
            return new Tuple<ResponseStatus, object>(
                success ? ResponseStatus.Success : ResponseStatus.BadRequest,
                success);
        }

        [RequestProcessMethod(UserAuthority.User, requestUserId:true, requestSession: true)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> SubscribeMessage(long userId, IAppSession session)
        {
            lock (_subscribers)
            {
                if (!_subscribers.TryGetValue(userId, out var sessions))
                {
                    sessions = new HashSet<IAppSession>();
                    _subscribers.Add(userId, sessions);
                }

                lock (sessions)
                {
                    sessions.Add(session);
                }
            }
            return new Tuple<ResponseStatus, object>(
                ResponseStatus.Success,
                null);
        }

        #endregion

        #region ProcessRequestAboutPlugin

        [RequestProcessMethod(UserAuthority.Admin)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> GetPluginDetails()
        {
            var details = PluginManager.GetPluginDetails();
            return new Tuple<ResponseStatus, object>(
                details != null ? ResponseStatus.Success : ResponseStatus.BadRequest,
                details);
        }

        [RequestProcessMethod(UserAuthority.Admin)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> GetPluginDetail(string pluginType)
        {
            var detail = PluginManager.GetPluginDetail(pluginType);
            return new Tuple<ResponseStatus, object>(
                detail != null ? ResponseStatus.Success : ResponseStatus.BadRequest,
                detail);
        }

        [RequestProcessMethod(UserAuthority.Admin)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> SetPluginParameters(PluginDetail detail)
        {
            var success = PluginManager.SetPluginParameters(detail);
            return new Tuple<ResponseStatus, object>(
                success ? ResponseStatus.Success : ResponseStatus.BadRequest,
                success);
        }

        [RequestProcessMethod(UserAuthority.Admin)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> ResetPluginParameters(string pluginType)
        {
            var detail = PluginManager.ResetPluginParameters(pluginType);
            return new Tuple<ResponseStatus, object>(
                detail != null ? ResponseStatus.Success : ResponseStatus.BadRequest,
                detail);
        }

        [RequestProcessMethod(UserAuthority.Admin)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> GetScrapers()
        {
            var scrapers = PluginManager.MetadataScrapers.Select(it => it.Name).ToList();
            return new Tuple<ResponseStatus, object>(
                ResponseStatus.Success,
                scrapers);
        }

        #endregion

        #region MessageNotify

        private async void MessageManagerOnMessageSent(object? sender, Message e)
        {
            foreach (var receiver in e.Receivers)
            {
                if (!_subscribers.TryGetValue(receiver, out var sessions)) continue;
                var response = new Response()
                {
                    Id = -1,
                    Completed = true,
                    Data = new Base.Network.Message()
                    {
                        Data = ByteString.CopyFrom(MessagePackSerializer.Serialize(ServerNotify.NewMessageSent,
                            SerializerOptions))
                    }
                };
                foreach (var session in sessions.ToList())
                {
                    if(!session.Channel.IsClosed)
                    {
                        await session.SendAsync(_responseEncoder, response);
                    }
                    else
                    {
                        lock (sessions)
                        {
                            sessions.Remove(session);
                        }
                    }
                }
            }
        }

        #endregion

        #region ServerConfig

        [RequestProcessMethod(UserAuthority.Admin)]
        public virtual async ValueTask<Tuple<ResponseStatus, object>> ResetFtsIndex()
        {
            var mangas = await MangaManager.GetAllMangas();
            MangaFtsIndexService.Clear();
            MangaFtsIndexService.Create(mangas);
            var tags = await MangaManager.GetAllTags();
            TagFtsIndexService.Clear();
            TagFtsIndexService.Create(tags);
            return new Tuple<ResponseStatus, object>(ResponseStatus.Success, true);
        }

        #endregion

        public void GenerateClientConfig()
        {
            ConsoleColorSetter.SetForeground(ConsoleColor.DarkYellow);
            Console.WriteLine("\n客户端配置字符串:");
            foreach (var server in ServerConfigs)
            {
                ConsoleColorSetter.SetForeground(ConsoleColor.Green);
                Console.WriteLine($"{server.Host}:{server.Port}");
                ConsoleColorSetter.SetForeground(ConsoleColor.White);
                Console.WriteLine(ServerConfigStringGenerator.ServerConfigStringGenerate(server.Host, server.Port, CertificateHash, Name, Id));
            }

            ConsoleColorSetter.SetForeground(ConsoleColor.Gray);
            Console.WriteLine();
        }
    }
}