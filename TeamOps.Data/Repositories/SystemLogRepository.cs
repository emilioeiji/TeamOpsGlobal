using System;
using TeamOps.Data.Db;
using TeamOps.Core.Entities;

namespace TeamOps.Data.Repositories
{
    public sealed class SystemLogRepository
    {
        private readonly SqliteConnectionFactory _factory;

        public SystemLogRepository(SqliteConnectionFactory factory)
        {
            _factory = factory;
        }

        public void Add(SystemLog log)
        {
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                INSERT INTO SystemLog
                (Timestamp, UserFJ, Module, Action, TargetId, Details)
                VALUES
                (@ts, @fj, @module, @action, @targetId, @details);
            ";

            cmd.Parameters.AddWithValue("@ts", log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("@fj", log.UserFJ);
            cmd.Parameters.AddWithValue("@module", log.Module);
            cmd.Parameters.AddWithValue("@action", log.Action);
            cmd.Parameters.AddWithValue("@targetId", (object?)log.TargetId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@details", (object?)log.Details ?? DBNull.Value);

            cmd.ExecuteNonQuery();
        }

        public void Log(string userFj, string module, string action, int? targetId = null, string? details = null)
        {
            Add(new SystemLog
            {
                Timestamp = DateTime.Now,
                UserFJ = userFj,
                Module = module,
                Action = action,
                TargetId = targetId,
                Details = details
            });
        }
    }
}
