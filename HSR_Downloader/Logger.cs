using System;
using System.Drawing;
using Colorful;

namespace HSR_DataDownloader
{
    public class Logger
    {
        public bool DoLogUselessInfo = true;

        public void ClearConsole()
        {
            Colorful.Console.ResetColor();
        }

        public void LogError(string message)
        {
            LogMessage(message, LogColors.ERROR);
        }

        public void LogWarning(string message)
        {
            LogMessage(message, LogColors.WARNING);
        }

        public void LogInfo(string message, bool isImportant = false)
        {
            if (DoLogUselessInfo || isImportant)
                LogMessage(message, LogColors.INFO);
        }

        public void LogSuccess(string message, bool isImportant = false)
        {
            if (DoLogUselessInfo || isImportant)
                LogMessage(message, LogColors.SUCCESS);
        }

        private void LogMessage(string message, LogColors color)
        {
            var colorCode = ColorTranslator.FromHtml($"#{(int)color:X6}");
            Colorful.Console.WriteLine($"[{color.ToString()}] {message}", colorCode);
            ClearConsole();
        }

        internal enum LogColors
        {
            None = 0xFFFFFF,
            INFO = 0x61AFEF,
            WARNING = 0xE5C07B,
            ERROR = 0xE06C75,
            SUCCESS = 0x98C379
        }
    }
}