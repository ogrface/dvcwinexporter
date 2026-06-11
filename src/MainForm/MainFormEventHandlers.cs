namespace CrmSolutionExporter
{
    internal class MainFormEventHandlers
    {
        private readonly MainForm form;
        private readonly MainFormComponents components;
        private bool isConnected;
        private List<SolutionInfo> allSolutions;
        private DateTime? pacAuthTime;
        private static readonly TimeSpan PacAuthLifetime = TimeSpan.FromMinutes(50);

        private bool IsPacAuthExpired => pacAuthTime == null || DateTime.UtcNow - pacAuthTime.Value > PacAuthLifetime;

        public MainFormEventHandlers(MainForm form, MainFormComponents components)
        {
            this.form = form;
            this.components = components;

            allSolutions = new List<SolutionInfo>();
            LoadUserSettings();
            ConnectEventHandlers();
        }

        private void LoadUserSettings()
        {
            var settings = UserSettings.Load();
            if (!string.IsNullOrEmpty(settings.ServerUrl))
                components.TxtServerUrl.Text = settings.ServerUrl;
            if (!string.IsNullOrEmpty(settings.GitRepoPath))
                components.TxtSolutionPath.Text = settings.GitRepoPath;
        }

        private void ConnectEventHandlers()
        {
            components.BtnConnect.Click += BtnConnect_Click;
            components.BtnExport.Click += BtnExport_Click;
            components.BtnBrowse.Click += BtnBrowse_Click;
            components.ChkAllSolutions.CheckedChanged += ChkAllSolutions_CheckedChanged;
            components.TxtFilter.TextChanged += TxtFilter_TextChanged;
        }

        public void Log(string message)
        {
            if (components.TxtLog?.InvokeRequired ?? false)
            {
                components.TxtLog.Invoke(new Action(() => Log(message)));
                return;
            }

            components.TxtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }

        public void BtnBrowse_Click(object? sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.SelectedPath = components.TxtSolutionPath?.Text ?? "";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    components.TxtSolutionPath.Text = dialog.SelectedPath;
                }
            }
        }

        public void ChkAllSolutions_CheckedChanged(object? sender, EventArgs e)
        {
            components.LstSolutions.Enabled = !components.ChkAllSolutions.Checked;

            if (components.ChkAllSolutions.Checked)
            {
                for (int i = 0; i < components.LstSolutions.Items.Count; i++)
                {
                    components.LstSolutions.SetItemChecked(i, true);
                }
            }
            else
            {
                for (int i = 0; i < components.LstSolutions.Items.Count; i++)
                {
                    components.LstSolutions.SetItemChecked(i, false);
                }
            }
        }

        public void TxtFilter_TextChanged(object? sender, EventArgs e)
        {
            FilterSolutions();
        }

        private void FilterSolutions()
        {
            if (allSolutions == null || allSolutions.Count == 0)
                return;

            var filterText = components.TxtFilter.Text.Trim().ToLower();
            var checkedItems = new HashSet<string>();

            // Remember which items were checked
            foreach (var item in components.LstSolutions.CheckedItems)
            {
                var itemStr = item?.ToString();
                if (itemStr != null)
                {
                    checkedItems.Add(itemStr);
                }
            }

            components.LstSolutions.Items.Clear();

            var filteredSolutions = allSolutions.Where(s =>
            {
                if (string.IsNullOrEmpty(filterText))
                    return true;

                var uniqueName = s.UniqueName?.ToLower() ?? "";
                var friendlyName = s.FriendlyName?.ToLower() ?? "";

                return uniqueName.Contains(filterText) || friendlyName.Contains(filterText);
            });

            foreach (var solution in filteredSolutions)
            {
                var uniqueName = solution.UniqueName;
                var friendlyName = solution.FriendlyName;
                var displayText = $"{uniqueName} ({friendlyName})";

                var index = components.LstSolutions.Items.Add(displayText);

                // Restore checked state
                if (checkedItems.Contains(displayText))
                {
                    components.LstSolutions.SetItemChecked(index, true);
                }
            }
        }

        public async void BtnConnect_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(components.TxtServerUrl.Text))
            {
                MessageBox.Show("Please enter a server URL.", "Validation Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!DataverseActions.ValidateServerUrl(components.TxtServerUrl.Text))
            {
                MessageBox.Show("Invalid server URL format. Expected format: https://<yourorg>.crm.dynamics.com",
                    "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            components.BtnConnect.Enabled = false;
            components.BtnExport.Enabled = false;
            components.LstSolutions.Items.Clear();

            this.Log("Connecting...");

            try
            {
                this.Log("Checking .NET installation...");
                DataverseActions.CheckDotNetInstallation(this.Log);

                this.Log("Ensuring PAC CLI is installed...");
                await DataverseActions.EnsurePacCliInstalled(this.Log);

                await DataverseActions.AuthenticatePacCli(components.TxtServerUrl.Text, this.Log);
                pacAuthTime = DateTime.UtcNow;

                new UserSettings
                {
                    ServerUrl = components.TxtServerUrl.Text,
                    GitRepoPath = components.TxtSolutionPath.Text
                }.Save();

                this.Log("Retrieving solutions...");

                allSolutions = await DataverseActions.GetUnmanagedSolutions(this.Log);
                isConnected = true;

                this.Log($"Found {allSolutions.Count} unmanaged solutions.");

                FilterSolutions();

                components.BtnExport.Enabled = true;
                components.LstSolutions.Enabled = true;
                components.TxtFilter.Enabled = true;
            }
            catch (Exception ex)
            {
                this.Log($"Error: {ex.Message}");
                MessageBox.Show($"Error connecting: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                components.BtnConnect.Enabled = true;
            }
        }

        public async void BtnExport_Click(object? sender, EventArgs e)
        {
            if (!isConnected)
            {
                MessageBox.Show("Please connect first.", "Validation Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedSolutions = new List<string>();

            if (components.ChkAllSolutions.Checked)
            {
                selectedSolutions.AddRange(allSolutions.Select(s => s.UniqueName));
            }
            else
            {
                if (components.LstSolutions.CheckedItems.Count == 0)
                {
                    MessageBox.Show("Please select at least one solution to export.",
                        "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                foreach (var item in components.LstSolutions.CheckedItems)
                {
                    var itemText = item.ToString();
                    if (itemText != null)
                    {
                        var index = itemText.IndexOf(" (");
                        if (index > 0)
                        {
                            var uniqueName = itemText.Substring(0, index);
                            selectedSolutions.Add(uniqueName);
                        }
                    }
                }
            }

            components.BtnExport.Enabled = false;
            components.BtnConnect.Enabled = false;
            components.ProgressBar.Visible = true;
            components.ProgressBar.Maximum = selectedSolutions.Count * 3; // Export managed + unmanaged + unpack/copy
            components.ProgressBar.Value = 0;

            string? tempDir = null;

            try
            {
                var repoPath = components.TxtSolutionPath.Text;
                var repoSolutionsPath = System.IO.Path.Combine(repoPath, "Solutions");

                if (!Directory.Exists(repoSolutionsPath))
                {
                    MessageBox.Show($"Solutions folder not found at:\n{repoSolutionsPath}\n\nPlease ensure the path points to a valid repository clone.",
                        "Invalid Repository Path", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"DVCWinExporter_{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempDir);
                var tempUnpackDir = System.IO.Path.Combine(tempDir, "Unpacked");
                Directory.CreateDirectory(tempUnpackDir);

                this.Log($"Using temp folder: {tempDir}");

                foreach (var solutionName in selectedSolutions)
                {
                    this.Log($"Exporting {solutionName} (unmanaged)...");
                    await DataverseActions.ExportSolution(solutionName, tempDir, false, this.Log);
                    components.ProgressBar.Value++;

                    this.Log($"Exporting {solutionName} (managed)...");
                    await DataverseActions.ExportSolution(solutionName, tempDir, true, this.Log);
                    components.ProgressBar.Value++;
                }

                if (IsPacAuthExpired)
                {
                    this.Log("PAC CLI token expired, re-authenticating...");
                    await DataverseActions.AuthenticatePacCli(components.TxtServerUrl.Text, this.Log);
                    pacAuthTime = DateTime.UtcNow;
                }

                // Unpack and copy to repository
                foreach (var solutionName in selectedSolutions)
                {
                    var repoSolutionDir = System.IO.Path.Combine(repoSolutionsPath, solutionName);
                    if (!Directory.Exists(repoSolutionDir))
                    {
                        this.Log($"Skipping {solutionName} — no matching folder in repository.");
                        components.ProgressBar.Value++;
                        continue;
                    }

                    var repoContentDir = System.IO.Path.Combine(repoSolutionDir, solutionName);
                    if (Directory.Exists(repoContentDir))
                    {
                        var result = MessageBox.Show(
                            $"The solution '{solutionName}' already has content in the repository at:\n{repoContentDir}\n\nDo you want to overwrite it?",
                            "Overwrite Solution Content",
                            MessageBoxButtons.YesNoCancel,
                            MessageBoxIcon.Question);

                        if (result == DialogResult.Cancel)
                        {
                            this.Log("Export cancelled by user.");
                            break;
                        }

                        if (result == DialogResult.No)
                        {
                            this.Log($"Skipping {solutionName} (not overwriting).");
                            components.ProgressBar.Value++;
                            continue;
                        }
                    }

                    await DataverseActions.UnpackSolution(solutionName, tempDir, tempUnpackDir, this.Log);
                    DataverseActions.CopySolutionToRepository(solutionName, tempUnpackDir, repoSolutionsPath, this.Log);
                    components.ProgressBar.Value++;
                }

                this.Log("Export completed successfully!");
                MessageBox.Show("All solutions exported successfully!", "Success",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                this.Log($"Error during export: {ex.Message}");
                MessageBox.Show($"Error during export: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (tempDir != null && Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
                components.ProgressBar.Visible = false;
                components.BtnExport.Enabled = true;
                components.BtnConnect.Enabled = true;
            }
        }
    }
}