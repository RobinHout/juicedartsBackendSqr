using System.Globalization;
using System.Text;
using YourNamespace.Models;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

namespace YourNamespace.Services
{
    public class CsvScoreRepository : IScoreRepository
    {
        private static readonly object _fileLock = new();
        private readonly string _csvPath;

        public CsvScoreRepository(IWebHostEnvironment env, IConfiguration config)
        {
            // You can override via appsettings.json: "ScoresCsv:Path": "Data/scores.csv"
            var relative = config["ScoresCsv:Path"] ?? "Data/scores.csv";
            _csvPath = Path.Combine(env.ContentRootPath, relative);

            // Ensure directory exists
            var dir = Path.GetDirectoryName(_csvPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Ensure header exists
            if (!File.Exists(_csvPath))
            {
                lock (_fileLock)
                {
                    if (!File.Exists(_csvPath))
                    {
                        File.WriteAllText(_csvPath, "User,Score,Bull,Date" + Environment.NewLine, Encoding.UTF8);
                    }
                }
            }
        }
        public Task AppendManyAsync(IEnumerable<ScoreEntry> entries, CancellationToken ct = default)
        {
            var sb = new StringBuilder();

            static string Csv(string s) => $"\"{(s ?? string.Empty).Replace("\"", "\"\"")}\"";

            foreach (var e in entries)
            {
                var user = e.User ?? string.Empty;
                var score = e.Score.ToString(CultureInfo.InvariantCulture);
                var bull = e.Bull.ToString(CultureInfo.InvariantCulture);
                var date = (e.Date ?? DateTimeOffset.UtcNow).ToUniversalTime()
                    .ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

                sb.Append(string.Join(",", new[] { Csv(user), Csv(score), Csv(bull), Csv(date) }))
                  .AppendLine();
            }

            lock (_fileLock)
            {
                File.AppendAllText(_csvPath, sb.ToString(), Encoding.UTF8);
            }

            return Task.CompletedTask;
        }


        public Task AppendAsync(ScoreEntry entry, CancellationToken ct = default)
        {
            // Defensive: normalize values
            var user = entry.User ?? string.Empty;
            var score = entry.Score.ToString(CultureInfo.InvariantCulture);
            var bull = entry.Bull.ToString(CultureInfo.InvariantCulture);
            var date = (entry.Date ?? DateTimeOffset.UtcNow).ToUniversalTime()
                .ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

            // CSV-escape each field (wrap in quotes, double-up inner quotes)
            static string Csv(string s) => $"\"{s.Replace("\"", "\"\"")}\"";

            var line = string.Join(",", new[]
            {
                Csv(user),
                Csv(score),
                Csv(bull),
                Csv(date)
            }) + Environment.NewLine;

            // File append with a lock to prevent interleaved writes under load
            lock (_fileLock)
            {
                File.AppendAllText(_csvPath, line, Encoding.UTF8);
            }

            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<ScoreEntry>> ReadAllAsync(CancellationToken ct = default)
        {
            var result = new List<ScoreEntry>();

            if (!File.Exists(_csvPath))
                return Task.FromResult((IReadOnlyList<ScoreEntry>)result);

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                PrepareHeaderForMatch = args => args.Header.Trim(),
                MissingFieldFound = null,
                BadDataFound = null
            };

            lock (_fileLock) // consistente read tijdens writes
            {
                using var stream = File.Open(_csvPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                using var csv = new CsvReader(reader, config);

                // Header lezen (User,Score,Bull,Date)
                if (!csv.Read() || !csv.ReadHeader())
                    return Task.FromResult((IReadOnlyList<ScoreEntry>)result);

                while (csv.Read())
                {
                    ct.ThrowIfCancellationRequested();

                    var user = csv.GetField("User") ?? string.Empty;

                    // Score
                    int score = 0;
                    int.TryParse(csv.GetField("Score"), NumberStyles.Integer, CultureInfo.InvariantCulture, out score);

                    // Bull (true/false, 0/1, yes/no)
                    var bull = 0;
                    int.TryParse(csv.GetField("Bull"), NumberStyles.Integer, CultureInfo.InvariantCulture, out score);

                    // Date (optioneel)
                    DateTimeOffset? when = null;
                    var dateField = csv.GetField("Date");
                    if (!string.IsNullOrWhiteSpace(dateField) &&
                        DateTimeOffset.TryParse(dateField, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
                    {
                        when = parsed;
                    }

                    result.Add(new ScoreEntry
                    {
                        User = user,
                        Score = score,
                        Bull = bull,
                        Date = when
                    });
                }
            }

            return Task.FromResult((IReadOnlyList<ScoreEntry>)result);
        }
    }
}
