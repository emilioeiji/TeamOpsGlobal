using Dapper;
using TeamOps.Core.Entities;
using TeamOps.Data.Db;

namespace TeamOps.Data.Security
{
    public sealed class SessionValidationService
    {
        private readonly SqliteConnectionFactory _factory;

        public SessionValidationService(SqliteConnectionFactory factory)
        {
            _factory = factory;
        }

        public bool IsCurrent(User authenticatedUser)
        {
            ArgumentNullException.ThrowIfNull(authenticatedUser);

            using var connection = _factory.CreateOpenConnection();
            return connection.ExecuteScalar<long>(@"
                SELECT EXISTS (
                    SELECT 1
                    FROM Users
                    WHERE Id = @userId
                      AND IsActive = 1
                      AND SessionVersion = @sessionVersion
                );",
                new
                {
                    userId = authenticatedUser.Id,
                    sessionVersion = authenticatedUser.SessionVersion
                }) == 1;
        }

        public void DemandCurrent(User authenticatedUser)
        {
            if (!IsCurrent(authenticatedUser))
                throw new UnauthorizedAccessException(
                    "A sessao foi encerrada porque o acesso ou a senha do usuario mudou.");
        }
    }
}
