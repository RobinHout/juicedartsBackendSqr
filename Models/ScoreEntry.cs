using System.ComponentModel.DataAnnotations;

namespace YourNamespace.Models
{
    public class ScoreEntry
    {
        [Required]
        public string User { get; set; } = string.Empty;

        [Required]
        public int Score { get; set; }

        [Required]
        public int Bull { get; set; }

        // If not supplied, we’ll default to UtcNow in the controller
        public DateTimeOffset? Date { get; set; }
    }
}
