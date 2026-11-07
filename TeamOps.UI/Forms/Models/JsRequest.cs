namespace TeamOps.UI.Forms.Models
{
    public class JsRequest
    {
        // Ação enviada pelo app.js
        public string action { get; set; } = string.Empty;

        // ID genérico usado em preview, reply, delete_reply, mark_read, edit, delete
        public int id { get; set; }

        // ============================
        // HIKITSUGUI - CAMPOS GERAIS
        // ============================
        public string text { get; set; } = string.Empty;       // reply
        public int parentId { get; set; }      // delete_reply
        public string dtInicial { get; set; } = string.Empty;
        public string dtFinal { get; set; } = string.Empty;
        public List<AttachmentItem> attachments { get; set; } = new();
        public string path { get; set; } = string.Empty;

        // ============================
        // FILTROS DO HIKITSUGUI
        // ============================
        public string audienceType { get; set; } = string.Empty;
        public int organizationNodeId { get; set; }
        public int shiftId { get; set; }
        public int operatorId { get; set; }
        public int reasonId { get; set; }
        public int typeId { get; set; }
        public int equipId { get; set; }          // FILTRO
        public int sectorIdFilter { get; set; }   // FILTRO (renomeado)
        public int localIdFilter { get; set; }    // FILTRO
        public string search { get; set; } = string.Empty;

        // ============================
        // CAMPOS DO MODAL DE EDIÇÃO
        // ============================
        public int categoryId { get; set; }
        public int equipmentId { get; set; }      // ← AGORA EXISTE
        public int localId { get; set; }          // ← AGORA É DO MODAL
        public int sectorId { get; set; }         // ← AGORA É DO MODAL
        public string description { get; set; } = string.Empty;

        // ============================
        // PAID LEAVE (já existente)
        // ============================
        public string opCodigoFJ { get; set; } = string.Empty;
        public string reqDate { get; set; } = string.Empty;
        public string notes { get; set; } = string.Empty;
        public int motivoId { get; set; }

        // ============================
        // SOBRA DE PECA
        // ============================
        public string date { get; set; } = string.Empty;
        public string lote { get; set; } = string.Empty;
        public decimal tanjuu { get; set; }
        public decimal pesoGramas { get; set; }
        public decimal quantidade { get; set; }
        public int machineId { get; set; }
        public int shainId { get; set; }
        public string observacao { get; set; } = string.Empty;
        public string item { get; set; } = string.Empty;
        public string operatorCodigoFJ { get; set; } = string.Empty;
        public string executorCodigoFJ { get; set; } = string.Empty;
        public string witnessCodigoFJ { get; set; } = string.Empty;
        public string guidance { get; set; } = string.Empty;

        // ============================
        // OPERATORS
        // ============================
        public string codigoFJ { get; set; } = string.Empty;
        public string nameRomanji { get; set; } = string.Empty;
        public string nameNihongo { get; set; } = string.Empty;
        public int groupId { get; set; }
        public string startDate { get; set; } = string.Empty;
        public bool hasEndDate { get; set; }
        public string endDate { get; set; } = string.Empty;
        public bool trainer { get; set; }
        public bool status { get; set; }
        public bool isLeader { get; set; }
        public string phone { get; set; } = string.Empty;
        public string address { get; set; } = string.Empty;
        public string birthDate { get; set; } = string.Empty;

        // ============================
        // ACCESS CONTROL
        // ============================
        public string login { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public string password { get; set; } = string.Empty;
        public string confirmPassword { get; set; } = string.Empty;
        public int userId { get; set; }
        public string profileCode { get; set; } = string.Empty;
        public int referenceNodeId { get; set; }
        public List<int> sectorIds { get; set; } = new();
        public List<string> profileCodes { get; set; } = new();
        public string operatorType { get; set; } = "Regular";
        public bool isActive { get; set; } = true;
        public List<ModulePermissionDto> permissions { get; set; } = new();

        // ============================
        // TASKS
        // ============================
        public string dueDate { get; set; } = string.Empty;
        public string leaderCodigoFJ { get; set; } = string.Empty;
        public string taskStatus { get; set; } = string.Empty;
        public string trainerCodigoFJ { get; set; } = string.Empty;
        public string masterCardStatus { get; set; } = string.Empty;

        // ============================
        // ANEXOS DO EDITAR
        // ============================
        public List<AttachmentExistingDto> existingAttachments { get; set; } = new();
        public List<AttachmentNewDto> newAttachments { get; set; } = new();
    }

    public class AttachmentItem
    {
        public string fileName { get; set; } = string.Empty;
        public string base64 { get; set; } = string.Empty;
    }

    public class ModulePermissionDto
    {
        public string moduleCode { get; set; } = string.Empty;
        public string actionCode { get; set; } = string.Empty;
        public bool isAllowed { get; set; }
    }

    public class AttachmentExistingDto
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
    }

    public class AttachmentNewDto
    {
        public string fileName { get; set; } = string.Empty;
        public string base64 { get; set; } = string.Empty;
    }

}
