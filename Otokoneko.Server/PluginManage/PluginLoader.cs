using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Otokoneko.DataType;
using Otokoneko.Plugins.Interface;

namespace Otokoneko.Server.PluginManage
{
    public class PluginLoader
    {
        public PluginParameterProvider PluginParameterProvider { get; set; }

        private const string PluginDirectory = "./plugins";

        private static readonly HashSet<Type> SupportInterface = new HashSet<Type>()
        {
            typeof(IMangaDownloader),
            typeof(IMetadataScraper)
        };

        private HashSet<IPlugin> Plugins { get; set; }
        public Dictionary<string, PluginDetail> PluginDetails { get; private set; }

        public void Load()
        {
            Plugins = new HashSet<IPlugin>();
            PluginDetails = new Dictionary<string, PluginDetail>();
            var path = Path.Combine(Environment.CurrentDirectory, PluginDirectory);
            foreach (var file in Directory.GetFiles(path, "*.dll"))
            {
                var ass = Assembly.LoadFile(file);
                var type = ass.GetTypes().FirstOrDefault(m => m.GetInterface(nameof(IPlugin)) != null);
                if (type == null) continue;
                var plugin = (IPlugin)Activator.CreateInstance(type);
                Plugins.Add(plugin);
            }

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
                        .Select(it=>it.Name)
                        .ToList();

                PluginDetails.Add(pluginDetail.Type, pluginDetail);
            }
        }

        public bool SetParameters(PluginDetail detail)
        {
            var plugin = GetPlugin(detail.Type);
            if (plugin == null) return false;

            var properties = plugin.GetType().GetProperties()
                .Where(it => it.GetCustomAttribute<RequiredParameterAttribute>() != null)
                .ToDictionary(it => it.Name, it => it);
            foreach (var parameter in detail.RequiredParameters)
            {
                if (properties.TryGetValue(parameter.Name, out var property) &&
                    parameter.Value.GetType() == property.PropertyType)
                {
                    PluginParameterProvider.Put(plugin.GetType(), parameter.Name, parameter.Value);
                    property.SetValue(plugin, parameter.Value);
                }
            }

            return true;
        }

        public void ResetParameters(string pluginType)
        {
            var plugin = GetPlugin(pluginType);
            if (plugin == null || !PluginDetails.TryGetValue(pluginType, out var detail)) return;

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
