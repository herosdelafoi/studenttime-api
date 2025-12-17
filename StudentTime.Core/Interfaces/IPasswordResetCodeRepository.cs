using StudentTime.Core.Entities;

namespace StudentTime.Core.Interfaces;

public interface IPasswordResetCodeRepository
{
    Task<PasswordResetCode?> GetByEmailAndCodeAsync(string email, string code);
    Task<PasswordResetCode> AddAsync(PasswordResetCode entity);
    Task UpdateAsync(PasswordResetCode entity);
    Task DeleteExpiredCodesAsync();
}

