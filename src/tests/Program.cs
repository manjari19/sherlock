using Sherlock.Core.Analysis;
using Sherlock.Core.Models;
using Sherlock.Core.Parsing;
using Sherlock.Tests;

Console.WriteLine("Sherlock test suite");
Console.WriteLine("=====================");

// --- LogParser tests -------------------------------------------------
TestHarness.Run("Parses a well-formed single-line entry", () =>
{
    var entries = LogParser.Parse(new[] { "[2026-08-14 08:00:00] ERROR MyService: Something broke" });
    TestHarness.AssertEqual(1, entries.Count, "entry count");
    TestHarness.AssertEqual(LogLevel.Error, entries[0].Level, "level");
    TestHarness.AssertEqual("MyService", entries[0].Source, "source");
    TestHarness.AssertEqual("Something broke", entries[0].Message, "message");
});

TestHarness.Run("Attaches a stack trace to the preceding entry", () =>
{
    var lines = new[]
    {
        "[2026-08-14 08:00:00] ERROR MyService: Null reference",
        "   at MyService.DoWork()",
        "   at MyService.Run()",
    };
    var entries = LogParser.Parse(lines);
    TestHarness.AssertEqual(1, entries.Count, "entry count");
    TestHarness.AssertTrue(entries[0].StackTrace!.Contains("DoWork"), "stack trace should contain DoWork");
    TestHarness.AssertTrue(entries[0].StackTrace!.Contains("Run"), "stack trace should contain Run");
});

TestHarness.Run("Skips lines that match neither an entry nor a stack trace", () =>
{
    var lines = new[]
    {
        "==== log start ====",
        "[2026-08-14 08:00:00] INFO MyService: OK",
        "",
    };
    var entries = LogParser.Parse(lines);
    TestHarness.AssertEqual(1, entries.Count, "should only parse the one real entry");
});

TestHarness.Run("Silently skips a line with an unrecognized level word instead of throwing", () =>
{
    bool threw = false;
    try { LogParser.Parse(new[] { "[2026-08-14 08:00:00] WEIRD MyService: x" }); }
    catch { threw = true; }
    // An unrecognized level word means the regex simply won't match
    // (WEIRD isn't in the level alternation), so this line is
    // silently skipped as noise rather than raising an exception.
    TestHarness.AssertTrue(!threw, "unmatched level should be skipped, not throw");
});

// --- SignatureNormalizer tests -----------------------------------------
TestHarness.Run("Normalizes different numeric IDs to the same signature", () =>
{
    var sig1 = SignatureNormalizer.Normalize("User 12345 not found");
    var sig2 = SignatureNormalizer.Normalize("User 99 not found");
    TestHarness.AssertEqual(sig1, sig2, "signatures should match despite different IDs");
});

TestHarness.Run("Normalizes different quoted values to the same signature", () =>
{
    var sig1 = SignatureNormalizer.Normalize("Connection to 'server-a' failed");
    var sig2 = SignatureNormalizer.Normalize("Connection to 'server-b' failed");
    TestHarness.AssertEqual(sig1, sig2, "signatures should match despite different hostnames");
});

TestHarness.Run("Does not collapse genuinely different messages", () =>
{
    var sig1 = SignatureNormalizer.Normalize("User not found");
    var sig2 = SignatureNormalizer.Normalize("Order not found");
    TestHarness.AssertTrue(sig1 != sig2, "different messages should not collapse to the same signature");
});

// --- TriageEngine tests --------------------------------------------------
List<LogEntry> MakeEntries(params (string source, string msg, LogLevel level, DateTime ts)[] rows) =>
    rows.Select(r => new LogEntry { Source = r.source, Message = r.msg, Level = r.level, Timestamp = r.ts }).ToList();

