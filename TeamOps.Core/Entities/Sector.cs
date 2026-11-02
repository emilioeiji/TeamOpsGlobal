namespace TeamOps.Core.Entities
{
    public class Sector
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string NamePt { get; set; } = string.Empty;
        public string NameJp { get; set; } = string.Empty;
        public int? OrganizationNodeId { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
