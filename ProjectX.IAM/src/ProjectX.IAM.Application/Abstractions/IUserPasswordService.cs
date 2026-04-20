using ProjectX.IAM.Domain.Entities;

namespace ProjectX.IAM.Application.Abstractions;

public interface IUserPasswordService
{
    string HashPassword(User user, string password);
}