TestHarness.Run("BuildClusters groups messages with different IDs into one cluster", () =>
{
    var entries = MakeEntries(
        ("Svc", "User 1 not found", LogLevel.Error, new DateTime(2026, 1, 1)),
        ("Svc", "User 2 not found", LogLevel.Error, new DateTime(2026, 1, 1, 0, 1, 0)),
        ("Svc", "User 3 not found", LogLevel.Error, new DateTime(2026, 1, 1, 0, 2, 0))
    );
    var clusters = TriageEngine.BuildClusters(entries);
    TestHarness.AssertEqual(1, clusters.Count, "should be exactly one cluster");
    TestHarness.AssertEqual(3, clusters[0].Count, "cluster should contain all 3 entries");
});

TestHarness.Run("BuildClusters keeps different sources separate even with the same message", () =>
{
    var entries = MakeEntries(
        ("SvcA", "Timeout", LogLevel.Error, new DateTime(2026, 1, 1)),
        ("SvcB", "Timeout", LogLevel.Error, new DateTime(2026, 1, 1))
    );
    var clusters = TriageEngine.BuildClusters(entries);
    TestHarness.AssertEqual(2, clusters.Count, "different sources should not merge");
});

TestHarness.Run("Rank scores a more frequent error higher than a rare warning", () =>
{
    var entries = MakeEntries(
        ("Svc", "Frequent error", LogLevel.Error, new DateTime(2026, 1, 1, 10, 0, 0)),
        ("Svc", "Frequent error", LogLevel.Error, new DateTime(2026, 1, 1, 10, 1, 0)),
        ("Svc", "Frequent error", LogLevel.Error, new DateTime(2026, 1, 1, 10, 2, 0)),
        ("Svc", "Rare warning", LogLevel.Warn, new DateTime(2026, 1, 1, 10, 0, 0))
    );
    var asOf = new DateTime(2026, 1, 1, 10, 5, 0);
    var ranked = TriageEngine.Rank(TriageEngine.BuildClusters(entries), asOf);
    TestHarness.AssertEqual("Frequent error", ranked[0].SampleMessage, "top-ranked cluster");
});

TestHarness.Run("Rank penalizes an issue that hasn't recurred recently", () =>
{
    var oldEntries = MakeEntries(
        ("Svc", "Old issue", LogLevel.Error, new DateTime(2026, 1, 1, 10, 0, 0))
    );
    var freshEntries = MakeEntries(
        ("Svc", "Fresh issue", LogLevel.Error, new DateTime(2026, 1, 10, 9, 59, 0))
    );
    var all = oldEntries.Concat(freshEntries).ToList();
    var asOf = new DateTime(2026, 1, 10, 10, 0, 0); // 9 days after the old one, 1 minute after the fresh one

    var ranked = TriageEngine.Rank(TriageEngine.BuildClusters(all), asOf);
    TestHarness.AssertEqual("Fresh issue", ranked[0].SampleMessage, "recently-seen issue should outrank a stale one of equal severity/count");
});

TestHarness.Run("Rank scores FATAL above ERROR at equal frequency and recency", () =>
{
    var entries = MakeEntries(
        ("Svc", "Bad crash", LogLevel.Fatal, new DateTime(2026, 1, 1, 10, 0, 0)),
        ("Svc", "Regular error", LogLevel.Error, new DateTime(2026, 1, 1, 10, 0, 0))
    );
    var ranked = TriageEngine.Rank(TriageEngine.BuildClusters(entries), new DateTime(2026, 1, 1, 10, 0, 0));
    TestHarness.AssertEqual("Bad crash", ranked[0].SampleMessage, "FATAL should outrank ERROR when count and recency are equal");
});

TestHarness.Run("Full pipeline: parse real sample log and verify known clustering", () =>
{
    var lines = File.ReadAllLines("../../sample-data/production.log");
    var entries = LogParser.Parse(lines);
    var clusters = TriageEngine.BuildClusters(entries);

    var inventoryCluster = clusters.First(c => c.Source == "InventoryService");
    TestHarness.AssertEqual(5, inventoryCluster.Count, "5 distinct user IDs should cluster into 1 issue");

    var jobStarted = clusters.First(c => c.SampleMessage.Contains("started"));
    TestHarness.AssertEqual(3, jobStarted.Count, "3 job-started info lines should cluster together");
});

return TestHarness.Summarize();
