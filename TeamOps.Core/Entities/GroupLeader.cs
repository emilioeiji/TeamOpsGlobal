// Project: TeamOps.Core
// File: Entities/GroupLeader.cs
using TeamOps.Core.Common;

namespace TeamOps.Core.Entities
{
    public class GroupLeader
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Login { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
