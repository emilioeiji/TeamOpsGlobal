using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using TeamOps.Core.Entities;
using TeamOps.Data.Db;

namespace TeamOps.Data.Repositories
{
    public sealed class LocalRepository
    {
        private readonly SqliteConnectionFactory _factory;

        public LocalRepository(SqliteConnectionFactory factory)
        {
            _factory = factory;
        }

        public int Add(Local l)
        {
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Locals (NamePt, NameJp, SectorId) 
                VALUES (@pt, @jp, @sectorId);
                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@pt", l.NamePt);
            cmd.Parameters.AddWithValue("@jp", l.NameJp);
            cmd.Parameters.AddWithValue("@sectorId", l.SectorId);
            return (int)(long)cmd.ExecuteScalar()!;
        }

        public List<Local> GetAll()
        {
            var list = new List<Local>();
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, NamePt, NameJp, SectorId FROM Locals ORDER BY Id";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new Local
                {
                    Id = reader.GetInt32(0),
                    NamePt = reader.GetString(1),
                    NameJp = reader.GetString(2),
                    SectorId = reader.GetInt32(3)
                });
            }
            return list;
        }

        public List<Local> GetBySector(int sectorId)
        {
            var list = new List<Local>();

            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
            SELECT Id, NamePt, NameJp, SectorId 
            FROM Locals 
            WHERE SectorId = @sectorId
            ORDER BY NamePt";

            cmd.Parameters.AddWithValue("@sectorId", sectorId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new Local
                {
                    Id = reader.GetInt32(0),
                    NamePt = reader.GetString(1),
                    NameJp = reader.GetString(2),
                    SectorId = reader.GetInt32(3)
                });
            }

            return list;
        }

        public List<int> GetLocalIdsBySector(int sectorId)
        {
            var list = new List<int>();

            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "SELECT Id FROM Locals WHERE SectorId = @sectorId";
            cmd.Parameters.AddWithValue("@sectorId", sectorId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(reader.GetInt32(0));

            return list;
        }

        public Local? GetById(int id)
        {
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, NamePt, NameJp, SectorId FROM Locals WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new Local
                {
                    Id = reader.GetInt32(0),
                    NamePt = reader.GetString(1),
                    NameJp = reader.GetString(2),
                    SectorId = reader.GetInt32(3)
                };
            }
            return null;
        }

        public void Update(Local l)
        {
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE Locals 
                SET NamePt = @pt, NameJp = @jp, SectorId = @sectorId
                WHERE Id = @id";
            cmd.Parameters.AddWithValue("@pt", l.NamePt);
            cmd.Parameters.AddWithValue("@jp", l.NameJp);
            cmd.Parameters.AddWithValue("@sectorId", l.SectorId);
            cmd.Parameters.AddWithValue("@id", l.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Locals WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }
}
