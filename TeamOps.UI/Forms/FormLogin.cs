// Project: TeamOps.UI
// File: Forms/FormLogin.cs
using System;
using System.Windows.Forms;
using TeamOps.Core.Entities;
using TeamOps.Data.Repositories;
using TeamOps.Data.Security;

namespace TeamOps.UI.Forms
{
    public partial class FormLogin : Form
    {
        private readonly AuthenticationService _authentication;

        public FormLogin()
        {
            InitializeComponent();
            _authentication = new AuthenticationService(Program.ConnectionFactory);
            pictureBoxLogo.Image = Image.FromFile("Logo.png");
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }

        private void btnEntrar_Click(object sender, EventArgs e)
        {
            var login = NormalizeLogin(txtLogin.Text);
            var senha = txtSenha.Text;

            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(senha))
            {
                lblMensagem.ForeColor = Color.Firebrick;
                lblMensagem.Text = "Informe login e senha. / ログインとパスワードを入力してください。";
                return;
            }

            var result = _authentication.Authenticate(login, senha, Environment.MachineName);

            if (result.Succeeded && result.User != null)
            {
                Program.CurrentUser = result.User;
                lblMensagem.Text = string.Empty;
                DialogResult = DialogResult.OK;
            }
            else
            {
                lblMensagem.ForeColor = Color.Firebrick;
                lblMensagem.Text = result.ErrorCode == "locked"
                    ? "Acesso temporariamente bloqueado. Tente novamente mais tarde. / 一時的にロックされています。"
                    : "Login ou senha invalidos. / ログインまたはパスワードが正しくありません。";
            }
        }

        private void btnAlterarSenha_Click(object sender, EventArgs e)
        {
            using var form = new FormChangePassword(txtLogin.Text);
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                txtSenha.Clear();
                lblMensagem.ForeColor = Color.FromArgb(30, 108, 67);
                lblMensagem.Text = "Senha alterada. Entre com a nova senha. / 新しいパスワードでログインしてください。";
            }
        }

        private static string NormalizeLogin(string? login)
        {
            var normalized = (login ?? string.Empty).Trim();

            if (normalized.StartsWith("FJ", StringComparison.OrdinalIgnoreCase))
                return normalized.ToUpperInvariant();

            return normalized;
        }
    }
}

