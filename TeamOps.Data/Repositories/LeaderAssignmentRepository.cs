using Dapper;
using TeamOps.Core.Security;
using TeamOps.Data.Db;
using TeamOps.Data.Security;

namespace TeamOps.Data.Repositories
{
    public sealed class LeaderAssignmentRepository
    {
        private readonly SqliteConnectionFactory _factory;
        private readonly AuthorizationService _authorization;

        public LeaderAssignmentRepository(SqliteConnectionFactory factory)
        {
            _factory = factory;
            _authorization = new AuthorizationService(factory);
        }

        public IReadOnlyList<LeaderAssignmentOperator> GetLeaders(int actorUserId)
        {
            var sectors = DemandAdministration(actorUserId);
            using var conn = _factory.CreateOpenConnection();
            return conn.Query<LeaderAssignmentOperator>(@"
                SELECT CodigoFJ, NameRomanji, SectorId
                FROM Operators
                WHERE Status = 1
                  AND IsLeader = 1
                  AND SectorId IN @sectors
                ORDER BY NameRomanji COLLATE NOCASE, CodigoFJ;",
                new { sectors }).ToList();
        }

        public IReadOnlyList<LeaderAssignmentOperator> GetAssigned(int actorUserId, string leaderCodigoFJ)
        {
            var sectors = DemandAdministration(actorUserId);
            using var conn = _factory.CreateOpenConnection();
            var leaderSectorId = DemandLeader(conn, leaderCodigoFJ, sectors);
            return conn.Query<LeaderAssignmentOperator>(@"
                SELECT o.CodigoFJ, o.NameRomanji, o.SectorId
                FROM OperatorLeaderAssignments assignment
                JOIN Operators o ON o.CodigoFJ = assignment.OperatorCodigoFJ
                WHERE assignment.LeaderCodigoFJ = @leaderCodigoFJ
                  AND o.Status = 1
                  AND o.SectorId = @leaderSectorId
                ORDER BY o.NameRomanji COLLATE NOCASE, o.CodigoFJ;",
                new { leaderCodigoFJ = Normalize(leaderCodigoFJ), leaderSectorId }).ToList();
        }

        public IReadOnlyList<LeaderAssignmentOperator> GetAvailable(int actorUserId, string leaderCodigoFJ)
        {
            var sectors = DemandAdministration(actorUserId);
            using var conn = _factory.CreateOpenConnection();
            var leaderCode = Normalize(leaderCodigoFJ);
            var leaderSectorId = DemandLeader(conn, leaderCode, sectors);
            return conn.Query<LeaderAssignmentOperator>(@"
                SELECT o.CodigoFJ, o.NameRomanji, o.SectorId
                FROM Operators o
                WHERE o.Status = 1
                  AND o.SectorId = @leaderSectorId
                  AND o.CodigoFJ <> @leaderCode COLLATE NOCASE
                  AND NOT EXISTS (
                      SELECT 1
                      FROM OperatorLeaderAssignments assignment
                      WHERE assignment.LeaderCodigoFJ = @leaderCode COLLATE NOCASE
                        AND assignment.OperatorCodigoFJ = o.CodigoFJ
                  )
                ORDER BY o.NameRomanji COLLATE NOCASE, o.CodigoFJ;",
                new { leaderCode, leaderSectorId }).ToList();
        }

