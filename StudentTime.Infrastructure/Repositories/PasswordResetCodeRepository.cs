using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StudentTime.Core.Entities;
using StudentTime.Core.Interfaces;
using StudentTime.Infrastructure.Data;

namespace StudentTime.Infrastructure.Repositories;

public class PasswordResetCodeRepository : IPasswordResetCodeRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<PasswordResetCodeRepository> _logger;

    public PasswordResetCodeRepository(AppDbContext context, ILogger<PasswordResetCodeRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PasswordResetCode?> GetByEmailAndCodeAsync(string email, string code)
    {
        // Récupérer tous les codes correspondants et filtrer en mémoire
        // Cela évite les problèmes de conversion TEXT vs TIMESTAMP
        var allCodes = await _context.PasswordResetCodes
            .Where(prc => 
                prc.Email == email.ToLower() && 
                prc.Code == code &&
                prc.IsUsed == false)
            .ToListAsync();
        
        // Filtrer en mémoire pour la comparaison de date (évite les problèmes de type)
        var now = DateTime.UtcNow;
        return allCodes
            .FirstOrDefault(prc => prc.ExpiresAt > now);
    }

    public async Task<PasswordResetCode> AddAsync(PasswordResetCode entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Email))
        {
            throw new ArgumentException("L'email ne peut pas être vide", nameof(entity));
        }
        entity.Email = entity.Email.ToLower();
        entity.CreatedAt = DateTime.UtcNow;
        // IsUsed est déjà initialisé à false par défaut dans l'entité
        _context.PasswordResetCodes.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(PasswordResetCode entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Email))
        {
            throw new ArgumentException("L'email ne peut pas être vide", nameof(entity));
        }
        entity.Email = entity.Email.ToLower();
        _context.PasswordResetCodes.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteExpiredCodesAsync()
    {
        try
        {
            // Solution temporaire : récupérer tous les codes et filtrer en mémoire
            // pour éviter le problème de comparaison TEXT vs TIMESTAMP dans PostgreSQL
            // TODO: Migrer les colonnes CreatedAt et ExpiresAt de TEXT vers TIMESTAMP pour PostgreSQL
            var allCodes = await _context.PasswordResetCodes.ToListAsync();
            
            var expiredCodes = allCodes
                .Where(prc => prc.ExpiresAt < DateTime.UtcNow || prc.IsUsed)
                .ToList();

            if (expiredCodes.Any())
            {
                _context.PasswordResetCodes.RemoveRange(expiredCodes);
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            // Logger mais ne pas faire échouer le processus
            // Les codes expirés seront supprimés lors de la prochaine tentative
            _logger.LogWarning(ex, "Erreur lors de la suppression des codes expirés");
            // Ne pas throw
        }
    }
}

