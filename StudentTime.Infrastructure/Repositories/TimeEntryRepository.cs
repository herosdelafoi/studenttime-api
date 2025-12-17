using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StudentTime.Core.Entities;
using StudentTime.Core.Interfaces;
using StudentTime.Infrastructure.Data;

namespace StudentTime.Infrastructure.Repositories;

public class TimeEntryRepository : ITimeEntryRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<TimeEntryRepository> _logger;

    public TimeEntryRepository(AppDbContext context, ILogger<TimeEntryRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<TimeEntry?> GetByIdAsync(string id)
    {
        return await _context.TimeEntries
            .FirstOrDefaultAsync(te => te.Id == id && te.IsDeleted == false);
    }

    public async Task<TimeEntry?> GetActiveEntryAsync(string userId)
    {
        return await _context.TimeEntries
            .FirstOrDefaultAsync(te => te.UserId == userId && te.EndTime == null && te.IsDeleted == false);
    }

    public async Task<bool> HasActiveEntryAsync(string userId)
    {
        try
        {
            _logger.LogInformation("Vérification des sessions actives pour l'utilisateur {UserId}", userId);
            var result = await _context.TimeEntries
                .AnyAsync(te => te.UserId == userId && te.EndTime == null && te.IsDeleted == false);
            _logger.LogInformation("Résultat de la vérification des sessions actives pour {UserId}: {Result}", userId, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la vérification des sessions actives pour l'utilisateur {UserId}. Message: {Message}", userId, ex.Message);
            throw;
        }
    }

    public async Task<IEnumerable<TimeEntry>> GetByUserIdAsync(string userId, int skip = 0, int take = 20)
    {
        return await _context.TimeEntries
            .Where(te => te.UserId == userId && te.IsDeleted == false)
            .OrderByDescending(te => te.StartTime)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<IEnumerable<TimeEntry>> GetByUserIdAndDateRangeAsync(
        string userId,
        DateTime startDate,
        DateTime endDate)
    {
        return await _context.TimeEntries
            .Where(te => te.UserId == userId
                && te.IsDeleted == false
                && te.StartTime >= startDate
                && te.StartTime <= endDate)
            .OrderByDescending(te => te.StartTime)
            .ToListAsync();
    }

    public async Task<int> GetTotalSecondsAsync(string userId, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _context.TimeEntries
            .Where(te => te.UserId == userId && te.IsDeleted == false && te.DurationSeconds != null);

        if (startDate.HasValue)
            query = query.Where(te => te.StartTime >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(te => te.StartTime <= endDate.Value);

        return await query.SumAsync(te => te.DurationSeconds ?? 0);
    }

    public async Task<IEnumerable<TimeEntry>> GetAllAsync()
    {
        return await _context.TimeEntries
            .Where(te => te.IsDeleted == false)
            .ToListAsync();
    }

    public async Task<TimeEntry> AddAsync(TimeEntry entity)
    {
        try
        {
            _logger.LogInformation("Ajout d'une nouvelle session en base de données. UserId: {UserId}, Title: {Title}", entity.UserId, entity.Title);
            entity.CreatedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;
            _context.TimeEntries.Add(entity);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Session ajoutée avec succès. Id: {Id}", entity.Id);
            return entity;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'ajout de la session en base de données. UserId: {UserId}, Title: {Title}. Message: {Message}", 
                entity.UserId, entity.Title, ex.Message);
            _logger.LogError(ex, "Stack trace: {StackTrace}", ex.StackTrace);
            if (ex.InnerException != null)
            {
                _logger.LogError(ex.InnerException, "Inner exception: {Message}", ex.InnerException.Message);
            }
            throw;
        }
    }

    public async Task UpdateAsync(TimeEntry entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _context.TimeEntries.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(string id)
    {
        var entry = await GetByIdAsync(id);
        if (entry != null)
        {
            entry.IsDeleted = true;
            entry.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(string id)
    {
        return await _context.TimeEntries.AnyAsync(te => te.Id == id && te.IsDeleted == false);
    }
}

