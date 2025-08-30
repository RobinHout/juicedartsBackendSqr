// Services/IScoreRepository.cs
using YourNamespace.Models;

namespace YourNamespace.Services
{
    public interface IScoreRepository
    {
        Task AppendAsync(ScoreEntry entry, CancellationToken ct = default);
        Task AppendManyAsync(IEnumerable<ScoreEntry> entries, CancellationToken ct = default);

        // ✅ Nieuw: alles lezen
        Task<IReadOnlyList<ScoreEntry>> ReadAllAsync(CancellationToken ct = default);
    }
}
