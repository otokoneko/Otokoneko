using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extras.DynamicProxy;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Core;
using log4net.Filter;
using log4net.Layout;
using MessagePack;
using Otokoneko.DataType;
using Otokoneko.Server.Config;
using Otokoneko.Server.Converter;
using Otokoneko.Server.MangaManage;
using Otokoneko.Server.MessageBox;
using Otokoneko.Server.PluginManage;
using Otokoneko.Server.ScheduleTaskManage;
using Otokoneko.Server.SearchService;
using Otokoneko.Server.UserManage;
using SqlSugar;

namespace Otokoneko.Server
{
    class Program
    {
        private static ILog Logger { get; set; }

        static async Task Main(string[] args)
        {
            var builder = new ContainerBuilder();
            
            builder.RegisterType<Server>().PropertiesAutowired().EnableClassInterceptors().SingleInstance();
            
            builder.RegisterType<MangaFtsIndexService>().PropertiesAutowired().SingleInstance();
            builder.RegisterType<TagFtsIndexService>().PropertiesAutowired().SingleInstance();
            builder.RegisterType<MangaKeywordSearchService>().PropertiesAutowired().SingleInstance();
            builder.RegisterType<TagKeywordSearchService>().PropertiesAutowired().SingleInstance();
            builder.RegisterType<MangaReadHistorySearchService>().PropertiesAutowired().SingleInstance();
            builder.RegisterType<FavoriteMangaSearchService>().PropertiesAutowired().SingleInstance();
            builder.RegisterType<Scheduler>().PropertiesAutowired().SingleInstance();
            builder.RegisterType<MessageManager>().PropertiesAutowired().SingleInstance();
            builder.RegisterType<MangaManager>().PropertiesAutowired().SingleInstance();
            builder.RegisterType<LibraryManager>().PropertiesAutowired().SingleInstance();
            builder.RegisterType<UserManager>().PropertiesAutowired().SingleInstance();
            builder.RegisterType<ConfigLoader>().PropertiesAutowired().SingleInstance();
            builder.Register(context =>
            {
                var planManager = new PlanManager(context.Resolve<ILog>())
                {
                    CronTriggerHandler = context.Resolve<ITriggerHandler<CronTrigger>>(),
                    DisposableTriggerHandler = context.Resolve<ITriggerHandler<DisposableTrigger>>(),
                    MessageManager = context.Resolve<MessageManager>(),
                    ScanPlanHandler = context.Resolve<IPlanHandler<ScanPlan>>(),
                    DownloadPlanHandler = context.Resolve<IPlanHandler<DownloadPlan>>()
                };
                planManager.TaskCompletedTriggerHandler = new TaskCompletedTriggerHandler {PlanManager = planManager};
                planManager.Recover();
                return planManager;
            }).As<PlanManager>().SingleInstance();

            builder.Register(context =>
                {
                    var configLoader = context.Resolve<ConfigLoader>();
                    var level = configLoader.Config.LogConfig.Level switch
                    {
                        "All" => Level.All,
                        "Info" => Level.Info,
                        "Debug" => Level.Debug,
                        "Warn" => Level.Warn,
                        "Error" => Level.Error,
                        "Fatal" => Level.Fatal,
                        "Off" => Level.Off,
                        _ => Level.All
                    };
                    var layout = new PatternLayout("%date{yyyy-MM-dd HH:mm:ss} [%-5level]–%message %newline");

                    var fileFilter = new LevelMatchFilter {LevelToMatch = level };
                    fileFilter.ActivateOptions();

                    var fileAppender = new RollingFileAppender
                    {
                        File = configLoader.Config.LogConfig.Path,
                        ImmediateFlush = true,
                        AppendToFile = true,
                        RollingStyle = RollingFileAppender.RollingMode.Date,
                        DatePattern = "yyyyMMdd'.log'",
                        LockingModel = new FileAppender.InterProcessLock(),
                        Name = "Otokoneko File Appender",
                        MaxSizeRollBackups = configLoader.Config.LogConfig.MaxSizeRollBackups,
                        Encoding = Encoding.UTF8,
                        StaticLogFileName = false,
                        Layout = layout
                    };
                    fileAppender.AddFilter(fileFilter);
                    fileAppender.ActivateOptions();

                    var consoleAppender = new ConsoleAppender()
                    {
                        Layout = layout,
                        Name = "Otokoneko Console Appender",
                    };

                    var repository = LoggerManager.CreateRepository("Otokoneko Repository");
                    repository.Configured = true;
                    BasicConfigurator.Configure(repository, fileAppender, consoleAppender);
                    return LogManager.GetLogger("Otokoneko Repository", "Otokoneko Logger");
                }).As<ILog>().SingleInstance();

            builder.RegisterType<FileTreeNodeToMangaConverter>().PropertiesAutowired().SingleInstance();
            
            builder.RegisterType<PluginParameterProvider>().PropertiesAutowired().SingleInstance();
            builder.RegisterType<PluginLoader>().PropertiesAutowired().SingleInstance();
            builder.RegisterType<PluginManager>().PropertiesAutowired().SingleInstance();
            
            builder.RegisterType<DownloadPlanHandler>().PropertiesAutowired().As<IPlanHandler<DownloadPlan>>();
            builder.RegisterType<ScanPlanHandler>().PropertiesAutowired().As<IPlanHandler<ScanPlan>>();
            
            builder.RegisterType<TaskCompletedTriggerHandler>().PropertiesAutowired(PropertyWiringOptions.AllowCircularDependencies).As<ITriggerHandler<TaskCompletedTrigger>>();
            builder.RegisterType<DisposableTriggerHandler>().PropertiesAutowired().As<ITriggerHandler<DisposableTrigger>>();
            builder.RegisterType<CronTriggerHandler>().PropertiesAutowired().As<ITriggerHandler<CronTrigger>>();
            
            builder.RegisterType<DownloadMangaTaskHandler>().PropertiesAutowired().As<ITaskHandler<DownloadMangaScheduleTask>>();
            builder.RegisterType<DownloadChapterTaskHandler>().PropertiesAutowired().As<ITaskHandler<DownloadChapterScheduleTask>>();
            builder.RegisterType<DownloadImageTaskHandler>().PropertiesAutowired().As<ITaskHandler<DownloadImageScheduleTask>>();
            builder.RegisterType<ScanLibraryTaskHandler>().PropertiesAutowired().As<ITaskHandler<ScanLibraryTask>>();
            builder.RegisterType<ScanMangaTaskHandler>().PropertiesAutowired().As<ITaskHandler<ScanMangaTask>>();
            
            builder.Register(c => MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block))
                .As<MessagePackSerializerOptions>()
                .SingleInstance();
            
            builder.Register((c, p) =>
            {
                var config = new ConnectionConfig()
                {
                    DbType = DbType.Sqlite,
                    ConnectionString = p.TypedAs<string>(),
                    IsAutoCloseConnection = true,
                    InitKeyType = InitKeyType.Attribute
                };
                return new SqlSugarClient(config);
            })
                .As<SqlSugarClient>()
                .As<ISqlSugarClient>();
            
            var container = builder.Build();

            Logger = container.Resolve<ILog>();

            Directory.CreateDirectory(@"./certificate");
            Directory.CreateDirectory(@"./data");
            Directory.CreateDirectory(@"./data/thumbnail");
            Directory.CreateDirectory(@"./data/library");
            Directory.CreateDirectory(@"./plugins");

            Console.CancelKeyPress += ConsoleOnCancelKeyPress;
            
            var server = container.Resolve<Server>();
            server.GenerateClientConfig();
            
            var userManager = container.Resolve<UserManager>();
            await userManager.CreateRootUser();
            
            await server.Run();
        }

        private static void ConsoleOnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            Logger.Info("关闭服务器");
        }
    }
}
