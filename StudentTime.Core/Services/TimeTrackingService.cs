using StudentTime.Core.DTOs.TimeTracking;
using StudentTime.Core.Entities;
using StudentTime.Core.Exceptions;
using StudentTime.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace StudentTime.Core.Services;

public class TimeTrackingService : ITimeTrackingService
{
    private readonly ITimeEntryRepository _timeEntryRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<TimeTrackingService> _logger;

    public TimeTrackingService(ITimeEntryRepository timeEntryRepository, IUserRepository userRepository, ILogger<TimeTrackingService> logger)
    {
        _timeEntryRepository = timeEntryRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<TimeEntryResponse> StartEntryAsync(string userId, StartTimeEntryRequest request)
    {
        try
        {
            _logger.LogInformation("Démarrage d'une session pour l'utilisateur {UserId}", userId);
            
            if (!await _userRepository.ExistsAsync(userId))
            {
                _logger.LogWarning("Utilisateur {UserId} introuvable", userId);
                throw new NotFoundException("Utilisateur introuvable");
            }

            _logger.LogInformation("Vérification des sessions actives pour l'utilisateur {UserId}", userId);
            if (await _timeEntryRepository.HasActiveEntryAsync(userId))
            {
                _logger.LogWarning("Une session est déjà en cours pour l'utilisateur {UserId}", userId);
                throw new BusinessException("Une session est déjà en cours. Arrêtez-la d'abord.");
            }

            var entry = new TimeEntry
            {
                UserId = userId,
                Title = request.Title,
                StartTime = DateTime.UtcNow,
                Notes = request.Notes,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _logger.LogInformation("Ajout de la session en base de données pour l'utilisateur {UserId}", userId);
            await _timeEntryRepository.AddAsync(entry);
            _logger.LogInformation("Session créée avec succès avec l'ID {EntryId}", entry.Id);
            
            return MapToResponse(entry);
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (NotFoundException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du démarrage d'une session pour l'utilisateur {UserId}. Message: {Message}", userId, ex.Message);
            throw;
        }
    }

    public async Task<TimeEntryResponse> CreateEntryAsync(string userId, CreateTimeEntryRequest request)
    {
        if (!await _userRepository.ExistsAsync(userId))
        {
            throw new NotFoundException("Utilisateur introuvable");
        }

        if (request.EndTime <= request.StartTime)
        {
            throw new BusinessException("L'heure de fin doit être après l'heure de début");
        }

        // Donner une marge de 5 minutes pour tenir compte des décalages horaires
        var maxAllowedTime = DateTime.UtcNow.AddMinutes(5);
        if (request.EndTime > maxAllowedTime)
        {
            throw new BusinessException("Vous ne pouvez pas enregistrer une session future");
        }

        var entry = new TimeEntry
        {
            UserId = userId,
            Title = request.Title,
            StartTime = request.StartTime.ToUniversalTime(),
            EndTime = request.EndTime.ToUniversalTime(),
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        entry.DurationSeconds = entry.CalculateDuration();

        await _timeEntryRepository.AddAsync(entry);
        return MapToResponse(entry);
    }

    public async Task<TimeEntryResponse> StopEntryAsync(string userId, string entryId)
    {
        var entry = await _timeEntryRepository.GetByIdAsync(entryId);
        if (entry == null)
        {
            throw new NotFoundException("Session introuvable");
        }

        if (entry.UserId != userId)
        {
            throw new BusinessException("Accès non autorisé à cette session");
        }

        if (entry.EndTime != null)
        {
            throw new BusinessException("Cette session est déjà terminée");
        }

        entry.Complete();
        await _timeEntryRepository.UpdateAsync(entry);

        return MapToResponse(entry);
    }

    public async Task<TimeEntryResponse?> GetActiveEntryAsync(string userId)
    {
        var entry = await _timeEntryRepository.GetActiveEntryAsync(userId);
        return entry != null ? MapToResponse(entry) : null;
    }

    public async Task<IEnumerable<TimeEntryResponse>> GetEntriesAsync(string userId, int page = 1, int pageSize = 20)
    {
        if (page < 1) page = 1;
        // Enlever la limite de 100 pour permettre de récupérer toutes les sessions
        // La pagination est gérée côté frontend pour une meilleure UX
        if (pageSize < 1 || pageSize > 100000) pageSize = 10000;

        var skip = (page - 1) * pageSize;
        var entries = await _timeEntryRepository.GetByUserIdAsync(userId, skip, pageSize);
        return entries.Select(MapToResponse);
    }

    public async Task<TimeEntryResponse> UpdateEntryAsync(string userId, string entryId, UpdateTimeEntryRequest request)
    {
        var entry = await _timeEntryRepository.GetByIdAsync(entryId);
        if (entry == null)
        {
            throw new NotFoundException("Session introuvable");
        }

        if (entry.UserId != userId)
        {
            throw new BusinessException("Accès non autorisé à cette session");
        }

        if (entry.IsActive && (request.StartTime.HasValue || request.EndTime.HasValue))
        {
            throw new BusinessException("Arrêtez la session avant de modifier les horaires");
        }

        if (request.StartTime.HasValue)
        {
            entry.StartTime = request.StartTime.Value;
        }

        if (request.EndTime.HasValue)
        {
            if (request.EndTime.Value <= entry.StartTime)
            {
                throw new BusinessException("L'heure de fin doit être après l'heure de début");
            }

            // Donner une marge de 5 minutes pour tenir compte des décalages horaires
            var maxAllowedTime = DateTime.UtcNow.AddMinutes(5);
            if (request.EndTime.Value > maxAllowedTime)
            {
                throw new BusinessException("Vous ne pouvez pas enregistrer une session future");
            }

            entry.EndTime = request.EndTime.Value;
            entry.DurationSeconds = entry.CalculateDuration();
        }

        if (request.Title != null)
        {
            entry.Title = request.Title;
        }

        if (request.Notes != null)
        {
            entry.Notes = request.Notes;
        }

        entry.UpdatedAt = DateTime.UtcNow;
        await _timeEntryRepository.UpdateAsync(entry);

        return MapToResponse(entry);
    }

    public async Task DeleteEntryAsync(string userId, string entryId)
    {
        var entry = await _timeEntryRepository.GetByIdAsync(entryId);
        if (entry == null)
        {
            throw new NotFoundException("Session introuvable");
        }

        if (entry.UserId != userId)
        {
            throw new BusinessException("Accès non autorisé à cette session");
        }

        if (entry.IsActive)
        {
            throw new BusinessException("Arrêtez la session avant de la supprimer");
        }

        await _timeEntryRepository.DeleteAsync(entryId);
    }

    public async Task<TimeEntryStatsResponse> GetStatsAsync(string userId, DateTime? startDate = null, DateTime? endDate = null)
    {
        if (!await _userRepository.ExistsAsync(userId))
        {
            throw new NotFoundException("Utilisateur introuvable");
        }

        startDate ??= new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        endDate ??= DateTime.UtcNow;

        var entries = await _timeEntryRepository.GetByUserIdAndDateRangeAsync(userId, startDate.Value, endDate.Value);
        var completedEntries = entries.Where(e => e.DurationSeconds.HasValue).ToList();

        var totalSeconds = completedEntries.Sum(e => e.DurationSeconds ?? 0);
        var totalEntries = completedEntries.Count;

        var daysDiff = (int)(endDate.Value - startDate.Value).TotalDays + 1;
        var averageSecondsPerDay = daysDiff > 0 ? totalSeconds / daysDiff : 0;

        var secondsByDay = completedEntries
            .GroupBy(e => e.StartTime.Date.ToString("yyyy-MM-dd"))
            .ToDictionary(
                g => g.Key,
                g => g.Sum(e => e.DurationSeconds ?? 0)
            );

        return new TimeEntryStatsResponse
        {
            TotalSeconds = totalSeconds,
            TotalEntries = totalEntries,
            AverageSecondsPerDay = averageSecondsPerDay,
            SecondsByDay = secondsByDay
        };
    }

    private static TimeEntryResponse MapToResponse(TimeEntry entry)
    {
        return new TimeEntryResponse
        {
            Id = entry.Id,
            Title = entry.Title,
            StartTime = entry.StartTime,
            EndTime = entry.EndTime,
            DurationSeconds = entry.DurationSeconds,
            Notes = entry.Notes,
            IsActive = entry.IsActive,
            CreatedAt = entry.CreatedAt
        };
    }
}

