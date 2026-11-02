using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using TeamOps.Core.Entities;
using TeamOps.Data.Db;

namespace TeamOps.Data.Repositories
{
    public sealed class SectorRepository
    {
        private readonly SqliteConnectionFactory _factory;

        public SectorRepository(SqliteConnectionFactory factory)
        {
            _factory = factory;
        }

        public int Add(Sector s)
        {
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO Sectors (Code, NamePt, NameJp, OrganizationNodeId, IsActive) VALUES (@code, @pt, @jp, @nodeId, @active); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@code", string.IsNullOrWhiteSpace(s.Code) ? (object)DBNull.Value : s.Code.Trim().ToUpperInvariant());
            cmd.Parameters.AddWithValue("@pt", s.NamePt);
            cmd.Parameters.AddWithValue("@jp", s.NameJp);
            cmd.Parameters.AddWithValue("@nodeId", s.OrganizationNodeId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@active", s.IsActive ? 1 : 0);
            return (int)(long)cmd.ExecuteScalar()!;
        }

        public Sector? GetById(int id)
        {
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, COALESCE(Code, ''), NamePt, NameJp, OrganizationNodeId, IsActive FROM Sectors WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;

            return new Sector
            {
                Id = reader.GetInt32(0),
                Code = reader.GetString(1),
                NamePt = reader.GetString(2),
                NameJp = reader.GetString(3),
                OrganizationNodeId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                IsActive = reader.GetInt32(5) == 1
            };
        }

        public List<Sector> GetAll()
        {
            var list = new List<Sector>();
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, COALESCE(Code, ''), NamePt, NameJp, OrganizationNodeId, IsActive FROM Sectors WHERE IsActive = 1 ORDER BY NamePt, Id";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new Sector
                {
                    Id = reader.GetInt32(0),
                    Code = reader.GetString(1),
                    NamePt = reader.GetString(2),
                    NameJp = reader.GetString(3),
                    OrganizationNodeId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    IsActive = reader.GetInt32(5) == 1
                });
            }
            return list;
        }

        public void Update(Sector s)
        {
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Sectors SET Code=@code, NamePt=@pt, NameJp=@jp, IsActive=@active WHERE Id=@id";
            cmd.Parameters.AddWithValue("@code", string.IsNullOrWhiteSpace(s.Code) ? (object)DBNull.Value : s.Code.Trim().ToUpperInvariant());
            cmd.Parameters.AddWithValue("@pt", s.NamePt);
            cmd.Parameters.AddWithValue("@jp", s.NameJp);
            cmd.Parameters.AddWithValue("@active", s.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@id", s.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Sectors WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }
}
