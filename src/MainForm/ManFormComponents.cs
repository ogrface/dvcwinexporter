using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace CrmSolutionExporter
{
    internal class MainFormComponents
    {
        private readonly MainForm form;

        public TextBox TxtServerUrl { get; private set; }
        public TextBox TxtSolutionPath { get; private set; }
        public Button BtnBrowse { get; private set; }
        public CheckBox ChkAllSolutions { get; private set; }
        public TextBox TxtFilter { get; private set; }
        public CheckedListBox LstSolutions { get; private set; }
        public Button BtnConnect { get; private set; }
        public Button BtnExport { get; private set; }
        public TextBox TxtLog { get; private set; }
        public ProgressBar ProgressBar { get; private set; }

        public MainFormComponents(MainForm form)
        {
            this.form = form;
            this.InitializeComponents();
        }

        private void InitializeComponents()
        {
            form.Text = "CRM Solution Exporter";
            form.Size = new Size(700, 600);
            form.StartPosition = FormStartPosition.CenterScreen;

            // Server URL
            var lblServerUrl = new Label
            {
                Text = "Server URL:",
                Location = new Point(20, 20),
                Size = new Size(100, 20)
            };
            form.Controls.Add(lblServerUrl);

            TxtServerUrl = new TextBox
            {
                Location = new Point(130, 18),
                Size = new Size(400, 20),
                Text = "https://<yourorg>.crm.dynamics.com"
            };
            form.Controls.Add(TxtServerUrl);

            BtnConnect = new Button
            {
                Text = "Connect",
                Location = new Point(540, 16),
                Size = new Size(120, 25)
            };
            
            form.Controls.Add(BtnConnect);

            // Export Path
            var lblSolutionPath = new Label
            {
                Text = "Export Path:",
                Location = new Point(20, 60),
                Size = new Size(100, 20)
            };
            form.Controls.Add(lblSolutionPath);

            TxtSolutionPath = new TextBox
            {
                Location = new Point(130, 58),
                Size = new Size(320, 20),
                Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CRMSolutions")
            };
            form.Controls.Add(TxtSolutionPath);

            BtnBrowse = new Button
            {
                Text = "Browse...",
                Location = new Point(460, 56),
                Size = new Size(80, 25)
            };
            
            form.Controls.Add(BtnBrowse);

            // All Solutions Checkbox
            ChkAllSolutions = new CheckBox
            {
                Text = "Export All Solutions",
                Location = new Point(130, 95),
                Size = new Size(150, 20)
            };
            
            form.Controls.Add(ChkAllSolutions);

            // Filter TextBox
            var lblFilter = new Label
            {
                Text = "Filter:",
                Location = new Point(300, 97),
                Size = new Size(50, 20)
            };
            form.Controls.Add(lblFilter);

            TxtFilter = new TextBox
            {
                Location = new Point(350, 95),
                Size = new Size(310, 20),
                Enabled = false
            };
            
            form.Controls.Add(TxtFilter);

            // Solutions List
            var lblSolutions = new Label
            {
                Text = "Solutions:",
                Location = new Point(20, 125),
                Size = new Size(100, 20)
            };
            form.Controls.Add(lblSolutions);

            LstSolutions = new CheckedListBox
            {
                Location = new Point(130, 125),
                Size = new Size(530, 150),
                CheckOnClick = true,
                Enabled = false
            };
            form.Controls.Add(LstSolutions);

            // Export Button
            BtnExport = new Button
            {
                Text = "Export Solutions",
                Location = new Point(280, 290),
                Size = new Size(140, 35),
                Enabled = false
            };
            
            form.Controls.Add(BtnExport);

            // Progress Bar
            ProgressBar = new ProgressBar
            {
                Location = new Point(130, 335),
                Size = new Size(530, 20),
                Visible = false
            };
            form.Controls.Add(ProgressBar);

            // Log TextBox
            var lblLog = new Label
            {
                Text = "Log:",
                Location = new Point(20, 365),
                Size = new Size(100, 20)
            };
            form.Controls.Add(lblLog);

            TxtLog = new TextBox
            {
                Location = new Point(130, 365),
                Size = new Size(530, 160),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true
            };
            form.Controls.Add(TxtLog);
        }
    }
}