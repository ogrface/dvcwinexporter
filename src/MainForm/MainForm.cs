namespace CrmSolutionExporter
{
    internal partial class MainForm : Form
    {
        private MainFormComponents components;
        private MainFormEventHandlers eventHandlers;

        public MainForm()
        {
            components = new MainFormComponents(this);
            eventHandlers = new MainFormEventHandlers(this, components);
        }
    }
}