using System.Configuration;

namespace TeamOps.Config
{
    public static class StoragePathResolver
    {
        public const string StorageRootKey = "StorageRoot";

        public static string GetStorageRoot()
        {
            var configured = ConfigurationManager.AppSettings[StorageRootKey];
            if (string.IsNullOrWhiteSpace(configured))
                return Path.Combine(AppPaths.GetUserDataDirectory(), "storage");

            return Resolve(configured, AppContext.BaseDirectory, AppContext.BaseDirectory);
        }

        public static string GetRequiredPath(string key)
        {
            var configured = ConfigurationManager.AppSettings[key];
            if (string.IsNullOrWhiteSpace(configured))
                throw new InvalidOperationException($"{key} nao esta configurado no app.config.");
            return Resolve(configured, GetStorageRoot(), AppContext.BaseDirectory);
        }

        public static string? GetOptionalPath(string key)
        {
            var configured = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(configured)
                ? null
                : Resolve(configured, GetStorageRoot(), AppContext.BaseDirectory);
        }

        public static string Resolve(string configuredPath, string storageRoot, string appBaseDirectory)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
                throw new ArgumentException("O caminho configurado deve ser informado.", nameof(configuredPath));

            var expanded = Environment.ExpandEnvironmentVariables(configuredPath.Trim());
            if (LooksLikeDriveRelativePath(expanded))
                throw new InvalidOperationException(
                    $"Caminho relativo a unidade nao suportado: {expanded}. Use caminho absoluto local ou UNC.");

            var candidate = Path.IsPathRooted(expanded)
                ? expanded
                : Path.Combine(storageRoot, expanded);
            if (!Path.IsPathRooted(candidate))
                candidate = Path.Combine(appBaseDirectory, candidate);

            return Path.GetFullPath(candidate)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        public static bool IsUncPath(string path) =>
            !string.IsNullOrWhiteSpace(path)
            && path.StartsWith(@"\\", StringComparison.Ordinal);

        private static bool LooksLikeDriveRelativePath(string path) =>
            path.Length >= 2
            && char.IsLetter(path[0])
            && path[1] == ':'
            && (path.Length == 2 || (path[2] != '\\' && path[2] != '/'));
    }

    public sealed class StorageDiagnosticsService
    {
        private static readonly StoragePathDefinition[] Definitions =
        [
            new(StoragePathResolver.StorageRootKey, "Raiz de armazenamento", StoragePathType.Directory, RelativeToStorageRoot: false),
            new("DatabasePath", "Banco SQLite", StoragePathType.File, RelativeToStorageRoot: false),
            new("HikitsuguiAttachmentPath", "Anexos Hikitsugui", StoragePathType.Directory),
            new("OperatorScheduleDirectory", "Schedules", StoragePathType.Directory),
            new("HaidaiExportDirectory", "Exportacao Haidai", StoragePathType.Directory),
            new("ProductionEventsDirectory", "Eventos de producao", StoragePathType.Directory),
            new("ProductionSourceEventsDirectory", "Origem de eventos", StoragePathType.Directory),
            new("ProductionSourceDatDirectory", "Origem DAT", StoragePathType.Directory),
            new("ProductionImportBatchPath", "Importador de producao", StoragePathType.File),
            new("ProductionImportCompletionFile", "Sinal de importacao", StoragePathType.File),
            new("PRDirectory", "Diretorio PR", StoragePathType.Directory),
            new("PRTemplate", "Modelo PR", StoragePathType.File),
            new("CLDirectory", "Diretorio CL", StoragePathType.Directory),
            new("CLTemplate", "Modelo CL", StoragePathType.File),
            new("Ec2AdministratorFilePath", "Arquivo EC2", StoragePathType.File)
        ];

        public IReadOnlyList<StoragePathDiagnostic> Inspect()
        {
            var root = StoragePathResolver.GetStorageRoot();
            return Definitions.Select(definition => Inspect(definition, root)).ToList();
        }

        private static StoragePathDiagnostic Inspect(StoragePathDefinition definition, string storageRoot)
        {
            try
            {
                var configured = ConfigurationManager.AppSettings[definition.Key];
                if (string.IsNullOrWhiteSpace(configured))
                    return new(definition.Key, definition.Label, string.Empty, "Nao configurado", false, false, definition.Type);

                var path = definition.RelativeToStorageRoot
                    ? StoragePathResolver.Resolve(configured, storageRoot, AppContext.BaseDirectory)
                    : StoragePathResolver.Resolve(configured, AppContext.BaseDirectory, AppContext.BaseDirectory);
                var exists = definition.Type == StoragePathType.Directory
                    ? Directory.Exists(path)
                    : File.Exists(path);
                var parentExists = definition.Type == StoragePathType.Directory
                    ? Directory.Exists(Path.GetDirectoryName(path) ?? path) || exists
                    : Directory.Exists(Path.GetDirectoryName(path));
                var location = StoragePathResolver.IsUncPath(path) ? "UNC" : "Local";
                var status = exists ? $"Disponivel ({location})" : parentExists ? $"Ausente; pasta pai disponivel ({location})" : $"Indisponivel ({location})";
                return new(definition.Key, definition.Label, path, status, exists, StoragePathResolver.IsUncPath(path), definition.Type);
            }
            catch (Exception ex)
            {
                return new(definition.Key, definition.Label, string.Empty, ex.Message, false, false, definition.Type);
            }
        }

        private sealed record StoragePathDefinition(string Key, string Label, StoragePathType Type, bool RelativeToStorageRoot = true);
    }

    public enum StoragePathType { File, Directory }
    public sealed record StoragePathDiagnostic(string Key, string Label, string ResolvedPath, string Status,
        bool Exists, bool IsUnc, StoragePathType Type);
}
