namespace TeamOps.Data.Security
{
    public static class PasswordPolicy
    {
        public static void DemandStrong(string? password)
        {
            password ??= string.Empty;
            if (password.Length < 12 || !password.Any(char.IsUpper) || !password.Any(char.IsLower)
                || !password.Any(char.IsDigit) || !password.Any(ch => !char.IsLetterOrDigit(ch)))
            {
                throw new ArgumentException(
                    "A senha deve ter ao menos 12 caracteres, com maiuscula, minuscula, numero e simbolo.",
                    nameof(password));
            }
        }
    }
}
