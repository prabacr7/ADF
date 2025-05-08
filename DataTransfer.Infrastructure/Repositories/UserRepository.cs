using System;
using System.Threading.Tasks;
using Dapper;
using DataTransfer.Core.Entities;
using DataTransfer.Core.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.IO;

namespace DataTransfer.Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly string _connectionString;

        public UserRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new ArgumentNullException(nameof(configuration));
        }

        public async Task<UserLogin?> GetUserByUsernameAsync(string username)
        {
            const string sql = "SELECT * FROM UserLogin WHERE UserName = @UserName";
            
            using var connection = new SqlConnection(_connectionString);
            return await connection.QuerySingleOrDefaultAsync<UserLogin>(sql, new { UserName = username });
        }

        public async Task<UserLogin?> AuthenticateUserAsync(string username, string password)
        {
            var user = await GetUserByUsernameAsync(username);
            
            if (user == null)
                return null;
                
            // Compare encrypted password
            if (user.Password != Encrypt(password))
                return null;
                
            await UpdateLastLoginDateAsync(user.UserId);
            return user;
        }

        public async Task<bool> CreateUserAsync(UserLogin user)
        {
            const string sql = @"
                INSERT INTO UserLogin (Name, UserName, Password, EmailAddress)
                VALUES (@Name, @UserName, @Password, @EmailAddress);
                SELECT CAST(SCOPE_IDENTITY() as int)";
                
            // Encrypt the password before storing
            user.Password = Encrypt(user.Password);
            
            using var connection = new SqlConnection(_connectionString);
            var userId = await connection.ExecuteScalarAsync<int>(sql, user);
            user.UserId = userId;
            
            return userId > 0;
        }

        public async Task<bool> UpdateLastLoginDateAsync(int userId)
        {
            const string sql = "UPDATE UserLogin SET LastLoginDate = @LastLoginDate WHERE UserId = @UserId";
            
            using var connection = new SqlConnection(_connectionString);
            var affectedRows = await connection.ExecuteAsync(sql, new { UserId = userId, LastLoginDate = DateTime.Now });
            
            return affectedRows > 0;
        }
        
        private static string Encrypt(string clearText)
        {
            string EncryptionKey = "MAKV2SPBNI99212";
            byte[] clearBytes = Encoding.Unicode.GetBytes(clearText);

            using (Aes encryptor = Aes.Create())
            {
                Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(EncryptionKey, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
                encryptor.Key = pdb.GetBytes(32);
                encryptor.IV = pdb.GetBytes(16);

                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(clearBytes, 0, clearBytes.Length);
                        cs.Close();
                    }

                    clearText = Convert.ToBase64String(ms.ToArray());
                }
            }

            return clearText;
        }
    }
} 