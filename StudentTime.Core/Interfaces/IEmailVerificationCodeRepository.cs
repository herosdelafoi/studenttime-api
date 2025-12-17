using StudentTime.Core.Entities;

namespace StudentTime.Core.Interfaces;

public interface IEmailVerificationCodeRepository
{
    Task<EmailVerificationCode?> GetByEmailAndCodeAsync(string email, string code);
    Task<EmailVerificationCode> AddAsync(EmailVerificationCode entity);
    Task UpdateAsync(EmailVerificationCode entity);
    Task DeleteExpiredCodesAsync();
}

