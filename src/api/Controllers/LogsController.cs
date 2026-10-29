using Microsoft.AspNetCore.Mvc;
using Sherlock.Api.Services;

namespace Sherlock.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LogsController : ControllerBase
    {
        private readonly LogStore _store;

        public LogsController(LogStore store)
        {
            _store = store;
        }

        [HttpPost("ingest")]
        public IActionResult Ingest([FromBody] IngestRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RawLog))
            {
                return BadRequest(new { error = "rawLog is required" });
            }
            var count = _store.Ingest(request.RawLog);
            return Ok(new { entriesParsed = count, totalEntries = _store.EntryCount });
        }

        [HttpGet("triage")]
        public IActionResult Triage()
        {
            var report = _store.GetTriageReport(DateTime.UtcNow);
            return Ok(report.Select(c => new
            {
                c.Signature,
                c.Source,
                severity = c.HighestSeverity.ToString(),
                c.Count,
                c.FirstSeen,
                c.LastSeen,
                c.SampleMessage,
                c.TriageScore,
            }));
        }

        [HttpGet("clusters/{source}/{*signature}")]
        public IActionResult GetCluster(string source, string signature)
        {
            var cluster = _store.GetCluster(signature, source);
            if (cluster == null)
            {
                return NotFound(new { error = $"no cluster for {source}/{signature}" });
            }
            return Ok(cluster);
        }
    }

    public class IngestRequest
    {
        public string RawLog { get; set; } = string.Empty;
    }
}
