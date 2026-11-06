using TeamOps.Core.Entities;
using TeamOps.Data.Db;
using TeamOps.Data.Security;

namespace TeamOps.Data.Repositories
{
    public sealed class HikitsuguiRepository
    {
        private readonly SqliteConnectionFactory _factory;

        public HikitsuguiRepository(SqliteConnectionFactory factory)
        {
            _factory = factory;
        }

        public int Add(Hikitsugui value)
        {
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Hikitsugui
                    (Date, ShiftId, CreatorCodigoFJ, CategoryId, EquipmentId,
                     LocalId, SectorId, Description, AttachmentPath)
                VALUES
                    (@date, @shiftId, @creatorCodigoFJ, @categoryId, @equipmentId,
                     @localId, @sectorId, @description, @attachmentPath);
                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@date", value.Date);
            cmd.Parameters.AddWithValue("@shiftId", value.ShiftId);
            cmd.Parameters.AddWithValue("@creatorCodigoFJ", value.CreatorCodigoFJ);
            cmd.Parameters.AddWithValue("@categoryId", value.CategoryId);
            cmd.Parameters.AddWithValue("@equipmentId", (object?)value.EquipmentId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@localId", (object?)value.LocalId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@sectorId", (object?)value.SectorId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@description", value.Description ?? string.Empty);
            cmd.Parameters.AddWithValue("@attachmentPath", (object?)value.AttachmentPath ?? DBNull.Value);
            return checked((int)(long)cmd.ExecuteScalar()!);
        }

        public void UpdateAttachmentPath(int id, string path)
        {
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Hikitsugui SET AttachmentPath = @path WHERE Id = @id;";
            cmd.Parameters.AddWithValue("@path", path);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public Hikitsugui? GetById(int id)
        {
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, Date, ShiftId, CreatorCodigoFJ, CategoryId,
                       EquipmentId, LocalId, SectorId, Description, AttachmentPath
                FROM Hikitsugui
                WHERE Id = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public List<Hikitsugui> GetAll()
        {
            var result = new List<Hikitsugui>();
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, Date, ShiftId, CreatorCodigoFJ, CategoryId,
                       EquipmentId, LocalId, SectorId, Description, AttachmentPath
                FROM Hikitsugui
                ORDER BY Date DESC;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                result.Add(Map(reader));
            return result;
        }

        public List<Hikitsugui> GetAllWithSector() => GetAll();

        public List<HikitsuguiListItem> GetForOperator(
            DateTime start,
            DateTime end,
            int userId,
            int selectedLocalId)
        {
            var visibleIds = new HikitsuguiAudienceService(_factory)
                .GetVisibleMessageIds(userId, start, end);
            if (visibleIds.Count == 0)
                return [];

            var result = new List<HikitsuguiListItem>();
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT h.Id, h.Date, c.NamePt,
                       COALESCE(o.NameRomanji, h.CreatorCodigoFJ), h.Description
                FROM Hikitsugui h
                INNER JOIN Categories c ON c.Id = h.CategoryId
                LEFT JOIN Operators o ON o.CodigoFJ = h.CreatorCodigoFJ
                WHERE h.Date >= @start AND h.Date < @end
                  AND (h.LocalId = @localId OR h.LocalId IS NULL)
                ORDER BY h.Date DESC;";
            cmd.Parameters.AddWithValue("@localId", selectedLocalId);
            cmd.Parameters.AddWithValue("@start", start);
            cmd.Parameters.AddWithValue("@end", end);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var messageId = reader.GetInt32(0);
                if (!visibleIds.Contains(messageId))
                    continue;
                result.Add(new HikitsuguiListItem
                {
                    Id = messageId,
                    Date = reader.GetDateTime(1),
                    CategoryName = reader.GetString(2),
                    CreatorCodigoFJ = reader.GetString(3),
                    Description = reader.GetString(4)
                });
            }
            return result;
        }

        public void Delete(int id)
        {
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Hikitsugui WHERE Id = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Hikitsugui Map(Microsoft.Data.Sqlite.SqliteDataReader reader) => new()
        {
            Id = reader.GetInt32(0),
            Date = reader.GetDateTime(1),
            ShiftId = reader.GetInt32(2),
            CreatorCodigoFJ = reader.GetString(3),
            CategoryId = reader.GetInt32(4),
            EquipmentId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
            LocalId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
            SectorId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
            Description = reader.GetString(8),
            AttachmentPath = reader.IsDBNull(9) ? null : reader.GetString(9)
        };
    }
}
