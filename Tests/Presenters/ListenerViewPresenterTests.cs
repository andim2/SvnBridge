using System.Windows.Forms;
using Xunit;
using SvnBridge.Stubs;
using Assert = CodePlex.NUnitExtensions.Assert;
using SvnBridge.Infrastructure;
using SvnBridge.Interfaces;
using System;

namespace SvnBridge.Presenters
{
	public class ListenerViewPresenterTests
	{
        public ListenerViewPresenterTests()
        {
            stubListener = new StubListener();
            stubView = new StubListenerView();
            stubErrorView = new StubErrorsView();
        }

		private readonly StubListenerView stubView;
		private readonly StubListener stubListener;
		private readonly StubErrorsView stubErrorView;

		private ListenerViewPresenter CreatePresenter()
		{
			return new ListenerViewPresenter(stubView, stubErrorView, stubListener);
		}

		[Fact]
		public void TestCancelChangeSettingsDoesntStopListener()
		{
			stubListener.Get_Port = 8081;
			ListenerViewPresenter presenter = CreatePresenter();
			StubSettingsView stubSettingsView = new StubSettingsView();
			stubSettingsView.Show_Delegate =
				delegate
				{
					stubSettingsView.Presenter.Port = 8082;
					stubSettingsView.DialogResult_Return = DialogResult.Cancel;
				};

			presenter.ChangeSettings(stubSettingsView);

			Assert.False(stubListener.Stop_Called);
		}

		[Fact]
		public void TestChangeSettingsDefaultsToExistingSettings()
		{
			stubListener.Get_Port = 8081;
			ListenerViewPresenter presenter = CreatePresenter();
			StubSettingsView stubSettingsView = new StubSettingsView();

			presenter.ChangeSettings(stubSettingsView);

			Assert.Equal(stubSettingsView.Presenter.Port, 8081);
		}

		[Fact]
		public void TestChangeSettingsWithChangesStopsAndStartsListener()
		{
			stubListener.Get_Port = 8081;
			ListenerViewPresenter presenter = CreatePresenter();
			StubSettingsView stubSettingsView = new StubSettingsView();
			stubSettingsView.Show_Delegate =
				delegate
				{
					stubSettingsView.Presenter.Port = 8082;
				};

			presenter.ChangeSettings(stubSettingsView);

			Assert.True(stubListener.Stop_Called);
			Assert.True(stubListener.Start_Called);
		}

		[Fact]
		public void TestChangeSettingsWithNoChangesDoesntStopListener()
		{
			stubListener.Get_Port = 8081;
			ListenerViewPresenter presenter = CreatePresenter();
			StubSettingsView stubSettingsView = new StubSettingsView();
			stubSettingsView.Show_Delegate =
				delegate
				{
					stubSettingsView.Presenter.Port = 8081;
				};

			presenter.ChangeSettings(stubSettingsView);

			Assert.False(stubListener.Stop_Called);
		}

		[Fact]
		public void TestConstructorSetsViewsPresenter()
		{
			ListenerViewPresenter presenter = CreatePresenter();

			Assert.Equal(presenter, stubView.Set_Presenter);
		}

		[Fact]
		public void TestGetPortReturnsListenersPort()
		{
			int expected = 8081;
			ListenerViewPresenter presenter = CreatePresenter();

			stubListener.Get_Port = 8081;

			Assert.Equal(expected, presenter.Port);
		}

		[Fact]
		public void WhenListenerRaiseAnErrorWillShowInView()
		{
			CreatePresenter();
			stubListener.RaiseListenErrorEvent("blah");
			Assert.True(stubView.OnListenerError_Called);
		}

		[Fact]
		public void WhenListenerRaiseAnErrorWillAddToErrorsView()
		{
			CreatePresenter();
			stubListener.RaiseListenErrorEvent("blah");
			Assert.True(stubErrorView.AddError_Called);
		}

		[Fact]
		public void WhenAskedToShowErrorsWillShowErrorsView()
		{
			CreatePresenter().ShowErrors();
			Assert.True(stubErrorView.Show_Called);
		}

		[Fact]
		public void TestShowCallsViewsShow()
		{
			ListenerViewPresenter presenter = CreatePresenter();

			presenter.Show();

			Assert.True(stubView.Show_Called);
		}

		[Fact]
		public void TestStartListenerCallsViewsOnListenerStarted()
		{
			ListenerViewPresenter presenter = CreatePresenter();

			presenter.StartListener();

			Assert.True(stubView.OnListenerStarted_Called);
		}

		[Fact]
		public void TestStartListenerStartListener()
		{
			ListenerViewPresenter presenter = CreatePresenter();

			presenter.StartListener();

			Assert.True(stubListener.Start_Called);
		}

		[Fact]
		public void TestStopListenerCallsViewsOnListenerStopped()
		{
			ListenerViewPresenter presenter = CreatePresenter();

			presenter.StopListener();

			Assert.True(stubView.OnListenerStopped_Called);
		}

		[Fact]
		public void TestStopListenerStopsListener()
		{
			ListenerViewPresenter presenter = CreatePresenter();

			presenter.StopListener();

			Assert.True(stubListener.Stop_Called);
		}
    }
}
