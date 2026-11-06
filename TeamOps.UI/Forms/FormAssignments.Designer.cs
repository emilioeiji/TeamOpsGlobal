namespace TeamOps.UI.Forms
{
    partial class FormAssignments
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.ComboBox cmbGroupLeaders;
        private System.Windows.Forms.ListBox lstAvailable;
        private System.Windows.Forms.ListBox lstAssigned;
        private System.Windows.Forms.Button btnAdd;
        private System.Windows.Forms.Button btnRemove;
        private System.Windows.Forms.Label lblGL;
        private System.Windows.Forms.Label lblAvailable;
        private System.Windows.Forms.Label lblAssigned;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.cmbGroupLeaders = new System.Windows.Forms.ComboBox();
            this.lstAvailable = new System.Windows.Forms.ListBox();
            this.lstAssigned = new System.Windows.Forms.ListBox();
            this.btnAdd = new System.Windows.Forms.Button();
            this.btnRemove = new System.Windows.Forms.Button();
            this.lblGL = new System.Windows.Forms.Label();
            this.lblAvailable = new System.Windows.Forms.Label();
            this.lblAssigned = new System.Windows.Forms.Label();

            this.SuspendLayout();
            // 
            // lblGL
            // 
            this.lblGL.Text = "Lider operacional:";
            this.lblGL.Location = new System.Drawing.Point(20, 20);
            this.lblGL.AutoSize = true;
            // 
            // cmbGroupLeaders
            // 
            this.cmbGroupLeaders.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbGroupLeaders.Location = new System.Drawing.Point(120, 17);
            this.cmbGroupLeaders.Width = 200;
            this.cmbGroupLeaders.SelectedIndexChanged += new System.EventHandler(this.cmbGroupLeaders_SelectedIndexChanged);
            // 
            // lblAvailable
            // 
            this.lblAvailable.Text = "Disponíveis:";
            this.lblAvailable.Location = new System.Drawing.Point(20, 60);
            this.lblAvailable.AutoSize = true;
            // 
            // lstAvailable
            // 
            this.lstAvailable.Location = new System.Drawing.Point(20, 80);
            this.lstAvailable.Size = new System.Drawing.Size(200, 200);
            // 
            // lblAssigned
            // 
            this.lblAssigned.Text = "Atribuídos:";
            this.lblAssigned.Location = new System.Drawing.Point(320, 60);
            this.lblAssigned.AutoSize = true;
            // 
            // lstAssigned
            // 
            this.lstAssigned.Location = new System.Drawing.Point(320, 80);
            this.lstAssigned.Size = new System.Drawing.Size(200, 200);
            // 
            // btnAdd
            // 
            this.btnAdd.Text = ">>";
            this.btnAdd.Location = new System.Drawing.Point(240, 130);
            this.btnAdd.Size = new System.Drawing.Size(60, 30);
            this.btnAdd.Click += new System.EventHandler(this.btnAdd_Click);
            // 
            // btnRemove
            // 
            this.btnRemove.Text = "<<";
            this.btnRemove.Location = new System.Drawing.Point(240, 180);
            this.btnRemove.Size = new System.Drawing.Size(60, 30);
            this.btnRemove.Click += new System.EventHandler(this.btnRemove_Click);
            // 
            // FormAssignments
            // 
            this.ClientSize = new System.Drawing.Size(550, 320);
            this.Controls.Add(this.lblGL);
            this.Controls.Add(this.cmbGroupLeaders);
            this.Controls.Add(this.lblAvailable);
            this.Controls.Add(this.lstAvailable);
            this.Controls.Add(this.lblAssigned);
            this.Controls.Add(this.lstAssigned);
            this.Controls.Add(this.btnAdd);
            this.Controls.Add(this.btnRemove);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Atribuicao de operadores";
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
