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

            try
            {
                AttachmentsFolder = StoragePathResolver.GetRequiredPath("HikitsuguiAttachmentPath");
                DbSettings = new DbSettings(portableMode: false);
                var initializer = new DbInitializer(DbSettings);
                initializer.EnsureCreated();
                ConnectionFactory = new SqliteConnectionFactory(DbSettings);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Nao foi possivel iniciar o TeamOps Operator.\n\n{ex.Message}",
                    "Falha de configuracao ou armazenamento",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            // Abre o app do operador
            Application.Run(new HTMLHikitsuguiOperatorRead());
        }
    }
}
