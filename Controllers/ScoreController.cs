using Microsoft.AspNetCore.Mvc;
using YourNamespace.Models;
using YourNamespace.Services;
using System.Linq;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Http;


namespace YourNamespace.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ScoresController : ControllerBase
    {
        private readonly IScoreRepository _repo;

        public ScoresController(IScoreRepository repo)
        {
            _repo = repo;
        }

        // POST: /api/scores
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] ScoreEntry request, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            // Default date to now (UTC) if client didn’t send one
            if (request.Date is null)
                request.Date = DateTimeOffset.UtcNow;

            await _repo.AppendAsync(request, ct);

            return Created(string.Empty, new
            {
                message = "Row appended to CSV.",
                request.User,
                request.Score,
                request.Bull,
                Date = request.Date.Value.UtcDateTime
            });
        }
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? user, CancellationToken ct)
        {
            var all = await _repo.ReadAllAsync(ct);

            if (!string.IsNullOrWhiteSpace(user))
                all = all.Where(e => string.Equals(e.User, user, StringComparison.OrdinalIgnoreCase)).ToList();

            return Ok(all);
        }

        // GET: /api/scores/by-user        -> gegroepeerd per user
        [HttpGet("by-user")]
        public async Task<IActionResult> GetByUser(CancellationToken ct)
        {
            var all = await _repo.ReadAllAsync(ct);

            var grouped = all
                .GroupBy(e => e.User ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.ToList(),
                    StringComparer.OrdinalIgnoreCase
                );

            return Ok(grouped);
        }
        [HttpGet("LastTwenty")]
        public async Task<IActionResult> GetLastTwenty(CancellationToken ct)
        {
            var all = await _repo.ReadAllAsync(ct);

            var lastTwenty = all
                .OrderByDescending(e => e.Date)
                .Take(10)
                .ToList();

            return Ok(lastTwenty);
        }
        [HttpGet("TopTenLastWeek")]
        public async Task<IActionResult> GetTopTenLastWeek(CancellationToken ct)
        {
            var all = await _repo.ReadAllAsync(ct);
            var oneWeekAgo = DateTimeOffset.UtcNow.AddDays(-7);

            var topTenLastWeek = all
                .Where(e => e.Date >= oneWeekAgo)
                .OrderByDescending(e => e.Score)
                .ThenBy(e => e.Date) // Bij gelijke scores, de oudste eerst
                .Take(10)
                .ToList();

            return Ok(topTenLastWeek);
        }
    }

}

// [HttpPost("bulk")]
// public async Task<IActionResult> Bulk([FromBody] List<ScoreEntry> entries, CancellationToken ct)
// {
//     if (entries is null || entries.Count == 0)
//         return BadRequest("Geen records ontvangen.");

//     // Date defaulten naar UtcNow wanneer niet gezet
//     foreach (var e in entries)
//         e.Date ??= DateTimeOffset.UtcNow;

//     await _repo.AppendManyAsync(entries, ct);
//     return Created(string.Empty, new { message = $"Toegevoegd: {entries.Count} regels." });
// }

// // POST: /api/scores/upload (multipart/form-data met CSV)
// [HttpPost("upload")]
// [Consumes("multipart/form-data")]
// public async Task<IActionResult> Upload([FromForm] IFormFile file, CancellationToken ct)
// {
//     if (file is null || file.Length == 0)
//         return BadRequest("Upload een niet-leeg CSV-bestand met kolommen: User,Score,Bull,Date.");

//     // CSV lezen met CsvHelper
//     var config = new CsvConfiguration(CultureInfo.InvariantCulture)
//     {
//         HasHeaderRecord = true,
//         PrepareHeaderForMatch = args => args.Header.Trim(),
//         MissingFieldFound = null,
//         BadDataFound = null
//     };

//     var list = new List<ScoreEntry>();

//     using var stream = file.OpenReadStream();
//     using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
//     using var csv = new CsvReader(reader, config);

//     // Verwacht headers: User, Score, Bull, Date
//     while (await csv.ReadAsync())
//     {
//         // Valideer aanwezigheid verplichte velden
//         var user = csv.GetField("User");
//         if (string.IsNullOrWhiteSpace(user))
//             return BadRequest("Kolom 'User' ontbreekt of is leeg in een van de rijen.");

//         // Score
//         if (!int.TryParse(csv.GetField("Score"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var score))
//             return BadRequest("Kolom 'Score' bevat een ongeldige waarde.");

//         // Bull
//         var bullField = csv.GetField("Bull");
//         if (!bool.TryParse(bullField, out var bull))
//         {
//             // Sta 0/1 en yes/no toe
//             bull = bullField?.Trim().ToLowerInvariant() switch
//             {
//                 "1" or "yes" or "y" or "true" => true,
//                 "0" or "no" or "n" or "false" => false,
//                 _ => throw new FormatException("Kolom 'Bull' bevat een ongeldige waarde.")
//             };
//         }

//         // Date (optioneel)
//         DateTimeOffset? when = null;
//         var dateField = csv.GetField("Date");
//         if (!string.IsNullOrWhiteSpace(dateField))
//         {
//             if (!DateTimeOffset.TryParse(dateField, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
//                 return BadRequest("Kolom 'Date' bevat een ongeldige datum/tijd.");
//             when = parsed;
//         }

//         list.Add(new ScoreEntry
//         {
//             User = user,
//             Score = score,
//             Bull = bull,
//             Date = when ?? DateTimeOffset.UtcNow
//         });
//     }

//     if (list.Count == 0)
//         return BadRequest("Geen geldige rijen gevonden in het CSV-bestand.");

//     await _repo.AppendManyAsync(list, ct);
//     return Created(string.Empty, new { message = $"Geüpload: {list.Count} nieuwe scores." });
// }