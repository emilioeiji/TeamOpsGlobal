using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using TeamOps.Core.Entities;
using TeamOps.Data.Db;

namespace TeamOps.Data.Repositories
{
    public sealed class OperatorRepository
    {
        private readonly SqliteConnectionFactory _factory;

        public OperatorRepository(SqliteConnectionFactory factory)
        {
            _factory = factory;
        }

        public void Add(Operator op)
        {
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Operators 
                (CodigoFJ, NameRomanji, NameNihongo, ShiftId, GroupId, SectorId, 
                 StartDate, EndDate, Trainer, Status, IsLeader, Telefone, Endereco, Nascimento, OperatorType)
                VALUES 
                (@c, @r, @n, @s, @g, @sec, @start, @end, @t, @st, @leader, @tel, @endereco, @nascimento, @operatorType)";

            cmd.Parameters.AddWithValue("@c", op.CodigoFJ);
            cmd.Parameters.AddWithValue("@r", op.NameRomanji);
            cmd.Parameters.AddWithValue("@n", op.NameNihongo);
            cmd.Parameters.AddWithValue("@s", op.ShiftId);
            cmd.Parameters.AddWithValue("@g", op.GroupId);
            cmd.Parameters.AddWithValue("@sec", op.SectorId);
            cmd.Parameters.AddWithValue("@start", op.StartDate);
            cmd.Parameters.AddWithValue("@end", (object?)op.EndDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@t", op.Trainer ? 1 : 0);
            cmd.Parameters.AddWithValue("@st", op.Status ? 1 : 0);
            cmd.Parameters.AddWithValue("@leader", op.IsLeader ? 1 : 0);
            cmd.Parameters.AddWithValue("@tel", (object?)op.Telefone ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@endereco", (object?)op.Endereco ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@nascimento", (object?)op.Nascimento ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@operatorType", op.OperatorType);

            cmd.ExecuteNonQuery();
        }

        public Operator? GetByCodigoFJ(string codigoFJ)
        {
            if (string.IsNullOrWhiteSpace(codigoFJ))
                return null;

            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT 
                    o.CodigoFJ,
                    o.NameRomanji,
                    o.NameNihongo,
                    o.ShiftId,
                    o.GroupId,
                    o.SectorId,
                    o.StartDate,
                    o.EndDate,
                    o.Trainer,
                    o.Status,
                    o.CreatedAt,
                    o.IsLeader,
                    o.Telefone,
                    o.Endereco,
                    s.NameJp AS ShiftName,
                    g.NameJp AS GroupName,
                    sc.NameJp AS SectorName,
                    o.Nascimento,
                    o.OperatorType
                FROM Operators o
                LEFT JOIN Shifts s   ON s.Id = o.ShiftId
                LEFT JOIN Groups g   ON g.Id = o.GroupId
                LEFT JOIN Sectors sc ON sc.Id = o.SectorId
                WHERE o.CodigoFJ = @c";

            cmd.Parameters.AddWithValue("@c", codigoFJ);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;

            return Map(reader);
        }

        public List<Operator> GetAll()
        {
            var list = new List<Operator>();
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT 
                    o.CodigoFJ,
                    o.NameRomanji,
                    o.NameNihongo,
                    o.ShiftId,
                    o.GroupId,
                    o.SectorId,
                    o.StartDate,
                    o.EndDate,
                    o.Trainer,
                    o.Status,
                    o.CreatedAt,
                    o.IsLeader,
                    o.Telefone,
                    o.Endereco,
                    s.NameJp AS ShiftName,
                    g.NameJp AS GroupName,
                    sc.NameJp AS SectorName,
                    o.Nascimento,
                    o.OperatorType
                FROM Operators o
                LEFT JOIN Shifts s   ON s.Id = o.ShiftId
                LEFT JOIN Groups g   ON g.Id = o.GroupId
                LEFT JOIN Sectors sc ON sc.Id = o.SectorId
                WHERE o.Status = 1
                ORDER BY o.NameRomanji";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));

            return list;
        }

        public void Update(Operator op)
        {
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE Operators
                SET NameRomanji=@r, NameNihongo=@n, ShiftId=@s, GroupId=@g, SectorId=@sec,
                    StartDate=@start, EndDate=@end, Trainer=@t, Status=@st, IsLeader=@leader,
                    Telefone=@tel, Endereco=@endereco, Nascimento=@nascimento, OperatorType=@operatorType
                WHERE CodigoFJ=@c";

            cmd.Parameters.AddWithValue("@c", op.CodigoFJ);
            cmd.Parameters.AddWithValue("@r", op.NameRomanji);
            cmd.Parameters.AddWithValue("@n", op.NameNihongo);
            cmd.Parameters.AddWithValue("@s", op.ShiftId);
            cmd.Parameters.AddWithValue("@g", op.GroupId);
            cmd.Parameters.AddWithValue("@sec", op.SectorId);
            cmd.Parameters.AddWithValue("@start", op.StartDate);
            cmd.Parameters.AddWithValue("@end", (object?)op.EndDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@t", op.Trainer ? 1 : 0);
            cmd.Parameters.AddWithValue("@st", op.Status ? 1 : 0);
            cmd.Parameters.AddWithValue("@leader", op.IsLeader ? 1 : 0);
            cmd.Parameters.AddWithValue("@tel", (object?)op.Telefone ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@endereco", (object?)op.Endereco ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@nascimento", (object?)op.Nascimento ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@operatorType", op.OperatorType);

            cmd.ExecuteNonQuery();
        }

        public void Delete(string codigoFJ)
        {
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Operators WHERE CodigoFJ=@c";
            cmd.Parameters.AddWithValue("@c", codigoFJ);
            cmd.ExecuteNonQuery();
        }

        public void SetLeader(string codigoFJ, bool isLeader)
        {
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Operators SET IsLeader=@leader WHERE CodigoFJ=@c";
            cmd.Parameters.AddWithValue("@leader", isLeader ? 1 : 0);
            cmd.Parameters.AddWithValue("@c", codigoFJ);
            cmd.ExecuteNonQuery();
        }

        public List<Operator> GetLeaders()
        {
            var list = new List<Operator>();
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT 
                    o.CodigoFJ,
                    o.NameRomanji,
                    o.NameNihongo,
                    o.ShiftId,
                    o.GroupId,
                    o.SectorId,
                    o.StartDate,
                    o.EndDate,
                    o.Trainer,
                    o.Status,
                    o.CreatedAt,
                    o.IsLeader,
                    o.Telefone,
                    o.Endereco,
                    s.NameJp AS ShiftName,
                    g.NameJp AS GroupName,
                    sc.NameJp AS SectorName,
                    o.Nascimento,
                    o.OperatorType
                FROM Operators o
                LEFT JOIN Shifts s   ON s.Id = o.ShiftId
                LEFT JOIN Groups g   ON g.Id = o.GroupId
                LEFT JOIN Sectors sc ON sc.Id = o.SectorId
                WHERE o.IsLeader = 1
                ORDER BY o.NameRomanji";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));

            return list;
        }

        private static Operator Map(SqliteDataReader reader)
        {
            return new Operator
            {
                CodigoFJ = reader.GetString(0),
                NameRomanji = reader.GetString(1),
                NameNihongo = reader.GetString(2),
                ShiftId = reader.GetInt32(3),
                GroupId = reader.GetInt32(4),
                SectorId = reader.GetInt32(5),
                StartDate = reader.GetDateTime(6),
                EndDate = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                Trainer = reader.GetBoolean(8),
                Status = reader.GetBoolean(9),
                CreatedAt = reader.GetDateTime(10),
                IsLeader = reader.GetInt32(11) == 1,
                Telefone = reader.IsDBNull(12) ? null : reader.GetString(12),
                Endereco = reader.IsDBNull(13) ? null : reader.GetString(13),

                // Novas colunas (no final, sem quebrar nada)
                ShiftName = reader.IsDBNull(14) ? null : reader.GetString(14),
                GroupName = reader.IsDBNull(15) ? null : reader.GetString(15),
                SectorName = reader.IsDBNull(16) ? null : reader.GetString(16),

                Nascimento = reader.IsDBNull(17) ? null : reader.GetDateTime(17),
                OperatorType = reader.IsDBNull(18) ? "Regular" : reader.GetString(18)
            };
        }

        public List<Operator> GetByShift(int shiftId)
        {
            var list = new List<Operator>();
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT 
                    o.CodigoFJ,
                    o.NameRomanji,
                    o.NameNihongo,
                    o.ShiftId,
                    o.GroupId,
                    o.SectorId,
                    o.StartDate,
                    o.EndDate,
                    o.Trainer,
                    o.Status,
                    o.CreatedAt,
                    o.IsLeader,
                    o.Telefone,
                    o.Endereco,
                    s.NameJp AS ShiftName,
                    g.NameJp AS GroupName,
                    sc.NameJp AS SectorName,
                    o.Nascimento,
                    o.OperatorType
                FROM Operators o
                LEFT JOIN Shifts s   ON s.Id = o.ShiftId
                LEFT JOIN Groups g   ON g.Id = o.GroupId
                LEFT JOIN Sectors sc ON sc.Id = o.SectorId
                WHERE o.Status = 1 AND o.ShiftId = @shift
                ORDER BY o.NameRomanji";

            cmd.Parameters.AddWithValue("@shift", shiftId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));

            return list;
        }
    }
}
