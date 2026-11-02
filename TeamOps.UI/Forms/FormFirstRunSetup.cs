using TeamOps.Data.Security;

namespace TeamOps.UI.Forms
{
    public sealed class FormFirstRunSetup : Form
    {
        private readonly TextBox _login = new();
        private readonly TextBox _name = new();
        private readonly TextBox _password = new() { UseSystemPasswordChar = true };
        private readonly TextBox _confirmation = new() { UseSystemPasswordChar = true };
        private readonly Label _message = new() { AutoSize = true, ForeColor = Color.Firebrick };

        public FormFirstRunSetup()
        {
            Text = "Configuração inicial / 初期設定";
            Width = 560;
            Height = 390;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(28),
                ColumnCount = 2,
                RowCount = 7
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            var title = new Label
            {
                Text = "Crie o primeiro Diretor / 最初の管理者を作成",
                Font = new Font(Font, FontStyle.Bold),
                AutoSize = true
            };
            layout.Controls.Add(title, 0, 0);
            layout.SetColumnSpan(title, 2);
            AddRow(layout, 1, "Login", _login);
            AddRow(layout, 2, "Nome / 名前", _name);
            AddRow(layout, 3, "Senha / パスワード", _password);
            AddRow(layout, 4, "Confirmar / 確認", _confirmation);
            layout.Controls.Add(_message, 0, 5);
            layout.SetColumnSpan(_message, 2);

            var create = new Button { Text = "Criar acesso / 作成", AutoSize = true, Anchor = AnchorStyles.Right };
            create.Click += Create_Click;
            layout.Controls.Add(create, 1, 6);
            AcceptButton = create;
            Controls.Add(layout);
        }

        private void Create_Click(object? sender, EventArgs e)
        {
            if (_password.Text != _confirmation.Text)
            {
                _message.Text = "As senhas não coincidem. / パスワードが一致しません。";
                return;
            }

            try
            {
                new FirstRunProvisioningService(Program.ConnectionFactory)
                    .ProvisionDirector(_login.Text, _name.Text, _password.Text);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                _message.Text = ex.Message;
            }
        }

        private static void AddRow(TableLayoutPanel layout, int row, string label, Control control)
        {
            control.Dock = DockStyle.Fill;
            layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            layout.Controls.Add(control, 1, row);
        }
    }
}
