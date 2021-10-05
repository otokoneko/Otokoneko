using log4net.Appender;
using System;
using System.IO;

namespace Otokoneko.Server.Utils
{
    public class RollingFileByMaxAgeAppender : RollingFileAppender
    {
        public RollingFileByMaxAgeAppender()
            : base()
        {
        }

        protected override void AdjustFileBeforeAppend()
        {
            base.AdjustFileBeforeAppend();
            if(MaxSizeRollBackups <= 0)
            {
                return;
            }

            var maxAgeRollBackups = DateTime.Today.AddDays(-1 * MaxSizeRollBackups);

            foreach (var file in Directory.GetFiles(Path.GetDirectoryName(File), "*.log"))
            {
                if (System.IO.File.GetLastWriteTime(file) < maxAgeRollBackups)
                    DeleteFile(file);
            }
        }
    }
}
