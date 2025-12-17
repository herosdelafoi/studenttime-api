using StudentTime.Core.DTOs.TimeTracking;

namespace StudentTime.Core.Interfaces;

public interface ITimeTrackingService
{
    Task<TimeEntryResponse> StartEntryAsync(string userId, StartTimeEntryRequest request);
    Task<TimeEntryResponse> CreateEntryAsync(string userId, CreateTimeEntryRequest request);
    Task<TimeEntryResponse> StopEntryAsync(string userId, string entryId);
    Task<TimeEntryResponse?> GetActiveEntryAsync(string userId);
    Task<IEnumerable<TimeEntryResponse>> GetEntriesAsync(string userId, int page = 1, int pageSize = 20);
    Task<TimeEntryResponse> UpdateEntryAsync(string userId, string entryId, UpdateTimeEntryRequest request);
    Task DeleteEntryAsync(string userId, string entryId);
    Task<TimeEntryStatsResponse> GetStatsAsync(string userId, DateTime? startDate = null, DateTime? endDate = null);
}

