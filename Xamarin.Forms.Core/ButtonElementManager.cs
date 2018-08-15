using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;

namespace Xamarin.Forms
{
	internal static class ButtonElementManager
	{
		public const string PressedVisualState = "Pressed";

		public static void Init(IButtonController ButtonElementManager)
		{
			ButtonElementManager.CommandChanged += CommandChanged;
			ButtonElementManager.CommandCanExecuteChanged += CommandCanExecuteChanged;
		}

		public static void CommandChanged(object sender, BindableValueChangedEventArgs e)
		{
			IButtonController ButtonElementManager = (IButtonController)sender;
			if (ButtonElementManager.Command != null)
			{
				CommandCanExecuteChanged(ButtonElementManager, EventArgs.Empty);
			}
			else
			{
				ButtonElementManager.IsEnabledCore = true;
			}
		}

		public static void CommandCanExecuteChanged(object sender, EventArgs e)
		{
			IButtonController ButtonElementManager = (IButtonController)sender;
			ICommand cmd = ButtonElementManager.Command;
			if (cmd != null)
			{
				ButtonElementManager.IsEnabledCore = cmd.CanExecute(ButtonElementManager.CommandParameter);
			}
		}


		public static void ElementClicked(VisualElement visualElement, IButtonController ButtonElementManager)
		{
			if (visualElement.IsEnabled == true)
			{
				ButtonElementManager.Command?.Execute(ButtonElementManager.CommandParameter);
				ButtonElementManager.PropagateUpClicked();
			}
		}

		public static void ElementPressed(VisualElement visualElement, IButtonController ButtonElementManager)
		{
			if (visualElement.IsEnabled == true)
			{
				ButtonElementManager.SetIsPressed(true);
				visualElement.ChangeVisualStateInternal();
				ButtonElementManager.PropagateUpPressed();
			}
		}

		public static void ElementReleased(VisualElement visualElement, IButtonController ButtonElementManager)
		{
			if (visualElement.IsEnabled == true)
			{
				ButtonElementManager.SetIsPressed(false);
				visualElement.ChangeVisualStateInternal();
				ButtonElementManager.PropagateUpReleased();
			}
		}


	}
}
