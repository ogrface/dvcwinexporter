using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;

namespace CrmSolutionExporter
{
    internal class MainFormEventHandlers
    {
        private readonly MainForm form;
        private readonly MainFormComponents components;
        private ServiceClient? serviceClient;
        private List<Entity> allSolutions;

        public MainFormEventHandlers(MainForm form, MainFormComponents components)
        {
            this.form = form;
            this.components = components;

            allSolutions = new List<Entity>();
            ConnectEventHandlers();
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

                var uniqueName = s.GetAttributeValue<string>("uniquename")?.ToLower() ?? "";
                var friendlyName = s.GetAttributeValue<string>("friendlyname")?.ToLower() ?? "";

                return uniqueName.Contains(filterText) || friendlyName.Contains(filterText);
            });

            foreach (var solution in filteredSolutions)
            {
                var uniqueName = solution.GetAttributeValue<string>("uniquename");
                var friendlyName = solution.GetAttributeValue<string>("friendlyname");
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

            this.Log("Connecting to CRM organization...");

            try
            {
                await Task.Run(() =>
                {
                    serviceClient = DataverseActions.ConnectToDataverse(components.TxtServerUrl.Text);
                });

                if (serviceClient == null || !serviceClient.IsReady)
                {
                    this.Log($"Failed to connect: {serviceClient?.LastError}");
                    MessageBox.Show("Failed to connect to CRM organization.", "Connection Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                this.Log("Successfully connected!");
                this.Log("Retrieving solutions...");

                allSolutions = await Task.Run(() => DataverseActions.GetUnmanagedSolutions(serviceClient));

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
            if (serviceClient == null || !serviceClient.IsReady)
            {
                MessageBox.Show("Please connect to CRM first.", "Validation Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedSolutions = new List<string>();

            if (components.ChkAllSolutions.Checked)
            {
                selectedSolutions.AddRange(allSolutions.Select(s =>
                    s.GetAttributeValue<string>("uniquename")));
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
            components.ProgressBar.Maximum = selectedSolutions.Count * 2; // Managed + Unmanaged
            components.ProgressBar.Value = 0;

            try
            {
                var solutionPath = components.TxtSolutionPath.Text;
                DataverseActions.EnsureDirectoryExists(solutionPath);
                var exportPath = System.IO.Path.Combine(solutionPath, "Export");
                DataverseActions.EnsureDirectoryExists(exportPath);

                foreach (var solutionName in selectedSolutions)
                {
                    this.Log($"Exporting {solutionName} (unmanaged)...");
                    await Task.Run(() => DataverseActions.ExportSolution(serviceClient, solutionName, solutionPath, false, this.Log));
                    components.ProgressBar.Value++;

                    this.Log($"Exporting {solutionName} (managed)...");
                    await Task.Run(() => DataverseActions.ExportSolution(serviceClient, solutionName, solutionPath, true, this.Log));
                    components.ProgressBar.Value++;
                }

                this.Log("Checking .NET installation...");
                DataverseActions.CheckDotNetInstallation(this.Log);

                this.Log("Ensuring PAC CLI is installed...");
                await DataverseActions.EnsurePacCliInstalled(this.Log);

                // Unpack solutions
                foreach (var solutionName in selectedSolutions)
                {
                    await DataverseActions.UnpackSolution(solutionName, solutionPath, exportPath, this.Log);
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
                components.ProgressBar.Visible = false;
                components.BtnExport.Enabled = true;
                components.BtnConnect.Enabled = true;
            }
        }
    }
}