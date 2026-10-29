using Sherlock.Core.Analysis;
using Sherlock.Core.Parsing;

var lines = File.ReadAllLines("../sample-data/production.log");
var entries = LogParser.Parse(lines);
Console.WriteLine($"Parsed {entries.Count} log entries");

var clusters = TriageEngine.BuildClusters(entries);
var ranked = TriageEngine.Rank(clusters, asOf: new DateTime(2026, 8, 14, 10, 0, 0));

Console.WriteLine();
Console.WriteLine("Triage report (highest priority first):");
Console.WriteLine("=========================================");
foreach (var cluster in ranked)
{
    Console.WriteLine($"[{cluster.TriageScore,7}] {cluster.HighestSeverity,-5} {cluster.Source,-18} x{cluster.Count,-3} \"{cluster.SampleMessage}\"");
}
