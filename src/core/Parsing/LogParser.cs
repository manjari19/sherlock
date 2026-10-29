using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Sherlock.Core.Models;

namespace Sherlock.Core.Parsing
{
    public static class LogParser
    {
        private static readonly Regex EntryPattern = new(
            @"^\[(?<ts>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})\]\s+(?<level>TRACE|DEBUG|INFO|WARN|ERROR|FATAL)\s+(?<source>[^:]+):\s*(?<message>.*)$",
            RegexOptions.Compiled);

        public static List<LogEntry> Parse(IEnumerable<string> lines)
        {
            var entries = new List<LogEntry>();
            LogEntry? current = null;

            foreach (var rawLine in lines)
            {
                var match = EntryPattern.Match(rawLine);
                if (match.Success)
                {
                    current = new LogEntry
                    {
                        Timestamp = DateTime.Parse(match.Groups["ts"].Value),
                        Level = ParseLevel(match.Groups["level"].Value),
                        Source = match.Groups["source"].Value.Trim(),
                        Message = match.Groups["message"].Value.Trim(),
                    };
                    entries.Add(current);
                }
                else if (current != null && IsStackTraceLine(rawLine))
                {
                    current.StackTrace = current.StackTrace == null
                        ? rawLine.Trim()
                        : current.StackTrace + "\n" + rawLine.Trim();
                }
            }

            return entries;
        }

        private static bool IsStackTraceLine(string line) =>
            line.TrimStart().StartsWith("at ") || line.StartsWith("   ") || line.StartsWith("\t");

        private static LogLevel ParseLevel(string raw) => raw switch
        {
            "TRACE" => LogLevel.Trace,
            "DEBUG" => LogLevel.Debug,
            "INFO" => LogLevel.Info,
            "WARN" => LogLevel.Warn,
            "ERROR" => LogLevel.Error,
            "FATAL" => LogLevel.Fatal,
            _ => throw new ArgumentException($"Unknown log level: {raw}"),
        };
    }
}
