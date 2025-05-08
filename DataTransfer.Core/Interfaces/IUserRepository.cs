using System.Threading.Tasks;
using DataTransfer.Core.Entities;

namespace DataTransfer.Core.Interfaces
{
    public interface IUserRepository
    {
        Task<UserLogin?> GetUserByUsernameAsync(string username);
        Task<UserLogin?> AuthenticateUserAsync(string username, string password);
        Task<bool> CreateUserAsync(UserLogin user);
        Task<bool> UpdateLastLoginDateAsync(int userId);
    }
} 