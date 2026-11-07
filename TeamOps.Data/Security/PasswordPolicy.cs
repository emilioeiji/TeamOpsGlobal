namespace TeamOps.Data.Security
{
    public static class PasswordPolicy
    {
        public static void DemandStrong(string? password)
        {
            password ??= string.Empty;
            if (password.Length < 6 || !password.Any(char.IsUpper) || !password.Any(char.IsLower)
                || !password.Any(char.IsDigit))
            {
                throw new ArgumentException(
                    "A senha deve ter ao menos 6 caracteres, com letra maiuscula, letra minuscula e numero.",
                    nameof(password));
            }
        }
    }
}
