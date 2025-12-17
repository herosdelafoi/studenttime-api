using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StudentTime.Core.Entities;
using StudentTime.Core.Interfaces;
using StudentTime.Infrastructure.Data;

namespace StudentTime.Infrastructure.Repositories;

public class EmailVerificationCodeRepository : IEmailVerificationCodeRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<EmailVerificationCodeRepository> _logger;

    public EmailVerificationCodeRepository(AppDbContext context, ILogger<EmailVerificationCodeRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<EmailVerificationCode?> GetByEmailAndCodeAsync(string email, string code)
    {
        var allCodes = await _context.EmailVerificationCodes
            .Where(evc => 
                evc.Email == email.ToLower() && 
                evc.Code == code &&
                evc.IsUsed == false)
            .ToListAsync();
        
        var now = DateTime.UtcNow;
        return allCodes
            .FirstOrDefault(evc => evc.ExpiresAt > now);
    }

    public async Task<EmailVerificationCode> AddAsync(EmailVerificationCode entity)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(entity.Email))
            {
                throw new ArgumentException("L'email ne peut pas être vide", nameof(entity));
            }
            entity.Email = entity.Email.ToLower();
            entity.CreatedAt = DateTime.UtcNow;
            _context.EmailVerificationCodes.Add(entity);
            await _context.SaveChangesAsync();
            return entity;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'ajout du code de vérification pour {Email}. Type: {Type}, Message: {Message}", 
                entity.Email, ex.GetType().Name, ex.Message);
            throw;
        }
    }

    public async Task UpdateAsync(EmailVerificationCode entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Email))
        {
            throw new ArgumentException("L'email ne peut pas être vide", nameof(entity));
        }
        entity.Email = entity.Email.ToLower();
        _context.EmailVerificationCodes.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteExpiredCodesAsync()
    {
        try
        {
            var allCodes = await _context.EmailVerificationCodes.ToListAsync();
            
            var expiredCodes = allCodes
                .Where(evc => evc.ExpiresAt < DateTime.UtcNow || evc.IsUsed)
                .ToList();

            if (expiredCodes.Any())
            {
                _context.EmailVerificationCodes.RemoveRange(expiredCodes);
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erreur lors de la suppression des codes de vérification expirés");
        }
    }
}

