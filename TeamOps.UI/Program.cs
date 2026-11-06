// Project: TeamOps.UI
// File: Program.cs
using System;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using TeamOps.Config;
using TeamOps.Data.Db;
using TeamOps.Data.Repositories;
using TeamOps.Core.Entities;
using TeamOps.Data.Security;
using TeamOps.Core.Security;

namespace TeamOps.UI
{
    internal static class Program
    {
        private const string DefaultLocale = "pt-BR";

        public static DbSettings DbSettings { get; private set; } = null!;
        public static SqliteConnectionFactory ConnectionFactory { get; private set; } = null!;
        public static User? CurrentUser { get; set; }
        public static UserSession? CurrentSession { get; set; }
        public static string CurrentLocale { get; private set; } = DefaultLocale;

        [STAThread]
        private static void Main()
        {
            SetCurrentLocale(DefaultLocale);

            ApplicationConfiguration.Initialize();

            try
            {
                DbSettings = new DbSettings(portableMode: false);
                var initializer = new DbInitializer(DbSettings);
                initializer.EnsureCreated();
                ConnectionFactory = new SqliteConnectionFactory(DbSettings);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Nao foi possivel iniciar o TeamOps.\n\n{ex.Message}",
                    "Falha de configuracao ou armazenamento",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            var provisioning = new FirstRunProvisioningService(ConnectionFactory);
            if (provisioning.IsProvisioningRequired())
            {
                using var setupForm = new Forms.FormFirstRunSetup();
                if (setupForm.ShowDialog() != DialogResult.OK)
                    return;
            }

            using (var loginForm = new Forms.FormLogin())
            {
                if (loginForm.ShowDialog() == DialogResult.OK && Program.CurrentUser != null)
                {
                    var authorization = new AuthorizationService(ConnectionFactory);
                    var access = authorization.GetContext(Program.CurrentUser.Id);
                    CurrentSession = new UserSession(access, authorization.GetAuthorizedSectors(access));
                    Application.Run(new Forms.FormDashboardHtml(Program.CurrentUser));
                }
            }
        }

        public static void SetCurrentLocale(string? locale)
        {
            var normalized = string.Equals(locale, "ja-JP", StringComparison.OrdinalIgnoreCase)
                ? "ja-JP"
                : DefaultLocale;

            CurrentLocale = normalized;

            var culture = CultureInfo.GetCultureInfo(normalized);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
        }
    }
}
