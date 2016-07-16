using System.Windows.Forms;
using Example.Properties;

namespace Example
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            var binder = Settings.Default.Binder;

            binder.BindControl(textBox1, settings => settings.TextBox, this);
            binder.Subscribe(label1, label => label.Text, settings => settings.TextBox, this);

            binder.BindControl(checkBox1, settings => settings.CheckBox, this);
            binder.Subscribe(groupBox1, box => box.Enabled, settings => settings.CheckBox, this);

            binder.SendUpdates(this);

            Closed += (sender, args) => binder.Settings.Save();
        }
    }
}