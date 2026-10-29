using System;
using System.Collections.Generic;
using Sherlock.Core.Models;

namespace Sherlock.Core.Analysis
{
    public sealed class IssueCluster
    {
        public string Signature { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public LogLevel HighestSeverity { get; set; }
        public int Count { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public string SampleMessage { get; set; } = string.Empty;
        public string? SampleStackTrace { get; set; }
        public double TriageScore { get; set; }

        internal List<LogEntry> Entries { get; } = new();
    }
}
