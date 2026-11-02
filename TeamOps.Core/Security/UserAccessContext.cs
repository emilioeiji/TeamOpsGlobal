namespace TeamOps.Core.Security
{
    public sealed class UserAccessContext
    {
        public int UserId { get; init; }
        public string CodigoFJ { get; init; } = string.Empty;
        public string ProfileCode { get; init; } = string.Empty;
        public int HierarchyRank { get; init; }
        public string ScopeMode { get; init; } = string.Empty;
        public int ReferenceNodeId { get; init; }
        public IReadOnlySet<int> AuthorizedSectorIds { get; init; } = new HashSet<int>();
    }
}
