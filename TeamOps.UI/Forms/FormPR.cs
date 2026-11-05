using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TeamOps.Core.Entities;
using TeamOps.Config;
using TeamOps.Data.Repositories;

namespace TeamOps.UI.Forms
{
    public partial class FormPR : Form
    {
        private readonly PRRepository _prRepo;
        private readonly PRCategoriaRepository _catRepo;
        private readonly PRPrioridadeRepository _prioRepo;
        private readonly SectorRepository _sectorRepo;
        private readonly Operator _currentUser;
        private readonly OperatorRepository _operatorRepo;
        private readonly string _prDirectory;
        private readonly string _prTemplate;

        public FormPR(
            PRRepository prRepo,
            PRCategoriaRepository catRepo,
            PRPrioridadeRepository prioRepo,
            SectorRepository sectorRepo,
            OperatorRepository operatorRepo,
            Operator currentUser)
        {
            InitializeComponent();

            _prRepo = prRepo;
            _catRepo = catRepo;
            _prioRepo = prioRepo;
            _sectorRepo = sectorRepo;
            _operatorRepo = operatorRepo;
            _currentUser = currentUser;
            _prDirectory = StoragePathResolver.GetRequiredPath("PRDirectory");
            _prTemplate = StoragePathResolver.GetRequiredPath("PRTemplate");

            Load += FormPR_Load;
            btnSalvar.Click += btnSalvar_Click;
            btnCancelar.Click += btnCancelar_Click;
            btnFechar.Click += (s, e) => Close();
        }

        private void FormPR_Load(object? sender, EventArgs e)
        {
            LoadLookups();

            txtAutor.Text = _currentUser.NameNihongo;
            txtDataEmissao.Text = DateTime.Now.ToString("yyyy-MM-dd");
            txtNomeArquivo.ReadOnly = true;
            txtTitulo.TextChanged += (s, e) => GerarNomeArquivo();
        }

        private void LoadLookups()
        {
            cmbSetor.DataSource = _sectorRepo.GetAll().ToList();
            cmbSetor.DisplayMember = "NamePt";
            cmbSetor.ValueMember = "Id";
            cmbSetor.SelectedIndex = -1;

            cmbCategoria.DataSource = _catRepo.GetAll().ToList();
            cmbCategoria.DisplayMember = "NamePt";
            cmbCategoria.ValueMember = "Id";
            cmbCategoria.SelectedIndex = -1;

            cmbPrioridade.DataSource = _prioRepo.GetAll().ToList();
            cmbPrioridade.DisplayMember = "NamePt";
            cmbPrioridade.ValueMember = "Id";
            cmbPrioridade.SelectedIndex = -1;
        }

        private void btnSalvar_Click(object? sender, EventArgs e)
        {
            if (!ValidateForm())
                return;

            var setorId = GetSelectedId(cmbSetor);
            var categoriaId = GetSelectedId(cmbCategoria);
            var prioridadeId = GetSelectedId(cmbPrioridade);

            var pr = new PR
            {
                SetorId = setorId,
                CategoriaId = categoriaId,
                PrioridadeId = prioridadeId,
                Titulo = txtTitulo.Text.Trim(),
                NomeArquivo = txtNomeArquivo.Text.Trim(),
                DataEmissao = DateTime.Now,
                AutorCodigoFJ = _currentUser.CodigoFJ
            };

            int id = _prRepo.Add(pr);

            // Gera o arquivo
            GerarPRExcel(id);

            // Caminho completo do arquivo gerado
            string caminhoFinal = Path.Combine(_prDirectory, txtNomeArquivo.Text.Trim());

            // Abre o arquivo no Windows
            System.Diagnostics.Process.Start(new ProcessStartInfo()
            {
                FileName = caminhoFinal,
                UseShellExecute = true
            });

            MessageBox.Show("PR salvo e arquivo gerado com sucesso!");
            ClearForm();
        }

        private void btnCancelar_Click(object? sender, EventArgs e)
        {
            ClearForm();
        }

