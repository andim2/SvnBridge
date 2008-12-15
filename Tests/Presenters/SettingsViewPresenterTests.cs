using System.Windows.Forms;
using Xunit;
using SvnBridge.Net;
using SvnBridge.Stubs;
using Assert=CodePlex.NUnitExtensions.Assert;

namespace SvnBridge.Presenters
{
    public class SettingsViewPresenterTests
    {
        #region Setup/Teardown
        public SettingsViewPresenterTests()
        {
            stubView = new StubSettingsView();
        }
        #endregion

        private StubSettingsView stubView;

        private SettingsViewPresenter CreatePresenter()
        {
            return new SettingsViewPresenter(stubView, new ProxyInformation());
        }

        [Fact]
        public void TestConstructorSetsViewsPresenter()
        {
            SettingsViewPresenter presenter = CreatePresenter();

            Assert.Equal(stubView.Presenter, presenter);
        }

        [Fact]
        public void TestShowCallsViewsShow()
        {
            SettingsViewPresenter presenter = CreatePresenter();

            presenter.Show();

            Assert.True(stubView.Show_Called);
        }

        [Fact]
        public void TestViewSetsCancelled()
        {
            stubView.Show_Delegate =
                delegate { stubView.DialogResult_Return = DialogResult.Cancel; };
            SettingsViewPresenter presenter = CreatePresenter();

            presenter.Show();

            Assert.True(presenter.Canceled);
        }

        [Fact]
        public void TestViewSetsPort()
        {
            int expected = 8081;
            stubView.Show_Delegate =
                delegate { stubView.Presenter.Port = 8081; };
            SettingsViewPresenter presenter = CreatePresenter();

            presenter.Show();

            Assert.Equal(expected, presenter.Port);
        }
    }
}