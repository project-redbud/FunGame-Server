using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Milimoe.FunGame.Core.Api.Utility;

namespace Milimoe.FunGame.WebAPI.Services
{
    public class CustomConsoleFormatter() : ConsoleFormatter("CustomFormatter")
    {
        public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
        {
            string message = logEntry.Formatter(logEntry.State, logEntry.Exception);
            string level = logEntry.LogLevel.ToString()[..1].ToUpper();
            string category = logEntry.Category.Split('.')[^1];
            string colorLevel = GetColorCode(logEntry.LogLevel);
            DateTime now = DateTime.Now;
            string timestamp = now.AddMilliseconds(-now.Millisecond).ToString();

            if ((int)logEntry.LogLevel >= (int)Server.Others.Config.LogLevelValue)
            {
                textWriter.Write("\r");
                textWriter.WriteLine($"{colorLevel}{timestamp} {level}/[{category}] {message}");
            }
            textWriter.Write("\x1b[0m\r> ");

            if (logEntry.Exception != null)
            {
                TXTHelper.AppendErrorLog(logEntry.Exception);
            }
        }

        private static string GetColorCode(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Trace => "\x1b[37m", // 灰色 ConsoleColor.Gray
                LogLevel.Debug => "\x1b[32m", // 绿色 ConsoleColor.Green
                LogLevel.Warning => "\x1b[33m", // 黄色 ConsoleColor.Yellow
                LogLevel.Error => "\x1b[31m", // 红色 ConsoleColor.Red
                LogLevel.Critical => "\x1b[31m", // 红色 ConsoleColor.Red
                _ => "\x1b[0m" // 重置颜色
            };
        }
    }
}
