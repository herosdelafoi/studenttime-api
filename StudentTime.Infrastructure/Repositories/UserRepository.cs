using System;
using Microsoft.EntityFrameworkCore;
using StudentTime.Core.Entities;
using StudentTime.Core.Interfaces;
using StudentTime.Infrastructure.Data;

namespace StudentTime.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(string id)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Id == id && u.IsActive == true);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email.ToLower() && u.IsActive == true);
    }

    public async Task<User?> GetByGoogleIdAsync(string googleId)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.GoogleId == googleId && u.IsActive == true);
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _context.Users
            .AnyAsync(u => u.Email == email.ToLower() && u.IsActive == true);
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        return await _context.Users
            .Where(u => u.IsActive == true)
            .ToListAsync();
    }

    public async Task<User> AddAsync(User entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Email))
        {
            throw new ArgumentException("L'email ne peut pas être vide", nameof(entity));
        }
        entity.Email = entity.Email.ToLower();
        entity.CreatedAt = DateTime.UtcNow;
        _context.Users.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(User entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Email))
        {
            throw new ArgumentException("L'email ne peut pas être vide", nameof(entity));
        }
        entity.Email = entity.Email.ToLower();
        _context.Users.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(string id)
    {
        var user = await GetByIdAsync(id);
        if (user != null)
        {
            user.IsActive = false;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(string id)
    {
        return await _context.Users.AnyAsync(u => u.Id == id && u.IsActive == true);
    }
}

