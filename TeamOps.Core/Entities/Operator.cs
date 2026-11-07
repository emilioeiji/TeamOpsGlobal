namespace TeamOps.Core.Entities
{
    public class Operator
    {
        public string CodigoFJ { get; set; } = string.Empty;
        public string NameRomanji { get; set; } = string.Empty;
        public string NameNihongo { get; set; } = string.Empty;

        // IDs continuam sendo IDs (INT)
        public int ShiftId { get; set; }
        public int GroupId { get; set; }
        public int SectorId { get; set; }

        // Nomes para exibição no grid
        public string? ShiftName { get; set; }
        public string? GroupName { get; set; }
        public string? SectorName { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool Trainer { get; set; }
        public bool Status { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsLeader { get; set; }
        public string OperatorType { get; set; } = "Regular";

        public string? Telefone { get; set; }
        public string? Endereco { get; set; }
        public DateTime? Nascimento { get; set; }
    }
}
