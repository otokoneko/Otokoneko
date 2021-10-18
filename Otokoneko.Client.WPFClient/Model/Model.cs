using LevelDB;
using MessagePack;
using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows;
using Color = System.Windows.Media.Color;

namespace Otokoneko.Client
{
    public partial class Model
    {
        #region Local

        private ConcurrentDictionary<string, DB> Databases { get; } = new ConcurrentDictionary<string, DB>();

        private DB GetDb(string databaseName)
        {
            if (string.IsNullOrEmpty(databaseName)) return null;
            if (!Databases.ContainsKey(databaseName))
            {
                try
                {
                    var db = new DB(new Options() { CreateIfMissing = true }, Path.Combine(DbPath, databaseName));
                    Databases.TryAdd(databaseName, db);
                }
                catch (Exception) { }
            }
            return Databases.TryGetValue(databaseName, out var database) ? database : null;
        }

        private T GetObjectFromDb<T>(long key, string databaseName)
        {
            var db = GetDb(databaseName);
            if (db == null) throw new ArgumentException($"invalid database {databaseName}", nameof(databaseName));

            var bytes = db.Get(BitConverter.GetBytes(key));
            return bytes != null ? MessagePackSerializer.Deserialize<T>(bytes, _lz4Option) : default;
        }

        private T GetObjectFromDb<T>(string key, string databaseName)
        {
            var db = GetDb(databaseName);
            if (db == null) throw new ArgumentException($"invalid database {databaseName}", nameof(databaseName));

            var bytes = db.Get(Encoding.UTF8.GetBytes(key));
            return bytes != null ? MessagePackSerializer.Deserialize<T>(bytes, _lz4Option) : default;
        }

        private void PutObjectToDb<T>(long key, T value, string databaseName)
        {
            var db = GetDb(databaseName);
            if (db == null) throw new ArgumentException($"invalid database {databaseName}", nameof(databaseName));

            db.Put(BitConverter.GetBytes(key), MessagePackSerializer.Serialize(value, _lz4Option));
        }

        private void PutObjectToDb<T>(string key, T value, string databaseName)
        {
            var db = GetDb(databaseName);
            if (db == null) throw new ArgumentException($"invalid database {databaseName}", nameof(databaseName));

            db.Put(Encoding.UTF8.GetBytes(key), MessagePackSerializer.Serialize(value, _lz4Option));
        }

        public void ChangeTheme()
        {
            ControlzEx.Theming.ThemeManager.Current.ChangeTheme(Application.Current,
                (Setting.ThemeOption.DarkMode ? "Dark." : "Light.") + Setting.ThemeOption.Color);
        }

        public void SetColor(long tagTypeId, Color color)
        {
            PutObjectToDb(tagTypeId, new ColorConverter().ConvertToString(color), CurrentSession.ServerConfig.ServerId);
        }

        public Color GetColor(long tagTypeId)
        {
            var color = GetObjectFromDb<string>(tagTypeId, CurrentSession.ServerConfig.ServerId);
            if (string.IsNullOrEmpty(color))
            {
                return System.Windows.Media.Colors.DarkGray;
            }
            else
            {
                var drawColor = (System.Drawing.Color)new ColorConverter().ConvertFromString(color);
                return Color.FromArgb(drawColor.A, drawColor.R, drawColor.G, drawColor.B);
            }
        }

        private void CheckIfRunnale()
        {
            if (GetDb(MetadataDatabaseName) == null)
            {
                MessageBox.Show("已有另一客户端正在运行");
                Environment.Exit(0);
            }
        }

        #endregion
    }
}