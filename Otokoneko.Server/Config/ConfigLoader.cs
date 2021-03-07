using System.IO;
using YamlDotNet.Serialization;

namespace Otokoneko.Server.Config
{
    public class ConfigLoader
    {
        private static string Path { get; } = @"./config.yml";
        public Config Config { get; set; }

        private ISerializer Serializer { get; } = new SerializerBuilder()
            .WithTypeInspector(inner => new CommentGatheringTypeInspector(inner))
            .WithEmissionPhaseObjectGraphVisitor(args => new CommentsObjectGraphVisitor(args.InnerVisitor))
            .Build();

        private IDeserializer Deserializer { get; } = new DeserializerBuilder().Build();

        public ConfigLoader()
        {
            if (File.Exists(Path))
            {
                Config = Deserializer.Deserialize<Config>(File.ReadAllText(Path));
            }
            else
            {
                Config = new Config();
                File.WriteAllText(Path, Serializer.Serialize(Config));
            }
        }
    }
}
