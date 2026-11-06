using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using TeamOps.Core.Entities;
using TeamOps.Data.Db;

namespace TeamOps.Data.Repositories
{
    public sealed class HikitsuguiReadRepository
    {
        private readonly SqliteConnectionFactory _factory;

        public HikitsuguiReadRepository(SqliteConnectionFactory factory)
        {
            _factory = factory;
        }

        // ---------------------------------------------------------
        // INSERT (mark as read)
        // ---------------------------------------------------------
        public int Add(HikitsuguiRead r)
        {
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                INSERT INTO HikitsuguiReads
                (HikitsuguiId, ReaderCodigoFJ, ReadAt)
                VALUES
                (@hikitsuguiId, @reader, @readAt);
                SELECT last_insert_rowid();";

            cmd.Parameters.AddWithValue("@hikitsuguiId", r.HikitsuguiId);
            cmd.Parameters.AddWithValue("@reader", r.ReaderCodigoFJ);
            cmd.Parameters.AddWithValue("@readAt", r.ReadAt);

            return (int)(long)cmd.ExecuteScalar()!;
        }

        // ---------------------------------------------------------
        // CHECK IF A USER HAS READ A HIKITSUGUI
        // ---------------------------------------------------------
        public bool HasRead(int hikitsuguiId, string codigoFJ)
        {
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT 1 
                FROM HikitsuguiReads
                WHERE HikitsuguiId = @h AND ReaderCodigoFJ = @c
                LIMIT 1";

            cmd.Parameters.AddWithValue("@h", hikitsuguiId);
            cmd.Parameters.AddWithValue("@c", codigoFJ);

            return cmd.ExecuteScalar() != null;
        }

        // ---------------------------------------------------------
        // MARK AS READ (simple version)
        // ---------------------------------------------------------
        public void MarkAsRead(int hikitsuguiId, string codigoFJ)
        {
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                INSERT INTO HikitsuguiReads
                (HikitsuguiId, ReaderCodigoFJ, ReadAt)
                SELECT @h, @c, CURRENT_TIMESTAMP
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM HikitsuguiReads
                    WHERE HikitsuguiId = @h
                      AND ReaderCodigoFJ = @c
                );";

            cmd.Parameters.AddWithValue("@h", hikitsuguiId);
            cmd.Parameters.AddWithValue("@c", codigoFJ);

            cmd.ExecuteNonQuery();
        }

        // ---------------------------------------------------------
        // GET READERS FOR A HIKITSUGUI
        // ---------------------------------------------------------
        public List<HikitsuguiRead> GetByHikitsugui(int hikitsuguiId)
        {
            var list = new List<HikitsuguiRead>();

            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT Id, HikitsuguiId, ReaderCodigoFJ, ReadAt
                FROM HikitsuguiReads
                WHERE HikitsuguiId = @id
                ORDER BY ReadAt ASC";

            cmd.Parameters.AddWithValue("@id", hikitsuguiId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new HikitsuguiRead
                {
                    Id = reader.GetInt32(0),
                    HikitsuguiId = reader.GetInt32(1),
                    ReaderCodigoFJ = reader.GetString(2),
                    ReadAt = reader.GetDateTime(3)
                });
            }

            return list;
        }

        public List<HikitsuguiRead> GetByPeriod(DateTime start, DateTime end)
        {
            var list = new List<HikitsuguiRead>();

            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
        SELECT HikitsuguiId, ReaderCodigoFJ
        FROM HikitsuguiReads
        WHERE ReadAt >= @start AND ReadAt < @end";

            cmd.Parameters.AddWithValue("@start", start);
            cmd.Parameters.AddWithValue("@end", end);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new HikitsuguiRead
                {
                    HikitsuguiId = reader.GetInt32(0),
                    ReaderCodigoFJ = reader.GetString(1)
                });
            }

            return list;
        }

        // ---------------------------------------------------------
        // DELETE
        // ---------------------------------------------------------
        public void Delete(int id)
        {
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "DELETE FROM HikitsuguiReads WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);

            cmd.ExecuteNonQuery();
        }
    }
}
