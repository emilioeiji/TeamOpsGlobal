using TeamOps.Core.Common;

namespace TeamOps.Core.Entities
{
    public sealed class OrganizationNode
    {
        public int Id { get; set; }
        public int? ParentId { get; set; }
        public OrganizationNodeType NodeType { get; set; }
        public string Code { get; set; } = string.Empty;
        public string NamePt { get; set; } = string.Empty;
        public string NameJp { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
