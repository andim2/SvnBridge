using System.Windows.Forms;
using SvnBridge.Presenters;

namespace SvnBridge.Views
{
    public interface ISettingsView
    {
        SettingsViewPresenter Presenter { set; }
        DialogResult DialogResult { get; }
        void Show();
    }
}