using Dapper;
using TeamOps.Core.Entities;
using TeamOps.Data.Db;
using System.Text.RegularExpressions;

namespace TeamOps.Data.Repositories
{
    public sealed class ProductionMachineRepository
    {
        private static readonly Regex ValidMachineCodeRegex = new(@"^[A-Z]{1,3}\d{1,3}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly SqliteConnectionFactory _factory;

        public ProductionMachineRepository(SqliteConnectionFactory factory)
        {
            _factory = factory;
        }

        public Machine? GetByMachineCode(string machineCode)
        {
            using var conn = _factory.CreateOpenConnection();
            return GetByMachineCode(conn, machineCode);
        }

        public Machine? GetByMachineCode(System.Data.IDbConnection conn, string machineCode)
        {
            var normalizedMachineCode = NormalizeCode(machineCode);
            return conn.QueryFirstOrDefault<Machine>(
                @"
                    SELECT
                        Id,
                        NamePt,
                        NameJp,
                        MachineCode,
                        MachineKey,
                        LineCode,
                        LocalId,
                        SectorId,
                        COALESCE(IsActive, 1) AS IsActive
                    FROM Machines
                    WHERE MachineCode = @machineCode
                    LIMIT 1;",
                new
                {
                    machineCode = normalizedMachineCode
                }
            );
        }

        public Machine? GetByMachineKey(System.Data.IDbConnection conn, string machineCode, string lineCode)
        {
            var machineKey = BuildMachineKey(machineCode, lineCode);
            var normalizedMachineCode = NormalizeCode(machineCode);
            var normalizedLineCode = NormalizeCode(lineCode);

            return conn.QueryFirstOrDefault<Machine>(
                @"
                    SELECT
                        Id,
                        NamePt,
                        NameJp,
                        MachineCode,
                        MachineKey,
                        LineCode,
                        LocalId,
                        SectorId,
                        COALESCE(IsActive, 1) AS IsActive
                    FROM Machines
                    WHERE MachineKey = @machineKey
                       OR (
                            MachineCode = @machineCode
                            AND LineCode = @lineCode
                       )
                    ORDER BY CASE WHEN MachineKey = @machineKey THEN 0 ELSE 1 END, Id
                    LIMIT 1;",
                new
                {
                    machineKey,
                    machineCode = normalizedMachineCode,
                    lineCode = normalizedLineCode
                }
            );
        }

        public Machine EnsureMachine(string machineCode, string lineCode, int? sectorId = null, int? localId = null)
        {
            using var conn = _factory.CreateOpenConnection();
            return EnsureMachine(conn, machineCode, lineCode, sectorId, localId);
        }

        public Machine EnsureMachine(System.Data.IDbConnection conn, string machineCode, string lineCode, int? sectorId = null, int? localId = null)
        {
            var normalizedMachineCode = NormalizeCode(machineCode);
            var normalizedLineCode = NormalizeCode(lineCode);
            if (!IsValidProductionMachineCode(normalizedMachineCode))
            {
                throw new ArgumentException($"Codigo de maquina invalido para producao: '{machineCode}'.", nameof(machineCode));
            }

            var machineKey = BuildMachineKey(normalizedMachineCode, normalizedLineCode);

            var machine = GetByMachineKey(conn, normalizedMachineCode, normalizedLineCode);

            if (machine != null)
            {
                if (!string.Equals(machine.MachineCode, normalizedMachineCode, System.StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(machine.LineCode, normalizedLineCode, System.StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(machine.MachineKey, machineKey, System.StringComparison.OrdinalIgnoreCase))
                {
                    conn.Execute(
                        @"
                            UPDATE Machines
                            SET
                                MachineCode = @machineCode,
                                LineCode = @lineCode,
                                MachineKey = @machineKey
                            WHERE Id = @id;",
                        new
                        {
                            machineCode = normalizedMachineCode,
                            lineCode = normalizedLineCode,
                            machineKey,
                            id = machine.Id
                        }
                    );

                    machine.MachineCode = normalizedMachineCode;
                    machine.LineCode = normalizedLineCode;
                    machine.MachineKey = machineKey;
                }

                LogLocationMismatchIfNeeded(machine, machineKey, sectorId, localId);

                return machine;
            }

            var newId = conn.ExecuteScalar<int>(
                @"
                    INSERT INTO Machines
                    (
                        NamePt,
                        NameJp,
                        MachineCode,
                        MachineKey,
                        LineCode,
                        LocalId,
                        SectorId,
                        IsActive
                    )
                    VALUES
                    (
                        @namePt,
                        @nameJp,
                        @machineCode,
                        @machineKey,
                        @lineCode,
                        @localId,
                        @sectorId,
                        1
                    );
                    SELECT last_insert_rowid();",
                new
                {
                    namePt = normalizedMachineCode,
                    nameJp = normalizedMachineCode,
                    machineCode = normalizedMachineCode,
                    machineKey,
                    lineCode = normalizedLineCode,
                    localId,
                    sectorId
                }
            );

            return new Machine
            {
                Id = newId,
                NamePt = normalizedMachineCode,
                NameJp = normalizedMachineCode,
                MachineCode = normalizedMachineCode,
                MachineKey = machineKey,
                LineCode = normalizedLineCode,
                LocalId = localId,
                SectorId = sectorId,
                IsActive = true
            };
        }

        public static string BuildMachineKey(string machineCode, string lineCode)
        {
            return $"{NormalizeCode(lineCode)}:{NormalizeCode(machineCode)}";
        }

        private static void LogLocationMismatchIfNeeded(Machine machine, string machineKey, int? importedSectorId, int? importedLocalId)
        {
            var sectorMismatch = importedSectorId.HasValue && machine.SectorId != importedSectorId.Value;
            var localMismatch = importedLocalId.HasValue && machine.LocalId != importedLocalId.Value;
            if (!sectorMismatch && !localMismatch)
            {
                return;
            }

            Console.WriteLine(
                "[ProductionMachineRepository][LocationMismatch] "
                + $"MachineId={machine.Id} "
                + $"MachineKey={machineKey} "
                + $"ExistingSectorId={FormatNullable(machine.SectorId)} "
                + $"ImportedSectorId={FormatNullable(importedSectorId)} "
                + $"ExistingLocalId={FormatNullable(machine.LocalId)} "
                + $"ImportedLocalId={FormatNullable(importedLocalId)} "
                + "Action=kept_existing_manual_location");
        }

        private static string FormatNullable(int? value)
        {
            return value.HasValue ? value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "NULL";
        }

        private static string NormalizeCode(string value)
        {
            return Regex.Replace(value ?? string.Empty, @"[\s\u200B-\u200D\uFEFF]", string.Empty)
                .ToUpperInvariant();
        }

        public static bool IsValidProductionMachineCode(string value)
        {
            var normalized = NormalizeCode(value);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            if (decimal.TryParse(normalized, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
            {
                return false;
            }

            return ValidMachineCodeRegex.IsMatch(normalized);
        }
    }
}
