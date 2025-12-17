using System.ComponentModel.DataAnnotations;

namespace StudentTime.Core.DTOs.TimeTracking;

public class StartTimeEntryRequest
{
    [Required(ErrorMessage = "Le titre est requis")]
    [MaxLength(200, ErrorMessage = "Le titre ne peut pas dépasser 200 caractères")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000, ErrorMessage = "Les notes ne peuvent pas dépasser 1000 caractères")]
    public string? Notes { get; set; }
}

public class StopTimeEntryRequest
{
    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class CreateTimeEntryRequest
{
    [Required(ErrorMessage = "Le titre est requis")]
    [MaxLength(200, ErrorMessage = "Le titre ne peut pas dépasser 200 caractères")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "L'heure de début est requise")]
    public DateTime StartTime { get; set; }

    [Required(ErrorMessage = "L'heure de fin est requise")]
    public DateTime EndTime { get; set; }

    [MaxLength(1000, ErrorMessage = "Les notes ne peuvent pas dépasser 1000 caractères")]
    public string? Notes { get; set; }
}

public class UpdateTimeEntryRequest
{
    [MaxLength(200)]
    public string? Title { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class TimeEntryResponse
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int? DurationSeconds { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TimeEntryStatsResponse
{
    public int TotalSeconds { get; set; }
    public int TotalEntries { get; set; }
    public int AverageSecondsPerDay { get; set; }
    public Dictionary<string, int> SecondsByDay { get; set; } = new();
}

