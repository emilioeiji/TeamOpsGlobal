namespace TeamOps.Core.Entities
{
    public sealed class AccessProfile
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string NamePt { get; set; } = string.Empty;
        public string NameJp { get; set; } = string.Empty;
        public int HierarchyRank { get; set; }
        public string ScopeMode { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
