using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Configuration;
using TaxiWPF.Models;

namespace TaxiWPF.Repositories
{
    public class UserRepository
    {
        private readonly string _connectionString;

        // --- ЗАГЛУШКА: Временное хранилище в памяти ---
        private static readonly List<User> _inMemoryUsers = new List<User>
        {
            new User { user_id = 1, username = "test_client", password = "123", email = "client@example.com", role = "Client" },
            new User { user_id = 2, username = "test_driver", password = "123", email = "driver@example.com", role = "Driver" }
        };
        private static readonly Dictionary<string, Tuple<int, DateTime>> _inMemoryTokens = new Dictionary<string, Tuple<int, DateTime>>();
        // ---------------------------------------------

        public UserRepository()
        {
            //_connectionString = ConfigurationManager.ConnectionStrings["TaxiDB"].ConnectionString;
        }

        public User GetUserByUsername(string username)
        {
            // --- ЗАГЛУШКА ---
            return _inMemoryUsers.FirstOrDefault(u => u.username.Equals(username, StringComparison.OrdinalIgnoreCase));
            // ------------------

            /*
            // --- РЕАЛЬНЫЙ КОД ДЛЯ БД (раскомментировать, когда будет доступ) ---
            User user = null;
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                var command = new SqlCommand("SELECT * FROM Users WHERE username = @username", connection);
                command.Parameters.AddWithValue("@username", username);
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        user = new User
                        {
                            user_id = (int)reader["user_id"],
                            username = reader["username"].ToString(),
                            password = reader["password"].ToString(),
                            email = reader["email"].ToString(),
                            role = reader["role"].ToString()
                        };
                    }
                }
            }
            return user;
            */
        }

        public bool AddUser(User user)
        {
            // --- ЗАГЛУШКА ---
            if (_inMemoryUsers.Any(u => u.username.Equals(user.username, StringComparison.OrdinalIgnoreCase))) return false;
            user.user_id = _inMemoryUsers.Count + 1;
            _inMemoryUsers.Add(user);
            return true;
            // ------------------

            /*
            // --- РЕАЛЬНЫЙ КОД ДЛЯ БД ---
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                // Проверка на уникальность
                var checkCmd = new SqlCommand("SELECT COUNT(*) FROM Users WHERE username = @username OR email = @email", connection);
                checkCmd.Parameters.AddWithValue("@username", user.username);
                checkCmd.Parameters.AddWithValue("@email", user.email);
                if ((int)checkCmd.ExecuteScalar() > 0) return false;

                var command = new SqlCommand("INSERT INTO Users (username, password, email, role) VALUES (@username, @password, @email, @role)", connection);
                command.Parameters.AddWithValue("@username", user.username);
                command.Parameters.AddWithValue("@password", user.password);
                command.Parameters.AddWithValue("@email", user.email);
                command.Parameters.AddWithValue("@role", user.role);
                command.ExecuteNonQuery();
                return true;
            }
            */
        }

        public string CreatePasswordResetToken(string email)
        {
            // --- ЗАГЛУШКА ---
            var user = _inMemoryUsers.FirstOrDefault(u => u.email.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (user == null) return null;

            string token = Guid.NewGuid().ToString();
            _inMemoryTokens[token] = Tuple.Create(user.user_id, DateTime.UtcNow.AddHours(1));
            return token;
            // ------------------

            /*
            // --- РЕАЛЬНЫЙ КОД ДЛЯ БД ---
            using (var connection = new SqlConnection(_connectionString))
            {
                 connection.Open();
                 var userCmd = new SqlCommand("SELECT user_id FROM Users WHERE email = @email", connection);
                 userCmd.Parameters.AddWithValue("@email", email);
                 var userIdObj = userCmd.ExecuteScalar();
                 if (userIdObj == null) return null;

                 int userId = (int)userIdObj;
                 string token = Guid.NewGuid().ToString();
                 var expiration = DateTime.UtcNow.AddHours(1);

                 var tokenCmd = new SqlCommand("INSERT INTO PasswordResetTokens (user_id, token, expiration_date) VALUES (@user_id, @token, @expiration)", connection);
                 tokenCmd.Parameters.AddWithValue("@user_id", userId);
                 tokenCmd.Parameters.AddWithValue("@token", token);
                 tokenCmd.Parameters.AddWithValue("@expiration", expiration);
                 tokenCmd.ExecuteNonQuery();
                 
                 return token;
            }
            */
        }

        public bool ResetPasswordWithToken(string token, string newPassword)
        {
            // --- ЗАГЛУШКА ---
            if (_inMemoryTokens.TryGetValue(token, out var tokenData) && tokenData.Item2 > DateTime.UtcNow)
            {
                var user = _inMemoryUsers.FirstOrDefault(u => u.user_id == tokenData.Item1);
                if (user != null)
                {
                    user.password = newPassword;
                    _inMemoryTokens.Remove(token); // Токен использован
                    return true;
                }
            }
            return false;
            // ------------------

            /*
            // --- РЕАЛЬНЫЙ КОД ДЛЯ БД ---
            int? userId = null;
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                var tokenCmd = new SqlCommand("SELECT user_id FROM PasswordResetTokens WHERE token = @token AND expiration_date > @now", connection);
                tokenCmd.Parameters.AddWithValue("@token", token);
                tokenCmd.Parameters.AddWithValue("@now", DateTime.UtcNow);
                var userIdObj = tokenCmd.ExecuteScalar();

                if (userIdObj != null)
                {
                    userId = (int)userIdObj;

                    // Обновляем пароль
                    var updateCmd = new SqlCommand("UPDATE Users SET password = @password WHERE user_id = @user_id", connection);
                    updateCmd.Parameters.AddWithValue("@password", newPassword);
                    updateCmd.Parameters.AddWithValue("@user_id", userId.Value);
                    updateCmd.ExecuteNonQuery();

                    // Удаляем токен
                    var deleteCmd = new SqlCommand("DELETE FROM PasswordResetTokens WHERE token = @token", connection);
                    deleteCmd.Parameters.AddWithValue("@token", token);
                    deleteCmd.ExecuteNonQuery();
                    
                    return true;
                }
            }
            return false;
            */
        }
    }
}
