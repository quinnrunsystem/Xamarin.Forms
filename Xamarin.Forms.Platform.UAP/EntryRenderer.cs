using System.ComponentModel;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Xamarin.Forms.Internals;
using Xamarin.Forms.PlatformConfiguration.WindowsSpecific;
using Specifics = Xamarin.Forms.PlatformConfiguration.WindowsSpecific.InputView;

namespace Xamarin.Forms.Platform.UWP
{
	public class EntryRenderer : ViewRenderer<Entry, FormsTextBox>
	{
		bool _fontApplied;
		Brush _backgroundColorFocusedDefaultBrush;
		Brush _placeholderDefaultBrush;
		Brush _textDefaultBrush;
		Brush _defaultTextColorFocusBrush;
		Brush _defaultPlaceholderColorFocusBrush;
		bool _cursorPositionChangePending = false;
		bool _selectionLengthChangePending = false;
		bool _selectionIsUpdating;

		IElementController ElementController => Element as IElementController;

		protected override void OnElementChanged(ElementChangedEventArgs<Entry> e)
		{
			base.OnElementChanged(e);

			if (e.NewElement != null)
			{
				if (Control == null)
				{
					var textBox = new FormsTextBox { Style = Windows.UI.Xaml.Application.Current.Resources["FormsTextBoxStyle"] as Windows.UI.Xaml.Style };

					SetNativeControl(textBox);
					textBox.TextChanged += OnNativeTextChanged;
					textBox.KeyUp += TextBoxOnKeyUp;
					textBox.SelectionChanged += SelectionChanged;
					textBox.GotFocus += TextBoxGotFocus;
					// If the Forms VisualStateManager is in play or the user wants to disable the Forms legacy
					// color stuff, then the underlying textbox should just use the Forms VSM states
					textBox.UseFormsVsm = e.NewElement.HasVisualStateGroups()
						|| !e.NewElement.OnThisPlatform().GetIsLegacyColorModeEnabled();
				}

				// When we set the control text, it triggers the SelectionChanged event, which updates CursorPosition and SelectionLength;
				// These one-time-use variables will let us initialize a CursorPosition and SelectionLength via ctor/xaml/etc.
				_cursorPositionChangePending = Element.IsSet(Entry.CursorPositionProperty);
				_selectionLengthChangePending = Element.IsSet(Entry.SelectionLengthProperty);

				UpdateIsPassword();
				UpdateText();
				UpdatePlaceholder();
				UpdateTextColor();
				UpdateFont();
				UpdateInputScope();
				UpdateAlignment();
				UpdatePlaceholderColor();
				UpdateMaxLength();
				UpdateDetectReadingOrderFromContent();
				UpdateReturnType();
				UpdateCursorPosition();
				UpdateSelectionLength();
			}
		}

		void TextBoxGotFocus(object sender, RoutedEventArgs e)
		{
			UpdateCursorPosition();
			UpdateSelectionLength();
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && Control != null)
			{
				Control.TextChanged -= OnNativeTextChanged;
				Control.KeyUp -= TextBoxOnKeyUp;
				Control.SelectionChanged -= SelectionChanged;
				Control.GotFocus -= TextBoxGotFocus;
			}

			base.Dispose(disposing);
		}

		protected override void OnElementPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			base.OnElementPropertyChanged(sender, e);

