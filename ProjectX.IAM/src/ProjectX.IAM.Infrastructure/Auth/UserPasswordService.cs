using Microsoft.AspNetCore.Identity;
using ProjectX.IAM.Application.Abstractions;
using ProjectX.IAM.Domain.Entities;

namespace ProjectX.IAM.Infrastructure.Auth;

public sealed class UserPasswordService(IPasswordHasher<User> passwordHasher) : IUserPasswordService
{
    public string HashPassword(User user, string password)
    {
        return passwordHasher.HashPassword(user, password);
    }
}