        public void Add(int actorUserId, string leaderCodigoFJ, string operatorCodigoFJ)
        {
            var sectors = DemandAdministration(actorUserId);
            using var conn = _factory.CreateOpenConnection();
            using var transaction = conn.BeginTransaction();
            var leaderCode = Normalize(leaderCodigoFJ);
            var operatorCode = Normalize(operatorCodigoFJ);
            var leaderSectorId = DemandLeader(conn, leaderCode, sectors, transaction);
            var operatorSectorId = DemandOperator(conn, operatorCode, sectors, transaction);
            if (leaderSectorId != operatorSectorId)
                throw new UnauthorizedAccessException("Lider e operador devem pertencer ao mesmo setor.");
            if (string.Equals(leaderCode, operatorCode, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Um lider nao pode ser associado a si mesmo.");

            conn.Execute(@"
                INSERT OR IGNORE INTO OperatorLeaderAssignments
                    (LeaderCodigoFJ, OperatorCodigoFJ, AssignedByUserId)
                VALUES (@leaderCode, @operatorCode, @actorUserId);",
                new { leaderCode, operatorCode, actorUserId }, transaction);
            WriteAudit(conn, transaction, actorUserId, "LeaderOperatorAssigned", leaderCode, operatorCode);
            transaction.Commit();
        }

        public void Remove(int actorUserId, string leaderCodigoFJ, string operatorCodigoFJ)
        {
            var sectors = DemandAdministration(actorUserId);
            using var conn = _factory.CreateOpenConnection();
            using var transaction = conn.BeginTransaction();
            var leaderCode = Normalize(leaderCodigoFJ);
            var operatorCode = Normalize(operatorCodigoFJ);
            DemandLeader(conn, leaderCode, sectors, transaction);
            DemandOperator(conn, operatorCode, sectors, transaction);
            conn.Execute(@"
                DELETE FROM OperatorLeaderAssignments
                WHERE LeaderCodigoFJ = @leaderCode COLLATE NOCASE
                  AND OperatorCodigoFJ = @operatorCode COLLATE NOCASE;",
                new { leaderCode, operatorCode }, transaction);
            WriteAudit(conn, transaction, actorUserId, "LeaderOperatorUnassigned", leaderCode, operatorCode);
            transaction.Commit();
        }

        private int[] DemandAdministration(int actorUserId)
        {
            _authorization.Demand(actorUserId, ModuleCodes.AccessControl, PermissionActions.Administer);
            return _authorization.GetContext(actorUserId).AuthorizedSectorIds.ToArray();
        }

        private static int DemandLeader(System.Data.IDbConnection conn, string codigoFJ,
            IReadOnlyCollection<int> sectors, System.Data.IDbTransaction? transaction = null)
        {
            return conn.QuerySingleOrDefault<int?>(@"
                SELECT SectorId FROM Operators
                WHERE CodigoFJ = @codigoFJ COLLATE NOCASE
                  AND Status = 1 AND IsLeader = 1 AND SectorId IN @sectors;",
                new { codigoFJ = Normalize(codigoFJ), sectors }, transaction)
                ?? throw new UnauthorizedAccessException("Lider inexistente ou fora do escopo autorizado.");
        }

        private static int DemandOperator(System.Data.IDbConnection conn, string codigoFJ,
            IReadOnlyCollection<int> sectors, System.Data.IDbTransaction? transaction = null)
        {
            return conn.QuerySingleOrDefault<int?>(@"
                SELECT SectorId FROM Operators
                WHERE CodigoFJ = @codigoFJ COLLATE NOCASE
                  AND Status = 1 AND SectorId IN @sectors;",
                new { codigoFJ = Normalize(codigoFJ), sectors }, transaction)
                ?? throw new UnauthorizedAccessException("Operador inexistente ou fora do escopo autorizado.");
        }

        private static void WriteAudit(System.Data.IDbConnection conn, System.Data.IDbTransaction transaction,
            int actorUserId, string eventType, string leaderCode, string operatorCode) =>
            conn.Execute(@"
                INSERT INTO SecurityAudit (ActorUserId, EventType, TargetType, TargetId, Details)
                VALUES (@actorUserId, @eventType, 'LeaderAssignment', @targetId, @details);",
                new
                {
                    actorUserId,
                    eventType,
                    targetId = $"{leaderCode}:{operatorCode}",
                    details = $"Leader={leaderCode};Operator={operatorCode}"
                }, transaction);

        private static string Normalize(string? codigoFJ) =>
            (codigoFJ ?? string.Empty).Trim().ToUpperInvariant();
    }

    public sealed class LeaderAssignmentOperator
    {
        public string CodigoFJ { get; set; } = string.Empty;
        public string NameRomanji { get; set; } = string.Empty;
        public int SectorId { get; set; }
    }
}
