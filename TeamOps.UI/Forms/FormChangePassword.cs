using System;
using System.Windows.Forms;
using TeamOps.Core.Entities;
using TeamOps.Data.Repositories;
using TeamOps.Data.Security;

namespace TeamOps.UI.Forms
{
    public partial class FormChangePassword : Form
    {
        private readonly UserRepository _userRepo;

        public FormChangePassword(string login = "")
        {
            InitializeComponent();
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            _userRepo = new UserRepository(Program.ConnectionFactory);
            txtLogin.Text = NormalizeLogin(login);
        }

        private void btnSalvar_Click(object sender, EventArgs e)
        {
            var login = NormalizeLogin(txtLogin.Text);
            var currentPassword = txtSenhaAtual.Text;
            var newPassword = txtNovaSenha.Text;
            var confirmPassword = txtConfirmarSenha.Text;

            if (string.IsNullOrWhiteSpace(login))
            {
                lblMensagem.Text = "Informe o login. / ログインを入力してください。";
                return;
            }

            if (string.IsNullOrWhiteSpace(currentPassword))
            {
                lblMensagem.Text = "Informe a senha atual. / 現在のパスワードを入力してください。";
                return;
            }

            if (string.IsNullOrWhiteSpace(newPassword))
            {
                lblMensagem.Text = "Informe a nova senha. / 新しいパスワードを入力してください。";
                return;
            }

            if (newPassword != confirmPassword)
            {
                lblMensagem.Text = "A confirmacao nao confere. / 確認用パスワードが一致しません。";
                return;
            }

            try
            {
                PasswordPolicy.DemandStrong(newPassword);
            }
            catch (ArgumentException ex)
            {
                lblMensagem.Text = ex.Message.Split(Environment.NewLine)[0];
                return;
            }

            var user = _userRepo.GetByLogin(login);
            if (user == null)
            {
                lblMensagem.Text = "Usuario nao encontrado. / ユーザーが見つかりません。";
                return;
            }

            if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            {
                lblMensagem.Text = "Senha atual invalida. / 現在のパスワードが正しくありません。";
                return;
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 12);
            _userRepo.Update(user);

            MessageBox.Show(
                "Senha alterada com sucesso.\nパスワードを変更しました。",
                "TeamOps",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );

            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnCancelar_Click(object sender, EventArgs e)
        {
            Close();
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
