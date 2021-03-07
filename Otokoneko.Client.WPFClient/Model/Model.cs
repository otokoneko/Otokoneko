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

        private ConcurrentDictionary<string, DB> Databases { get; }  = new ConcurrentDictionary<string, DB>();

        private T GetObjectFromDb<T>(long key, string databaseName)
        {
            if (string.IsNullOrEmpty(databaseName)) return default;
            if (!Databases.ContainsKey(databaseName))
            {
                Databases.TryAdd(databaseName,
                    new DB(new Options() { CreateIfMissing = true }, Path.Combine(DbPath, databaseName)));
            }

            Databases.TryGetValue(databaseName, out var db);
            var bytes = db.Get(BitConverter.GetBytes(key));
            return bytes != null ? MessagePackSerializer.Deserialize<T>(bytes, _lz4Option) : default;
        }

        private T GetObjectFromDb<T>(string key, string databaseName)
        {
            if (string.IsNullOrEmpty(databaseName)) return default;
            if (!Databases.ContainsKey(databaseName))
            {
                Databases.TryAdd(databaseName,
                    new DB(new Options() { CreateIfMissing = true }, Path.Combine(DbPath, databaseName)));
            }

            Databases.TryGetValue(databaseName, out var db);
            var bytes = db.Get(Encoding.UTF8.GetBytes(key));
            return bytes != null ? MessagePackSerializer.Deserialize<T>(bytes, _lz4Option) : default;
        }

        private void PutObjectToDb<T>(long key, T value, string databaseName)
        {
            if (!Databases.ContainsKey(databaseName))
            {
                Databases.TryAdd(databaseName,
                    new DB(new Options() { CreateIfMissing = true }, Path.Combine(DbPath, databaseName)));
            }

            Databases.TryGetValue(databaseName, out var db);
            db.Put(BitConverter.GetBytes(key), MessagePackSerializer.Serialize(value, _lz4Option));
        }

        private void PutObjectToDb<T>(string key, T value, string databaseName)
        {
            if (!Databases.ContainsKey(databaseName))
            {
                Databases.TryAdd(databaseName,
                    new DB(new Options() { CreateIfMissing = true }, Path.Combine(DbPath, databaseName)));
            }

            Databases.TryGetValue(databaseName, out var db);
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

        #endregion
    }
}