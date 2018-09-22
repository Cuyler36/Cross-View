using System.Windows;
using System.Windows.Controls;

namespace Cross_View
{
    /// <inheritdoc cref="Window" />
    /// <summary>
    /// Interaction logic for DrawListAddressBox.xaml
    /// </summary>
    public partial class DrawListAddressBox
    {
        public uint Address;

        public DrawListAddressBox()
        {
            InitializeComponent();
            DrawListAddressTextBox.Focus();
        }

        private string _lastText = "";

        private static bool CheckHexString(string text)
            => System.Text.RegularExpressions.Regex.IsMatch(text, @"\A\b[0-9a-fA-F]+\b\Z");

        private void DrawListAddressTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_lastText)) return;

            if (!CheckHexString(DrawListAddressTextBox.Text))
            {
                e.Handled = true;
                DrawListAddressTextBox.Text = _lastText;
            }
            else
            {
                _lastText = DrawListAddressTextBox.Text;
            }
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            if (!uint.TryParse(DrawListAddressTextBox.Text, System.Globalization.NumberStyles.HexNumber, null,
                out Address)) return;
            if (Address >= 0x81800000) return;

            DialogResult = true;
            Close();
        }

        private void DrawListAddressTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Return) return;

            e.Handled = true;
            OKButton_Click(null, null);
        }
    }
}
