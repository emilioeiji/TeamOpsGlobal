// Project: TeamOps.UI
// File: Forms/FormAssignments.cs
using TeamOps.Core.Entities;
using TeamOps.Data.Repositories;

namespace TeamOps.UI.Forms
{
    public partial class FormAssignments : Form
    {
        private readonly User _currentUser;
        private readonly LeaderAssignmentRepository _repository;

        public FormAssignments(User currentUser)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _repository = new LeaderAssignmentRepository(Program.ConnectionFactory);
            LoadLeaders();
        }

        private void LoadLeaders()
        {
            cmbGroupLeaders.SelectedIndexChanged -= cmbGroupLeaders_SelectedIndexChanged;

            var leaders = _repository.GetLeaders(_currentUser.Id)
                .Select(ToListItem)
                .ToList();
            cmbGroupLeaders.DataSource = leaders;
            cmbGroupLeaders.DisplayMember = nameof(OperatorListItem.Display);
            cmbGroupLeaders.ValueMember = nameof(OperatorListItem.CodigoFJ);

            cmbGroupLeaders.SelectedIndexChanged += cmbGroupLeaders_SelectedIndexChanged;
            LoadOperators();
        }

        private void LoadOperators()
        {
            if (cmbGroupLeaders.SelectedValue is not string leaderCodigoFJ)
            {
                lstAvailable.DataSource = null;
                lstAssigned.DataSource = null;
                return;
            }

            BindOperators(lstAvailable, _repository.GetAvailable(_currentUser.Id, leaderCodigoFJ));
            BindOperators(lstAssigned, _repository.GetAssigned(_currentUser.Id, leaderCodigoFJ));
        }

        private static void BindOperators(ListBox listBox,
            IReadOnlyList<LeaderAssignmentOperator> operators)
        {
            listBox.DataSource = operators.Select(ToListItem).ToList();
            listBox.DisplayMember = nameof(OperatorListItem.Display);
            listBox.ValueMember = nameof(OperatorListItem.CodigoFJ);
        }

        private static OperatorListItem ToListItem(LeaderAssignmentOperator value) =>
            new(value.CodigoFJ, $"{value.CodigoFJ} - {value.NameRomanji}");

        private void cmbGroupLeaders_SelectedIndexChanged(object? sender, EventArgs e) =>
            LoadOperators();

        private void btnAdd_Click(object sender, EventArgs e)
        {
            if (cmbGroupLeaders.SelectedValue is string leaderCodigoFJ &&
                lstAvailable.SelectedValue is string operatorCodigoFJ)
            {
                _repository.Add(_currentUser.Id, leaderCodigoFJ, operatorCodigoFJ);
                LoadOperators();
            }
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            if (cmbGroupLeaders.SelectedValue is string leaderCodigoFJ &&
                lstAssigned.SelectedValue is string operatorCodigoFJ)
            {
                _repository.Remove(_currentUser.Id, leaderCodigoFJ, operatorCodigoFJ);
                LoadOperators();
            }
        }

        private sealed record OperatorListItem(string CodigoFJ, string Display);
    }
}
