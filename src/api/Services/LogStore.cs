using Sherlock.Core.Analysis;
using Sherlock.Core.Models;
using Sherlock.Core.Parsing;

namespace Sherlock.Api.Services
{
    public class LogStore
    {
        private readonly object _lock = new();
        private readonly List<LogEntry> _entries = new();

        public int Ingest(string rawText)
        {
            var lines = rawText.Split('\n');
            var parsed = LogParser.Parse(lines);
            lock (_lock)
            {
                _entries.AddRange(parsed);
            }
            return parsed.Count;
        }

        public List<IssueCluster> GetTriageReport(DateTime asOf)
        {
            lock (_lock)
            {
                var clusters = TriageEngine.BuildClusters(_entries);
                return TriageEngine.Rank(clusters, asOf);
            }
        }

        public IssueCluster? GetCluster(string signature, string source)
        {
            var report = GetTriageReport(DateTime.UtcNow);
            return report.FirstOrDefault(c => c.Signature == signature && c.Source == source);
        }

        public int EntryCount
        {
            get { lock (_lock) { return _entries.Count; } }
        }
    }
}
