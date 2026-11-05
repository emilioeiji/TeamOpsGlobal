using System;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using TeamOps.Config;
using TeamOps.Data.Db;
using TeamOps.OperatorApp.Forms;

namespace TeamOps.OperatorApp
{
    internal static class Program
    {
        public static DbSettings DbSettings { get; private set; } = null!;
        public static SqliteConnectionFactory ConnectionFactory { get; private set; } = null!;
        public static string AttachmentsFolder { get; private set; } = string.Empty;

        [STAThread]
        static void Main()
        {
            // Cultura igual ao UI
            var culture = "pt-BR";
            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(culture);

            ApplicationConfiguration.Initialize();

            AttachmentsFolder = StoragePathResolver.GetRequiredPath("HikitsuguiAttachmentPath");

            // Le o caminho do banco no app.config.
            DbSettings = new DbSettings(portableMode: false);

            // Garante que o banco existe
            var initializer = new DbInitializer(DbSettings);
            initializer.EnsureCreated();

            // Cria a connection factory
            ConnectionFactory = new SqliteConnectionFactory(DbSettings);

            // Abre o app do operador
            Application.Run(new HTMLHikitsuguiOperatorRead());
        }
    }
}
