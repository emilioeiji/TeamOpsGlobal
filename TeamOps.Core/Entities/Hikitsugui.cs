// Project: TeamOps.Core
// File: Entities/Hikitsugui.cs
namespace TeamOps.Core.Entities
{
    public class Hikitsugui
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public int ShiftId { get; set; }
        public string CreatorCodigoFJ { get; set; } = "";
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = "";
        public int? EquipmentId { get; set; }
        public int? LocalId { get; set; }
        public string LocalName { get; set; } = "";
        public int? SectorId { get; set; }
        public string? SectorName { get; set; }
        public string Description { get; set; } = "";
        public string? AttachmentPath { get; set; }
    }
}
