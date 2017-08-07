namespace SampleForm
{
    partial class MainWindow
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.btnConnect = new System.Windows.Forms.Button();
            this.btnClear = new System.Windows.Forms.Button();
            this.btnRefresh = new System.Windows.Forms.Button();
            this.btnDump = new System.Windows.Forms.Button();
            this.btnSettings = new System.Windows.Forms.Button();
            this.OpenEmulator = new SampleForm.OpenEmulator();
            this.SuspendLayout();
            // 
            // btnConnect
            // 
            this.btnConnect.Location = new System.Drawing.Point(592, 12);
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.Size = new System.Drawing.Size(115, 33);
            this.btnConnect.TabIndex = 1;
            this.btnConnect.Text = "Connect";
            this.btnConnect.UseVisualStyleBackColor = true;
            this.btnConnect.Click += new System.EventHandler(this.btnConnect_Click);
            // 
            // btnClear
            // 
            this.btnClear.Location = new System.Drawing.Point(592, 64);
            this.btnClear.Name = "btnClear";
            this.btnClear.Size = new System.Drawing.Size(115, 33);
            this.btnClear.TabIndex = 2;
            this.btnClear.Text = "Clear";
            this.btnClear.UseVisualStyleBackColor = true;
            // 
            // btnRefresh
            // 
            this.btnRefresh.Location = new System.Drawing.Point(592, 103);
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new System.Drawing.Size(115, 33);
            this.btnRefresh.TabIndex = 3;
            this.btnRefresh.Text = "Refresh";
            this.btnRefresh.UseVisualStyleBackColor = true;
            this.btnRefresh.Click += new System.EventHandler(this.btnRefresh_Click);
            // 
            // btnDump
            // 
            this.btnDump.Location = new System.Drawing.Point(592, 142);
            this.btnDump.Name = "btnDump";
            this.btnDump.Size = new System.Drawing.Size(115, 33);
            this.btnDump.TabIndex = 4;
            this.btnDump.Text = "Dump";
            this.btnDump.UseVisualStyleBackColor = true;
            // 
            // btnSettings
            // 
            this.btnSettings.Location = new System.Drawing.Point(592, 181);
            this.btnSettings.Name = "btnSettings";
            this.btnSettings.Size = new System.Drawing.Size(115, 33);
            this.btnSettings.TabIndex = 5;
            this.btnSettings.Text = "Settings";
            this.btnSettings.UseVisualStyleBackColor = true;
            this.btnSettings.Click += new System.EventHandler(this.btnSettings_Click);
            // 
            // OpenEmulator
            // 
            this.OpenEmulator.AcceptsTab = true;
            this.OpenEmulator.BackColor = System.Drawing.Color.Black;
            this.OpenEmulator.ForeColor = System.Drawing.Color.Lime;
            this.OpenEmulator.Location = new System.Drawing.Point(12, 12);
            this.OpenEmulator.MaxLength = 1920;
            this.OpenEmulator.Name = "OpenEmulator";
            this.OpenEmulator.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.None;
            this.OpenEmulator.Size = new System.Drawing.Size(574, 373);
            this.OpenEmulator.TabIndex = 0;
            this.OpenEmulator.Text = "";
            // 
            // MainWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(719, 397);
            this.Controls.Add(this.btnSettings);
            this.Controls.Add(this.btnDump);
            this.Controls.Add(this.btnRefresh);
            this.Controls.Add(this.btnClear);
            this.Controls.Add(this.btnConnect);
            this.Controls.Add(this.OpenEmulator);
            this.Name = "MainWindow";
            this.Text = "Open3270";
            this.ResumeLayout(false);

        }

        #endregion

        private OpenEmulator OpenEmulator;
        private System.Windows.Forms.Button btnConnect;
        private System.Windows.Forms.Button btnClear;
        private System.Windows.Forms.Button btnRefresh;
        private System.Windows.Forms.Button btnDump;
        private System.Windows.Forms.Button btnSettings;
    }
}

