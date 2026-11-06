using Dapper;

namespace TeamOps.Data.Security
{
    internal static class OrganizationRootResolver
    {
        public static int ResolveRequired(
            System.Data.IDbConnection connection,
            System.Data.IDbTransaction? transaction = null)
        {
            var rootIds = connection.Query<int>(@"
                SELECT Id
                FROM OrganizationNodes
                WHERE ParentId IS NULL
                  AND NodeType = 'Unit'
                  AND IsActive = 1
                ORDER BY Id
                LIMIT 2;", transaction: transaction).ToArray();

            return rootIds.Length switch
            {
                1 => rootIds[0],
                0 => throw new InvalidOperationException("A raiz organizacional ativa nao foi encontrada."),
                _ => throw new InvalidOperationException("A estrutura possui mais de uma raiz organizacional ativa.")
            };
        }
    }
}
