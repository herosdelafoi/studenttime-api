using StudentTime.Core.Entities;

namespace StudentTime.Core.Interfaces;

public interface ITimeEntryRepository : IRepository<TimeEntry>
{
    Task<TimeEntry?> GetActiveEntryAsync(string userId);
    Task<IEnumerable<TimeEntry>> GetByUserIdAsync(string userId, int skip = 0, int take = 20);
    Task<IEnumerable<TimeEntry>> GetByUserIdAndDateRangeAsync(
        string userId,
        DateTime startDate,
        DateTime endDate);
    Task<bool> HasActiveEntryAsync(string userId);
    Task<int> GetTotalSecondsAsync(string userId, DateTime? startDate = null, DateTime? endDate = null);
}

