using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;

namespace Bot
{
    public class CyberTask
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTime? ReminderDate { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public static class DatabaseHelper
    {
        
        private const string Server = "localhost";
        private const string Database = "cybersecurity_bot";
        private const string User = "root";
        private const string Password = "noluthando"; 

        private static string ConnectionString =>
            $"server={Server};database={Database};uid={User};pwd={Password};";

        
        public static void InitialiseDatabase()
        {
            
            string connNoDB =
                $"server={Server};uid={User};pwd={Password};";

            using var connCreate = new MySqlConnection(connNoDB);
            connCreate.Open();
            new MySqlCommand(
                $"CREATE DATABASE IF NOT EXISTS `{Database}`;",
                connCreate).ExecuteNonQuery();

            using var conn = new MySqlConnection(ConnectionString);
            conn.Open();
            new MySqlCommand(@"
                CREATE TABLE IF NOT EXISTS tasks (
                    id           INT AUTO_INCREMENT PRIMARY KEY,
                    title        VARCHAR(200)  NOT NULL,
                    description  TEXT,
                    reminder_date DATETIME     NULL,
                    is_completed  TINYINT(1)   NOT NULL DEFAULT 0,
                    created_at   DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP
                );", conn).ExecuteNonQuery();
        }

        public static int AddTask(string title, string description, DateTime? reminderDate)
        {
            using var conn = new MySqlConnection(ConnectionString);
            conn.Open();
            var cmd = new MySqlCommand(
                "INSERT INTO tasks (title, description, reminder_date) " +
                "VALUES (@t, @d, @r); SELECT LAST_INSERT_ID();", conn);
            cmd.Parameters.AddWithValue("@t", title);
            cmd.Parameters.AddWithValue("@d", description);
            cmd.Parameters.AddWithValue("@r", reminderDate.HasValue ? (object)reminderDate.Value : DBNull.Value);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public static List<CyberTask> GetAllTasks()
        {
            var list = new List<CyberTask>();
            using var conn = new MySqlConnection(ConnectionString);
            conn.Open();
            using var reader = new MySqlCommand(
                "SELECT id, title, description, reminder_date, is_completed, created_at " +
                "FROM tasks ORDER BY created_at DESC;", conn).ExecuteReader();
            while (reader.Read())
            {
                list.Add(new CyberTask
                {
                    Id = reader.GetInt32(0),
                    Title = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    ReminderDate = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                    IsCompleted = reader.GetBoolean(4),
                    CreatedAt = reader.GetDateTime(5)
                });
            }
            return list;
        }

        public static void CompleteTask(int id)
        {
            using var conn = new MySqlConnection(ConnectionString);
            conn.Open();
            var cmd = new MySqlCommand(
                "UPDATE tasks SET is_completed = 1 WHERE id = @id;", conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public static void DeleteTask(int id)
        {
            using var conn = new MySqlConnection(ConnectionString);
            conn.Open();
            var cmd = new MySqlCommand("DELETE FROM tasks WHERE id = @id;", conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }
}