			if (e.PropertyName == Entry.TextProperty.PropertyName)
				UpdateText();
			else if (e.PropertyName == Entry.IsPasswordProperty.PropertyName)
				UpdateIsPassword();
			else if (e.PropertyName == Entry.PlaceholderProperty.PropertyName)
				UpdatePlaceholder();
			else if (e.PropertyName == Entry.TextColorProperty.PropertyName)
				UpdateTextColor();
			else if (e.PropertyName == InputView.KeyboardProperty.PropertyName)
				UpdateInputScope();
			else if (e.PropertyName == InputView.IsSpellCheckEnabledProperty.PropertyName)
				UpdateInputScope();
			else if (e.PropertyName == Entry.IsTextPredictionEnabledProperty.PropertyName)
				UpdateInputScope();
			else if (e.PropertyName == Entry.FontAttributesProperty.PropertyName)
				UpdateFont();
			else if (e.PropertyName == Entry.FontFamilyProperty.PropertyName)
				UpdateFont();
			else if (e.PropertyName == Entry.FontSizeProperty.PropertyName)
				UpdateFont();
			else if (e.PropertyName == Entry.HorizontalTextAlignmentProperty.PropertyName)
				UpdateAlignment();
			else if (e.PropertyName == Entry.PlaceholderColorProperty.PropertyName)
				UpdatePlaceholderColor();
			else if (e.PropertyName == VisualElement.FlowDirectionProperty.PropertyName)
				UpdateAlignment();
			else if (e.PropertyName == InputView.MaxLengthProperty.PropertyName)
				UpdateMaxLength();
			else if (e.PropertyName == Specifics.DetectReadingOrderFromContentProperty.PropertyName)
				UpdateDetectReadingOrderFromContent();
			else if (e.PropertyName == Entry.ReturnTypeProperty.PropertyName)
				UpdateReturnType();
			else if (e.PropertyName == Entry.CursorPositionProperty.PropertyName)
			{
				_cursorPositionChangePending = true;
				UpdateCursorPosition();
			}
			else if (e.PropertyName == Entry.SelectionLengthProperty.PropertyName)
			{
				_selectionLengthChangePending = true;
				UpdateSelectionLength();
			}
		}

		protected override void UpdateBackgroundColor()
		{
			base.UpdateBackgroundColor();

			if (Control == null)
			{
				return;
			}

			// By default some platforms have alternate default background colors when focused
			BrushHelpers.UpdateColor(Element.BackgroundColor, ref _backgroundColorFocusedDefaultBrush, 
				() => Control.BackgroundFocusBrush, brush => Control.BackgroundFocusBrush = brush);
		}

		void OnNativeTextChanged(object sender, Windows.UI.Xaml.Controls.TextChangedEventArgs args)
		{
			Element.SetValueCore(Entry.TextProperty, Control.Text);
		}

		void TextBoxOnKeyUp(object sender, KeyRoutedEventArgs args)
		{
			if (args?.Key != VirtualKey.Enter)
				return;


			// Hide the soft keyboard; this matches the behavior of Forms on Android/iOS
			Windows.UI.ViewManagement.InputPane.GetForCurrentView().TryHide();

			((IEntryController)Element).SendCompleted();
		}

		void UpdateAlignment()
		{
			Control.TextAlignment = Element.HorizontalTextAlignment.ToNativeTextAlignment(((IVisualElementController)Element).EffectiveFlowDirection);
		}

		void UpdateFont()
		{
			if (Control == null)
				return;

			Entry entry = Element;

			if (entry == null)
				return;

			bool entryIsDefault = entry.FontFamily == null && entry.FontSize == Device.GetNamedSize(NamedSize.Default, typeof(Entry), true) && entry.FontAttributes == FontAttributes.None;

			if (entryIsDefault && !_fontApplied)
				return;

			if (entryIsDefault)
			{
				// ReSharper disable AccessToStaticMemberViaDerivedType
				// Resharper wants to simplify 'FormsTextBox' to 'Control', but then it'll conflict with the property 'Control'
				Control.ClearValue(FormsTextBox.FontStyleProperty);
				Control.ClearValue(FormsTextBox.FontSizeProperty);
				Control.ClearValue(FormsTextBox.FontFamilyProperty);
				Control.ClearValue(FormsTextBox.FontWeightProperty);
				Control.ClearValue(FormsTextBox.FontStretchProperty);
				// ReSharper restore AccessToStaticMemberViaDerivedType
			}
			else
			{
				Control.ApplyFont(entry);
			}

			_fontApplied = true;
		}

		void UpdateInputScope()
		{
			Entry entry = Element;
			if (entry.Keyboard is CustomKeyboard custom)
			{
				Control.IsTextPredictionEnabled = (custom.Flags & KeyboardFlags.Suggestions) != 0;
				Control.IsSpellCheckEnabled = (custom.Flags & KeyboardFlags.Spellcheck) != 0;
			}
			else
			{
				if (entry.IsSet(Entry.IsTextPredictionEnabledProperty))
					Control.IsTextPredictionEnabled = entry.IsTextPredictionEnabled;
				else
					Control.ClearValue(TextBox.IsTextPredictionEnabledProperty);
				if (entry.IsSet(InputView.IsSpellCheckEnabledProperty))
					Control.IsSpellCheckEnabled = entry.IsSpellCheckEnabled;
				else
					Control.ClearValue(TextBox.IsSpellCheckEnabledProperty);
			}

			Control.InputScope = entry.Keyboard.ToInputScope();
		}

		void UpdateIsPassword()
		{
			Control.IsPassword = Element.IsPassword;
		}

		void UpdatePlaceholder()
		{
			Control.PlaceholderText = Element.Placeholder ?? "";
		}

		void UpdatePlaceholderColor()
		{
			Color placeholderColor = Element.PlaceholderColor;

			BrushHelpers.UpdateColor(placeholderColor, ref _placeholderDefaultBrush, 
				() => Control.PlaceholderForegroundBrush, brush => Control.PlaceholderForegroundBrush = brush);

			BrushHelpers.UpdateColor(placeholderColor, ref _defaultPlaceholderColorFocusBrush, 
				() => Control.PlaceholderForegroundFocusBrush, brush => Control.PlaceholderForegroundFocusBrush = brush);
		}

		void UpdateText()
		{
			Control.Text = Element.Text ?? "";
		}

		void UpdateTextColor()
		{
			Color textColor = Element.TextColor;

			BrushHelpers.UpdateColor(textColor, ref _textDefaultBrush,
				() => Control.Foreground, brush => Control.Foreground = brush);

			BrushHelpers.UpdateColor(textColor, ref _defaultTextColorFocusBrush,
				() => Control.ForegroundFocusBrush, brush => Control.ForegroundFocusBrush = brush);
		}
    
		void UpdateMaxLength()
		{
			Control.MaxLength = Element.MaxLength;

			var currentControlText = Control.Text;

			if (currentControlText.Length > Element.MaxLength)
				Control.Text = currentControlText.Substring(0, Element.MaxLength);
		}
    
		void UpdateDetectReadingOrderFromContent()
		{
			if (Element.IsSet(Specifics.DetectReadingOrderFromContentProperty))
			{
				if (Element.OnThisPlatform().GetDetectReadingOrderFromContent())
				{
					Control.TextReadingOrder = TextReadingOrder.DetectFromContent;
				}
				else
				{
					Control.TextReadingOrder = TextReadingOrder.UseFlowDirection;
				}
			}
		}

		void UpdateReturnType()
		{
			if (Control == null || Element == null)
				return;

			Control.InputScope = Element.ReturnType.ToInputScope();
		}

		void SelectionChanged(object sender, RoutedEventArgs e)
		{
			var control = Control;
			if (_selectionIsUpdating || control == null || Element == null)
				return;

			int cursorPosition = Element.CursorPosition;

			if (!_cursorPositionChangePending)
			{
				var start = cursorPosition;
				int selectionStart = control.SelectionStart;
				if (selectionStart != start)
				{
					_selectionIsUpdating = true;
					ElementController?.SetValueFromRenderer(Entry.CursorPositionProperty, selectionStart);
				}
			}

			if (!_selectionLengthChangePending)
			{
				int elementSelectionLength = System.Math.Min(control.Text.Length - cursorPosition, Element.SelectionLength);

				int controlSelectionLength = control.SelectionLength;
				if (controlSelectionLength != elementSelectionLength)
				{
					_selectionIsUpdating = true;
					ElementController?.SetValueFromRenderer(Entry.SelectionLengthProperty, controlSelectionLength);
				}
			}

			_selectionIsUpdating = false;
		}

		void UpdateSelectionLength()
		{
			var control = Control;
			if (_selectionIsUpdating || !_selectionLengthChangePending || control == null || Element == null)
				return;

			if (control.Focus(FocusState.Programmatic))
			{
				int selectionLength;
				if (Element.IsSet(Entry.SelectionLengthProperty))
					selectionLength = System.Math.Min(control.Text.Length - Element.CursorPosition, Element.SelectionLength);
				else
					selectionLength = 0;

				if (selectionLength != control.SelectionLength)
				{
					_selectionIsUpdating = true;
					control.SelectionLength = selectionLength;
					_selectionIsUpdating = false;
				}

				_selectionLengthChangePending = false;
			}
		}

		void UpdateCursorPosition()
		{
			var control = Control;
			if (_selectionIsUpdating || !_cursorPositionChangePending || control == null || Element == null )
				return;

			if (control.Focus(FocusState.Programmatic))
			{
				int start;
				if (Element.IsSet(Entry.CursorPositionProperty))
					start = Element.CursorPosition;
				else
					start = control.Text.Length;

				if (start != control.SelectionStart)
				{
					_selectionIsUpdating = true;
					control.SelectionStart = start;
					_selectionIsUpdating = false;

					// Length is dependent on start, so we'll need to update it
					_selectionLengthChangePending = true;
					UpdateSelectionLength();
				}

				_cursorPositionChangePending = false;
			}
		}
	}
}