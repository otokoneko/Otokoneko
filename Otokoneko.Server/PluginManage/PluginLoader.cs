using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using log4net;
using Otokoneko.DataType;
using Otokoneko.Plugins.Interface;

namespace Otokoneko.Server.PluginManage
{
    public class PluginLoader
    {
        public PluginParameterProvider PluginParameterProvider { get; set; }
        public ILog Logger { get; set; }

        private const string PluginDirectory = "./plugins";

        private static readonly HashSet<Type> SupportInterface = new HashSet<Type>()
        {
            typeof(IMangaDownloader),
            typeof(IMetadataScraper)
        };

        private HashSet<IPlugin> Plugins { get; set; }

        public PluginDetail GetPluginDetail(string type)
        {
            var plugin = GetPlugin(type);
            if(plugin == null) { 
                return null; 
            }

            var pluginType = plugin.GetType();
            var pluginDetail = new PluginDetail()
            {
                Name = plugin.Name,
                Author = plugin.Author,
                Version = plugin.Version,
                Type = pluginType.FullName,
                RequiredParameters = new List<PluginParameter>(),
            };
            var properties = pluginType.GetProperties();
            foreach (var property in properties)
            {
                var attribute = property.GetCustomAttribute<RequiredParameterAttribute>();
                if (attribute == null) continue;
                var value = PluginParameterProvider.Get(pluginType, attribute.Name, attribute.Type) ?? attribute.DefaultValue;
                property.SetValue(plugin, value);
                pluginDetail.RequiredParameters.Add(new PluginParameter()
                {
                    Name = attribute.Name,
                    Alias = attribute.Alias,
                    Type = attribute.Type,
                    IsReadOnly = attribute.IsReadOnly,
                    Value = value,
                    Comment = attribute.Comment,
                });
            }

            pluginDetail.SupportInterface =
                SupportInterface
                    .Where(type => type.IsAssignableFrom(pluginType))
                    .Select(it => it.Name)
                    .ToList();

            return pluginDetail;
        }

        public List<PluginDetail> GetPluginDetails()
        {
            var pluginDetails = new List<PluginDetail>();
            foreach (var plugin in Plugins)
            {
                var pluginType = plugin.GetType();
                var pluginDetail = new PluginDetail()
                {
                    Name = plugin.Name,
                    Author = plugin.Author,
                    Version = plugin.Version,
                    Type = pluginType.FullName,
                    RequiredParameters = new List<PluginParameter>(),
                };
                var properties = pluginType.GetProperties();
                foreach (var property in properties)
                {
                    var attribute = property.GetCustomAttribute<RequiredParameterAttribute>();
                    if (attribute == null) continue;
                    var value = PluginParameterProvider.Get(pluginType, attribute.Name, attribute.Type) ?? attribute.DefaultValue;
                    property.SetValue(plugin, value);
                    pluginDetail.RequiredParameters.Add(new PluginParameter()
                    {
                        Name = attribute.Name,
                        Alias = attribute.Alias,
                        Type = attribute.Type,
                        IsReadOnly = attribute.IsReadOnly,
                        Value = value,
                        Comment = attribute.Comment,
                    });
                }

                pluginDetail.SupportInterface =
                    SupportInterface
                        .Where(type => type.IsAssignableFrom(pluginType))
                        .Select(it => it.Name)
                        .ToList();

                pluginDetails.Add(pluginDetail);
            }
            return pluginDetails;
        }

        public void Load()
        {
            Plugins = new HashSet<IPlugin>();

            foreach (var dir in Directory.GetDirectories(PluginDirectory))
            {
                var dirName = Path.GetFileName(dir);
                var pluginDll = Path.GetFullPath(Path.Combine(dir, dirName + ".dll"));
                if (File.Exists(pluginDll))
                {
                    var loader = McMaster.NETCore.Plugins.PluginLoader.CreateFromAssemblyFile(
                        pluginDll,
                        sharedTypes: new[] { typeof(IPlugin) });

                    foreach (var pluginType in loader
                        .LoadDefaultAssembly()
                        .GetTypes()
                        .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract))
                    {
                        IPlugin plugin = (IPlugin)Activator.CreateInstance(pluginType);
                        Plugins.Add(plugin);
                        Logger.Info($"加载插件 - {plugin.Name} v{plugin.Version}");
                    }
                }
            }
        }

        public bool SetParameters(PluginDetail detail)
        {
            var plugin = GetPlugin(detail.Type);
            if (plugin == null) return false;

            foreach (var parameter in detail.RequiredParameters)
            {
                var property = plugin.GetType().GetProperty(parameter.Name);
                if (property.GetCustomAttribute<RequiredParameterAttribute>() != null &&
                    parameter.Value.GetType() == property.PropertyType)
                {
                    property.SetValue(plugin, parameter.Value);
                    PluginParameterProvider.Put(plugin.GetType(), parameter.Name, parameter.Value);
                }
            }

            return true;
        }

        public void ResetParameters(string pluginType)
        {
            var plugin = GetPlugin(pluginType);
            var detail = GetPluginDetail(pluginType);
            if (plugin == null || detail == null) return;

            var requiredParameters = new List<PluginParameter>();

            foreach (var property in plugin.GetType().GetProperties())
            {
                var attribute = property.GetCustomAttribute<RequiredParameterAttribute>();
                if(attribute == null) continue;
                property.SetValue(plugin, attribute.DefaultValue);
                PluginParameterProvider.Put(plugin.GetType(), attribute.Name, attribute.DefaultValue);
                requiredParameters.Add(new PluginParameter()
                {
                    Name = attribute.Name,
                    Alias = attribute.Alias,
                    Type = attribute.Type,
                    IsReadOnly = attribute.IsReadOnly,
                    Value = attribute.DefaultValue,
                    Comment = attribute.Comment,
                });
            }

            detail.RequiredParameters = requiredParameters;
        }

        private IPlugin GetPlugin(string type)
        {
            return Plugins.FirstOrDefault(it => it.GetType().FullName == type);
        }

        public List<T> GetPlugins<T>()
        where T: IPlugin
        {
            var results = new List<T>();
            foreach (var plugin in Plugins)
            {
                if (plugin is T t)
                {
                    results.Add(t);
                }
            }

            return results;
        }
    }
}
