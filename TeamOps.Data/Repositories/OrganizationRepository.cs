using Microsoft.Data.Sqlite;
using TeamOps.Core.Common;
using TeamOps.Core.Entities;
using TeamOps.Data.Db;

namespace TeamOps.Data.Repositories
{
    public sealed class OrganizationRepository
    {
        private readonly SqliteConnectionFactory _factory;

        public OrganizationRepository(SqliteConnectionFactory factory)
        {
            _factory = factory;
        }

        public OrganizationNode? GetByCode(string code)
        {
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, ParentId, NodeType, Code, NamePt, NameJp, IsActive, CreatedAt, UpdatedAt
                FROM OrganizationNodes
                WHERE Code = @code COLLATE NOCASE;";
            cmd.Parameters.AddWithValue("@code", NormalizeCode(code));
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public IReadOnlyList<OrganizationNode> GetTree(bool includeInactive = false)
        {
            var result = new List<OrganizationNode>();
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                WITH RECURSIVE tree AS (
                    SELECT *, printf('%010d', Id) AS SortPath
                    FROM OrganizationNodes
                    WHERE ParentId IS NULL
                    UNION ALL
                    SELECT child.*, tree.SortPath || '/' || printf('%010d', child.Id)
                    FROM OrganizationNodes child
                    JOIN tree ON child.ParentId = tree.Id
                )
                SELECT Id, ParentId, NodeType, Code, NamePt, NameJp, IsActive, CreatedAt, UpdatedAt
                FROM tree
                WHERE @includeInactive = 1 OR IsActive = 1
                ORDER BY SortPath;";
            cmd.Parameters.AddWithValue("@includeInactive", includeInactive ? 1 : 0);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                result.Add(Map(reader));
            return result;
        }

        public IReadOnlyList<OrganizationNode> GetDescendants(int nodeId, bool includeSelf = true)
        {
            var result = new List<OrganizationNode>();
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                WITH RECURSIVE descendants AS (
                    SELECT * FROM OrganizationNodes WHERE Id = @nodeId
                    UNION ALL
                    SELECT child.*
                    FROM OrganizationNodes child
                    JOIN descendants parent ON child.ParentId = parent.Id
                )
                SELECT Id, ParentId, NodeType, Code, NamePt, NameJp, IsActive, CreatedAt, UpdatedAt
                FROM descendants
                WHERE (@includeSelf = 1 OR Id <> @nodeId) AND IsActive = 1
                ORDER BY Id;";
            cmd.Parameters.AddWithValue("@nodeId", nodeId);
            cmd.Parameters.AddWithValue("@includeSelf", includeSelf ? 1 : 0);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                result.Add(Map(reader));
            return result;
        }

        public int AddUnit(string code, string namePt, string nameJp, int? parentId)
        {
            ValidateRequired(code, namePt);
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO OrganizationNodes (ParentId, NodeType, Code, NamePt, NameJp)
                VALUES (@parentId, 'Unit', @code, @namePt, @nameJp);
                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@parentId", parentId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@code", NormalizeCode(code));
            cmd.Parameters.AddWithValue("@namePt", namePt.Trim());
            cmd.Parameters.AddWithValue("@nameJp", (nameJp ?? string.Empty).Trim());
            return checked((int)(long)cmd.ExecuteScalar()!);
        }

