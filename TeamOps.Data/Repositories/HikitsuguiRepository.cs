using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
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

        // ---------------------------------------------------------
        // INSERT
        // ---------------------------------------------------------
        public int Add(Hikitsugui h)
        {
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
        INSERT INTO Hikitsugui
        (Date, ShiftId, CreatorCodigoFJ, CategoryId, EquipmentId, LocalId, SectorId, ForLeaders, ForOperators, Description, AttachmentPath, ForMaSv)
        VALUES
        (@Date, @ShiftId, @CreatorCodigoFJ, @CategoryId, @EquipmentId, @LocalId, @SectorId, @ForLeaders, @ForOperators, @Description, @AttachmentPath, @ForMaSv);

        SELECT last_insert_rowid();
        ";

            cmd.Parameters.AddWithValue("@Date", h.Date);
            cmd.Parameters.AddWithValue("@ShiftId", h.ShiftId);
            cmd.Parameters.AddWithValue("@CreatorCodigoFJ", h.CreatorCodigoFJ);
            cmd.Parameters.AddWithValue("@CategoryId", h.CategoryId);
            cmd.Parameters.AddWithValue("@EquipmentId", (object?)h.EquipmentId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@LocalId", (object?)h.LocalId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SectorId", (object?)h.SectorId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ForLeaders", h.ForLeaders ? 1 : 0);
            cmd.Parameters.AddWithValue("@ForOperators", h.ForOperators ? 1 : 0);
            cmd.Parameters.AddWithValue("@Description", h.Description ?? "");
            cmd.Parameters.AddWithValue("@AttachmentPath", (object?)h.AttachmentPath ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ForMaSv", h.ForMaSv ? 1 : 0);

            object? result = cmd.ExecuteScalar();

            return Convert.ToInt32(result);
        }


        // ---------------------------------------------------------
        // UPDATE AttachmentPath
        // ---------------------------------------------------------
        public void UpdateAttachmentPath(int id, string path)
        {
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                UPDATE Hikitsugui
                SET AttachmentPath = @path
                WHERE Id = @id";

            cmd.Parameters.AddWithValue("@path", path);
            cmd.Parameters.AddWithValue("@id", id);

            cmd.ExecuteNonQuery();
        }

        // ---------------------------------------------------------
        // GET BY ID
        // ---------------------------------------------------------
        public Hikitsugui? GetById(int id)
        {
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT Id, Date, ShiftId, CreatorCodigoFJ, CategoryId,
                       EquipmentId, LocalId, SectorId, ForLeaders, ForOperators,
                       Description, AttachmentPath
                FROM Hikitsugui
                WHERE Id = @id";

            cmd.Parameters.AddWithValue("@id", id);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                return null;

            return new Hikitsugui
            {
                Id = reader.GetInt32(0),
                Date = reader.GetDateTime(1),
                ShiftId = reader.GetInt32(2),
                CreatorCodigoFJ = reader.GetString(3),
                CategoryId = reader.GetInt32(4),
                EquipmentId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                LocalId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                SectorId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                ForLeaders = reader.GetInt32(8) == 1,
                ForOperators = reader.GetInt32(9) == 1,
                Description = reader.GetString(10),
                AttachmentPath = reader.IsDBNull(11) ? null : reader.GetString(11)
            };
        }

        // ---------------------------------------------------------
        // GET ALL
        // ---------------------------------------------------------
        public List<Hikitsugui> GetAll()
        {
            var list = new List<Hikitsugui>();

            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT Id, Date, ShiftId, CreatorCodigoFJ, CategoryId,
                       EquipmentId, LocalId, ForLeaders, ForOperators,
                       Description, AttachmentPath
                FROM Hikitsugui
                ORDER BY Date DESC";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new Hikitsugui
                {
                    Id = reader.GetInt32(0),
                    Date = reader.GetDateTime(1),
                    ShiftId = reader.GetInt32(2),
                    CreatorCodigoFJ = reader.GetString(3),
                    CategoryId = reader.GetInt32(4),
                    EquipmentId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                    LocalId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                    ForLeaders = reader.GetInt32(7) == 1,
                    ForOperators = reader.GetInt32(8) == 1,
                    Description = reader.GetString(9),
                    AttachmentPath = reader.IsDBNull(10) ? null : reader.GetString(10)
                });
            }

            return list;
        }

        public List<Hikitsugui> GetAllWithSector()
        {
            var list = new List<Hikitsugui>();

            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT Id, Date, ShiftId, CreatorCodigoFJ, CategoryId,
                       EquipmentId, LocalId, SectorId, ForLeaders, ForOperators,
                       Description, AttachmentPath,
                       ForMaSv
                FROM Hikitsugui
                ORDER BY Date DESC";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new Hikitsugui
                {
                    Id = reader.GetInt32(0),
                    Date = reader.GetDateTime(1),
                    ShiftId = reader.GetInt32(2),
                    CreatorCodigoFJ = reader.GetString(3),
                    CategoryId = reader.GetInt32(4),
                    EquipmentId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                    LocalId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                    SectorId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                    ForLeaders = reader.GetInt32(8) == 1,
                    ForOperators = reader.GetInt32(9) == 1,
                    Description = reader.GetString(10),
                    AttachmentPath = reader.IsDBNull(11) ? null : reader.GetString(11),
                    ForMaSv = reader.GetInt32(12) == 1
                });
            }

            return list;
        }

        public List<HikitsuguiListItem> GetForOperator(
            DateTime start,
            DateTime end,
            int userId,
            int selectedLocalId)
        {
            var list = new List<HikitsuguiListItem>();
            var visibleIds = new HikitsuguiAudienceService(_factory)
                .GetVisibleMessageIds(userId, start, end);

            if (visibleIds.Count == 0)
                return list;

            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
        SELECT 
            h.Id,
            h.Date,
            c.NamePt,
            o.NameRomanji AS CreatorCodigoFJ,
            h.Description
        FROM Hikitsugui h
        INNER JOIN Categories c ON c.Id = h.CategoryId
        LEFT JOIN Operators o ON o.CodigoFJ = h.CreatorCodigoFJ
        WHERE h.Date >= @Start AND h.Date < @End
          AND h.ForOperators = 1
          AND (h.LocalId = @LocalId OR h.LocalId IS NULL)
        ORDER BY h.Date DESC";

            cmd.Parameters.AddWithValue("@LocalId", selectedLocalId);
            cmd.Parameters.AddWithValue("@Start", start);
            cmd.Parameters.AddWithValue("@End", end);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetInt32(0);
                if (!visibleIds.Contains(id))
                    continue;

                list.Add(new HikitsuguiListItem
                {
                    Id = id,
                    Date = reader.GetDateTime(1),
                    CategoryName = reader.GetString(2),
                    CreatorCodigoFJ = reader.GetString(3),
                    Description = reader.GetString(4)
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

            cmd.CommandText = "DELETE FROM Hikitsugui WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);

            cmd.ExecuteNonQuery();
        }
    }
}
