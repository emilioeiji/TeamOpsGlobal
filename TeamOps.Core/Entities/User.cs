using TeamOps.Core.Common;

namespace TeamOps.Core.Entities
{
    public class User
    {
        public int Id { get; set; }
        public string Login { get; set; } = string.Empty;       // usado para login
        public string? CodigoFJ { get; set; }                   // opcional
        public string Name { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public AccessLevel AccessLevel { get; set; } = AccessLevel.Basic;
        public int? ProfileId { get; set; }
        public int? ReferenceNodeId { get; set; }
        public bool IsActive { get; set; } = true;
        public int FailedLoginAttempts { get; set; }
        public DateTime? LockedUntilUtc { get; set; }
        public int SessionVersion { get; set; } = 1;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
