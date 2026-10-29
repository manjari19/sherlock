using System;
using System.Collections.Generic;
using System.Linq;
using Sherlock.Core.Models;

namespace Sherlock.Core.Analysis
{
    /// <summary>
    /// Groups parsed log entries into clusters by normalized signature
    /// and ranks them so the issues actually worth investigating first
    /// surface at the top, not just the most recent line in the file.
    ///
    /// Ranking weighs three things: how often it happens (frequency),
    /// how severe it is (WARN vs ERROR vs FATAL), and how recently it
    /// last happened (a bug that stopped occurring three weeks ago is
    /// lower priority than one still firing today, even with the same
    /// historical count).
    /// </summary>
    public static class TriageEngine
    {
        private static readonly Dictionary<LogLevel, double> SeverityWeight = new()
        {
            [LogLevel.Trace] = 0.1,
            [LogLevel.Debug] = 0.2,
            [LogLevel.Info] = 0.3,
            [LogLevel.Warn] = 1.0,
            [LogLevel.Error] = 2.5,
            [LogLevel.Fatal] = 4.0,
        };

        public static List<IssueCluster> BuildClusters(IEnumerable<LogEntry> entries)
        {
            var groups = entries.GroupBy(e => (e.Source, Signature: SignatureNormalizer.Normalize(e.Message)));

            var clusters = new List<IssueCluster>();
            foreach (var group in groups)
            {
                var ordered = group.OrderBy(e => e.Timestamp).ToList();
                var cluster = new IssueCluster
                {
                    Signature = group.Key.Signature,
                    Source = group.Key.Source,
                    Count = ordered.Count,
                    FirstSeen = ordered.First().Timestamp,
                    LastSeen = ordered.Last().Timestamp,
                    HighestSeverity = ordered.Max(e => e.Level),
                    SampleMessage = ordered.Last().Message,
                    SampleStackTrace = ordered.LastOrDefault(e => e.StackTrace != null)?.StackTrace,
                };
                cluster.Entries.AddRange(ordered);
                clusters.Add(cluster);
            }

            return clusters;
        }

        /// <summary>
        /// Scores and sorts clusters descending, so index 0 is what an
        /// engineer should look at first. asOf lets the recency
        /// component be evaluated against a fixed point in time,
        /// which is what makes this testable deterministically.
        /// </summary>
        public static List<IssueCluster> Rank(List<IssueCluster> clusters, DateTime asOf)
        {
            foreach (var cluster in clusters)
            {
                double hoursSinceLastSeen = Math.Max(0, (asOf - cluster.LastSeen).TotalHours);
                // Recency decays on a half-life of roughly 3 days (72h):
                // an issue that stopped happening three days ago is
                // worth about half as much attention right now as one
                // still actively firing.
                double recencyFactor = Math.Pow(0.5, hoursSinceLastSeen / 72.0);

                double severity = SeverityWeight[cluster.HighestSeverity];
                cluster.TriageScore = Math.Round(cluster.Count * severity * recencyFactor, 3);
            }

            return clusters.OrderByDescending(c => c.TriageScore).ToList();
        }
    }
}