        private bool ValidateForm()
        {
            if (cmbSetor.SelectedIndex < 0)
            {
                MessageBox.Show("Selecione o setor.");
                return false;
            }

            if (cmbCategoria.SelectedIndex < 0)
            {
                MessageBox.Show("Selecione a categoria.");
                return false;
            }

            if (cmbPrioridade.SelectedIndex < 0)
            {
                MessageBox.Show("Selecione a prioridade.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(txtTitulo.Text))
            {
                MessageBox.Show("Digite o título.");
                return false;
            }

            return true;
        }

        private void ClearForm()
        {
            cmbSetor.SelectedIndex = -1;
            cmbCategoria.SelectedIndex = -1;
            cmbPrioridade.SelectedIndex = -1;
            txtTitulo.Clear();
            txtNomeArquivo.Clear();
        }

        private void GerarNomeArquivo()
        {
            if (string.IsNullOrWhiteSpace(txtTitulo.Text))
            {
                txtNomeArquivo.Clear();
                return;
            }

            var ultimo = _prRepo.GetAll().FirstOrDefault()?.Id ?? 0;
            var novoId = ultimo + 1;

            txtNomeArquivo.Text = $"PR_{novoId}_{txtTitulo.Text.Trim().Replace(" ", "_")}.xlsx";
        }

        private void GerarPRExcel(int prId)
        {
            if (!File.Exists(_prTemplate))
            {
                MessageBox.Show("Arquivo modelo PR não encontrado.");
                return;
            }

            if (!Directory.Exists(_prDirectory))
                Directory.CreateDirectory(_prDirectory);

            string nomeArquivo = txtNomeArquivo.Text.Trim();
            string caminhoFinal = Path.Combine(_prDirectory, nomeArquivo);

            try
            {
                File.Copy(_prTemplate, caminhoFinal, overwrite: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao copiar o arquivo modelo:\n" + ex.Message);
                return;
            }

            using var wb = new XLWorkbook(caminhoFinal);
            var ws = wb.Worksheet("PR文書");
            var prioridade = cmbPrioridade.SelectedItem as LookupItem;
            var categoriaId = GetSelectedId(cmbCategoria);

            ws.Cell("D4").Value = prId;
            ws.Cell("F5").Value = txtTitulo.Text.Trim();
            ws.Cell("D1").Value = prioridade?.NamePt ?? string.Empty;
            ws.Cell("T2").Value = DateTime.Now.ToString("yyyy-MM-dd");
            ws.Cell("T4").Value = _currentUser.NameNihongo;

            // Marca a categoria com ✔
            switch (categoriaId)
            {
                case 1:
                    ws.Cell("D7").Value = "✔";
                    break;

                case 2:
                    ws.Cell("D9").Value = "✔";
                    break;

                case 3:
                    ws.Cell("N7").Value = "✔";
                    break;

                case 4:
                    ws.Cell("N9").Value = "✔";
                    break;
            }

            ExportarListaFuncionarios(wb);

            wb.Save();
        }
        private void ExportarListaFuncionarios(XLWorkbook wb)
        {
            var ws = wb.Worksheet("Operadores");

            int setorSelecionado = GetSelectedId(cmbSetor);

            // Carrega operadores ativos do setor selecionado + setor 3
            var operadores = _operatorRepo.GetAll()
                .Where(o => o.Status
                         && (o.SectorId == setorSelecionado || o.SectorId == 3))
                .OrderBy(o => o.NameRomanji)
                .ToList();

            int rowDia = 3;   // B3
            int rowNoite = 3; // C3

            foreach (var op in operadores)
            {
                if (op.ShiftId == 1) // Hiru
                {
                    ws.Cell(rowDia, 2).Value = op.NameRomanji; // Coluna B
                    rowDia++;
                }
                else if (op.ShiftId == 2) // Yoru
                {
                    ws.Cell(rowNoite, 3).Value = op.NameRomanji; // Coluna C
                    rowNoite++;
                }
            }
        }

        private static int GetSelectedId(ComboBox comboBox)
        {
            return comboBox.SelectedValue is int value ? value : 0;
        }
    }
}
