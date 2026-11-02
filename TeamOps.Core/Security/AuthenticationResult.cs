using TeamOps.Core.Entities;

namespace TeamOps.Core.Security
{
    public sealed class AuthenticationResult
    {
        public bool Succeeded { get; init; }
        public User? User { get; init; }
        public string ErrorCode { get; init; } = string.Empty;
        public DateTime? LockedUntilUtc { get; init; }

        public static AuthenticationResult Success(User user) => new() { Succeeded = true, User = user };

        public static AuthenticationResult Failure(string errorCode, DateTime? lockedUntilUtc = null) =>
            new() { ErrorCode = errorCode, LockedUntilUtc = lockedUntilUtc };
    }
}
