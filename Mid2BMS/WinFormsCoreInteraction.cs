using System.Windows.Forms;

namespace Mid2BMS
{
    internal sealed class WinFormsCoreInteraction : ICoreInteraction
    {
        public void ShowMessage(string message)
        {
            MessageBox.Show(message);
        }

        public void ShowMessage(string message, string caption)
        {
            MessageBox.Show(message, caption);
        }

        public bool ConfirmAbort(string message, string caption)
        {
            return MessageBox.Show(message, caption, MessageBoxButtons.YesNo) == DialogResult.Yes;
        }
    }
}