        public Sector AddSector(string code, string namePt, string nameJp, int parentUnitId)
        {
            ValidateRequired(code, namePt);
            using var conn = _factory.CreateOpenConnection();
            using var transaction = conn.BeginTransaction();

            using var nodeCommand = conn.CreateCommand();
            nodeCommand.Transaction = transaction;
            nodeCommand.CommandText = @"
                INSERT INTO OrganizationNodes (ParentId, NodeType, Code, NamePt, NameJp)
                VALUES (@parentId, 'Sector', @code, @namePt, @nameJp);
                SELECT last_insert_rowid();";
            nodeCommand.Parameters.AddWithValue("@parentId", parentUnitId);
            nodeCommand.Parameters.AddWithValue("@code", NormalizeCode(code));
            nodeCommand.Parameters.AddWithValue("@namePt", namePt.Trim());
            nodeCommand.Parameters.AddWithValue("@nameJp", (nameJp ?? string.Empty).Trim());
            var nodeId = checked((int)(long)nodeCommand.ExecuteScalar()!);

            using var sectorCommand = conn.CreateCommand();
            sectorCommand.Transaction = transaction;
            sectorCommand.CommandText = @"
                INSERT INTO Sectors (Code, NamePt, NameJp, OrganizationNodeId, IsActive)
                VALUES (@code, @namePt, @nameJp, @nodeId, 1);
                SELECT last_insert_rowid();";
            sectorCommand.Parameters.AddWithValue("@code", NormalizeCode(code));
            sectorCommand.Parameters.AddWithValue("@namePt", namePt.Trim());
            sectorCommand.Parameters.AddWithValue("@nameJp", (nameJp ?? string.Empty).Trim());
            sectorCommand.Parameters.AddWithValue("@nodeId", nodeId);
            var sectorId = checked((int)(long)sectorCommand.ExecuteScalar()!);

            transaction.Commit();
            return new Sector
            {
                Id = sectorId,
                Code = NormalizeCode(code),
                NamePt = namePt.Trim(),
                NameJp = (nameJp ?? string.Empty).Trim(),
                OrganizationNodeId = nodeId,
                IsActive = true
            };
        }

        public void MoveUnit(int nodeId, int? newParentId)
        {
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE OrganizationNodes
                SET ParentId = @parentId, UpdatedAt = CURRENT_TIMESTAMP
                WHERE Id = @id AND NodeType = 'Unit';";
            cmd.Parameters.AddWithValue("@parentId", newParentId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@id", nodeId);
            if (cmd.ExecuteNonQuery() != 1)
                throw new InvalidOperationException("Unidade organizacional nao encontrada.");
        }

        public void SetActive(int nodeId, bool isActive)
        {
            using var conn = _factory.CreateOpenConnection();
            using var transaction = conn.BeginTransaction();
            using var cmd = conn.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"
                UPDATE OrganizationNodes
                SET IsActive = @isActive, UpdatedAt = CURRENT_TIMESTAMP
                WHERE Id = @id;";
            cmd.Parameters.AddWithValue("@isActive", isActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@id", nodeId);
            if (cmd.ExecuteNonQuery() != 1)
                throw new InvalidOperationException("No organizacional nao encontrado.");

            using var sector = conn.CreateCommand();
            sector.Transaction = transaction;
            sector.CommandText = @"
                UPDATE Sectors SET IsActive = @isActive
                WHERE OrganizationNodeId = @id;";
            sector.Parameters.AddWithValue("@isActive", isActive ? 1 : 0);
            sector.Parameters.AddWithValue("@id", nodeId);
            sector.ExecuteNonQuery();
            transaction.Commit();
        }

        private static OrganizationNode Map(SqliteDataReader reader) => new()
        {
            Id = reader.GetInt32(0),
            ParentId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
            NodeType = Enum.Parse<OrganizationNodeType>(reader.GetString(2), true),
            Code = reader.GetString(3),
            NamePt = reader.GetString(4),
            NameJp = reader.GetString(5),
            IsActive = reader.GetInt32(6) == 1,
            CreatedAt = reader.GetDateTime(7),
            UpdatedAt = reader.GetDateTime(8)
        };

        private static string NormalizeCode(string code) => (code ?? string.Empty).Trim().ToUpperInvariant();

        private static void ValidateRequired(string code, string namePt)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("O codigo organizacional deve ser informado.", nameof(code));
            if (string.IsNullOrWhiteSpace(namePt))
                throw new ArgumentException("O nome deve ser informado.", nameof(namePt));
        }
    }
}
