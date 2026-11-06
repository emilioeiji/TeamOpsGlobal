using System.Collections.Generic;
using System;
using TeamOps.Core.Entities;
using TeamOps.Data.Db;

namespace TeamOps.Data.Repositories
{
    public sealed class OperatorPresenceRepository
    {
        private readonly SqliteConnectionFactory _factory;

        public OperatorPresenceRepository(SqliteConnectionFactory factory)
        {
            _factory = factory;
        }

        public void RegisterPresence(string codigoFJ, int sectorId, int localId, int shiftId, DateTime date)
        {
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                INSERT INTO OperatorPresence 
                (CodigoFJ, SectorId, LocalId, ShiftId, Date)
                VALUES (@fj, @sector, @local, @shift, @date);
            ";

            cmd.Parameters.AddWithValue("@fj", codigoFJ);
            cmd.Parameters.AddWithValue("@sector", sectorId);
            cmd.Parameters.AddWithValue("@local", localId);
            cmd.Parameters.AddWithValue("@shift", shiftId);
            cmd.Parameters.AddWithValue("@date", date);

            cmd.ExecuteNonQuery();
        }

        public List<OperatorPresence> GetLatestByDateSectorShift(DateTime date, int sectorId, int shiftId)
        {
            var list = new List<OperatorPresence>();

            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT
                    p.CodigoFJ,
                    p.SectorId,
                    p.LocalId,
                    p.ShiftId,
                    p.Date AS LastPresence,
                    o.NameRomanji,
                    o.NameNihongo
                FROM (
                    SELECT
                        p.Id,
                        p.CodigoFJ,
                        p.SectorId,
                        p.LocalId,
                        p.ShiftId,
                        p.Date,
                        ROW_NUMBER() OVER (
                            PARTITION BY p.CodigoFJ
                            ORDER BY datetime(p.Date) DESC, p.Id DESC
                        ) AS RowNumber
                    FROM OperatorPresence p
                    WHERE date(p.Date) = date(@date)
                      AND p.SectorId = @sector
                      AND p.ShiftId = @shift
                ) p
                JOIN Operators o ON o.CodigoFJ = p.CodigoFJ
                WHERE p.RowNumber = 1
                ORDER BY o.NameRomanji, p.CodigoFJ;
            ";

            cmd.Parameters.AddWithValue("@date", date);
            cmd.Parameters.AddWithValue("@sector", sectorId);
            cmd.Parameters.AddWithValue("@shift", shiftId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new OperatorPresence
                {
                    CodigoFJ = reader.GetString(0),
                    SectorId = reader.GetInt32(1),
                    LocalId = reader.GetInt32(2),
                    ShiftId = reader.GetInt32(3),
                    Date = reader.GetDateTime(4),
                    NameRomanji = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    NameNihongo = reader.IsDBNull(6) ? "" : reader.GetString(6)
                });
            }

            return list;
        }

        public List<OperatorPresence> GetLatestByDateShift(DateTime date, int shiftId)
        {
            var list = new List<OperatorPresence>();

            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT
                    p.CodigoFJ,
                    p.SectorId,
                    p.LocalId,
                    p.ShiftId,
                    p.Date AS LastPresence,
                    o.NameRomanji,
                    o.NameNihongo
                FROM (
                    SELECT
                        p.Id,
                        p.CodigoFJ,
                        p.SectorId,
                        p.LocalId,
                        p.ShiftId,
                        p.Date,
                        ROW_NUMBER() OVER (
                            PARTITION BY p.CodigoFJ
                            ORDER BY datetime(p.Date) DESC, p.Id DESC
                        ) AS RowNumber
                    FROM OperatorPresence p
                    WHERE date(p.Date) = date(@date)
                      AND p.ShiftId = @shift
                ) p
                JOIN Operators o ON o.CodigoFJ = p.CodigoFJ
                WHERE p.RowNumber = 1
                ORDER BY p.SectorId, o.NameRomanji, p.CodigoFJ;
            ";

            cmd.Parameters.AddWithValue("@date", date);
            cmd.Parameters.AddWithValue("@shift", shiftId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new OperatorPresence
                {
                    CodigoFJ = reader.GetString(0),
                    SectorId = reader.GetInt32(1),
                    LocalId = reader.GetInt32(2),
                    ShiftId = reader.GetInt32(3),
                    Date = reader.GetDateTime(4),
                    NameRomanji = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    NameNihongo = reader.IsDBNull(6) ? "" : reader.GetString(6)
                });
            }

            return list;
        }

        public List<OperatorPresence> GetByDateSectorShift(DateTime date, int sectorId, int shiftId)
        {
            var list = new List<OperatorPresence>();

            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT 
                    p.Id,
                    p.CodigoFJ,
                    p.SectorId,
                    p.LocalId,
                    p.ShiftId,
                    p.Date,
                    o.NameRomanji,
                    o.NameNihongo
                FROM OperatorPresence p
                JOIN Operators o ON o.CodigoFJ = p.CodigoFJ
                WHERE DATE(p.Date) = DATE(@date)
                  AND p.SectorId = @sector
                  AND p.ShiftId = @shift
                ORDER BY p.Date DESC;
            ";

            cmd.Parameters.AddWithValue("@date", date);
            cmd.Parameters.AddWithValue("@sector", sectorId);
            cmd.Parameters.AddWithValue("@shift", shiftId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new OperatorPresence
                {
                    Id = reader.GetInt32(0),
                    CodigoFJ = reader.GetString(1),
                    SectorId = reader.GetInt32(2),
                    LocalId = reader.GetInt32(3),
                    ShiftId = reader.GetInt32(4),
                    Date = reader.GetDateTime(5),
                    NameRomanji = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    NameNihongo = reader.IsDBNull(7) ? "" : reader.GetString(7)
                });
            }

            return list;
        }

    }
}
