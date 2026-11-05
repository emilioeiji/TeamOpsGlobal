using TeamOps.Core.Security;
using TeamOps.Data.Db;

namespace TeamOps.Data.Security
{
    public sealed class PaidLeaveAuthorizationService
    {
        private readonly SqliteConnectionFactory _factory;
        private readonly AuthorizationService _authorization;

        public PaidLeaveAuthorizationService(SqliteConnectionFactory factory)
        {
            _factory = factory;
            _authorization = new AuthorizationService(factory);
        }

        public int[] GetAuthorizedSectorIds(int userId, string actionCode = PermissionActions.View)
        {
            _authorization.Demand(userId, ModuleCodes.PaidLeave, actionCode);
            return _authorization.GetContext(userId).AuthorizedSectorIds.ToArray();
        }

        public bool IsAllowed(int userId, string actionCode)
        {
            return _authorization.IsAllowed(userId, ModuleCodes.PaidLeave, actionCode);
        }

        public void DemandOperator(int userId, string actionCode, string codigoFJ)
        {
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT SectorId
                FROM Operators
                WHERE CodigoFJ = @codigoFJ AND Status = 1;";
            cmd.Parameters.AddWithValue("@codigoFJ", codigoFJ ?? string.Empty);
            var sectorId = cmd.ExecuteScalar();
            if (sectorId == null || sectorId == DBNull.Value)
                throw new UnauthorizedAccessException("Operador inexistente ou inativo.");

            _authorization.Demand(userId, ModuleCodes.PaidLeave, actionCode, Convert.ToInt32(sectorId));
        }

        public void DemandRequest(int userId, string actionCode, int requestId)
        {
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT operator.SectorId
                FROM AcompYukyu request
                JOIN Operators operator ON operator.CodigoFJ = request.OperatorCodigoFJ
                WHERE request.Id = @requestId;";
            cmd.Parameters.AddWithValue("@requestId", requestId);
            var sectorId = cmd.ExecuteScalar();
            if (sectorId == null || sectorId == DBNull.Value)
                throw new UnauthorizedAccessException("Solicitacao inexistente ou fora do escopo.");

            _authorization.Demand(userId, ModuleCodes.PaidLeave, actionCode, Convert.ToInt32(sectorId));
        }

        public void DemandRequestMove(int userId, string actionCode, int requestId, string targetCodigoFJ)
        {
            DemandRequest(userId, actionCode, requestId);
            DemandOperator(userId, actionCode, targetCodigoFJ);
        }
    }
}
