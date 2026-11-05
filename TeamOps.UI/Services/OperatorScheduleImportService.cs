using System;
using System.Globalization;
using System.IO;
using TeamOps.Core.Entities;
using TeamOps.Config;
using TeamOps.Data.Repositories;

namespace TeamOps.Services
{
    public class OperatorScheduleImportService
    {
        private readonly OperatorScheduleRepository _repo;

        public OperatorScheduleImportService(OperatorScheduleRepository repo)
        {
            _repo = repo;
        }

        public void Import(int sectorId, int shiftId, DateTime date)
        {
            var baseDir = StoragePathResolver.GetRequiredPath("OperatorScheduleDirectory");

            // Nome do arquivo: ex: 12-20260406.csv
            string fileName = $"{sectorId}{shiftId}-{date:yyyyMMdd}.csv";
            string fullPath = Path.Combine(baseDir, fileName);

            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"Arquivo não encontrado: {fullPath}");

            // Remove registros antigos do mesmo dia/turno
            _repo.DeleteByDateShiftSector(date, shiftId, sectorId);

            var lines = File.ReadAllLines(fullPath);

            if (lines.Length == 0)
                return; // CSV realmente vazio

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var parts = line.Split(',');
                if (parts.Length < 3)
                    continue;

                string codigoFJ = parts[0].Trim();

                // Se não conseguir converter, ignora a linha
                if (!int.TryParse(parts[1].Trim(), out int localId))
                    continue;

                if (!int.TryParse(parts[2].Trim(), out int csvSectorId))
                    continue;

                // Se o setor do CSV não for o mesmo da tela, ignora
                if (csvSectorId != sectorId)
                    continue;

                var schedule = new OperatorSchedule
                {
                    CodigoFJ = codigoFJ,
                    LocalId = localId,
                    SectorId = sectorId,
                    ShiftId = shiftId,
                    ScheduleDate = date
                };

                _repo.Add(schedule);
            }
        }
    }
}
