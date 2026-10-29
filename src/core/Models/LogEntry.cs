using System;

namespace Sherlock.Core.Models
{
    public enum LogLevel
    {
        Trace,
        Debug,
        Info,
        Warn,
        Error,
        Fatal
    }

    public sealed class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Source { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? StackTrace { get; set; }

        public override string ToString() =>
            $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] {Level} {Source}: {Message}";
    }
}
