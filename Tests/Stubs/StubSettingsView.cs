using System.Windows.Forms;
using SvnBridge.Presenters;
using SvnBridge.Views;

namespace UnitTests
{
    public class StubSettingsView : ISettingsView
    {
        #region Delegates

        public delegate void ShowDelegate();

        #endregion

        public DialogResult DialogResult_Return;

        public SettingsViewPresenter PresenterProperty;
        public bool Show_Called;
        public ShowDelegate Show_Delegate;

        #region ISettingsView Members

        public SettingsViewPresenter Presenter
        {
            set { PresenterProperty = value; }
            get { return PresenterProperty; }
        }

        public void Show()
        {
            if (Show_Delegate != null)
            {
                Show_Delegate();
            }

            Show_Called = true;
        }

        public DialogResult DialogResult
        {
            get { return DialogResult_Return; }
        }

        #endregion
    }
}