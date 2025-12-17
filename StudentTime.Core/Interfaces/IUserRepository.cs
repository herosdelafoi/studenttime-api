using StudentTime.Core.Entities;

namespace StudentTime.Core.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByGoogleIdAsync(string googleId);
    Task<bool> EmailExistsAsync(string email);
}

