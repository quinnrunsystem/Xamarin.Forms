using System;
using System.ComponentModel;

using System.Drawing;
using CoreGraphics;
using Foundation;
using UIKit;
using Xamarin.Forms.PlatformConfiguration.iOSSpecific;
using Specifics = Xamarin.Forms.PlatformConfiguration.iOSSpecific.Entry;

namespace Xamarin.Forms.Platform.iOS
{
	public class EntryRenderer : ViewRenderer<Entry, UITextField>
	{
		UIColor _defaultTextColor;

		// Placeholder default color is 70% gray
		// https://developer.apple.com/library/prerelease/ios/documentation/UIKit/Reference/UITextField_Class/index.html#//apple_ref/occ/instp/UITextField/placeholder
		readonly Color _defaultPlaceholderColor = ColorExtensions.SeventyPercentGrey.ToColor();
		UIColor _defaultCursorColor;
		bool _useLegacyColorManagement;

		bool _disposed;
		IDisposable _selectedTextRangeObserver;
		bool _selectedTextRangeIsUpdating;

		bool _cursorPositionChangePending = false;
		bool _selectionLengthChangePending = false;

		static readonly int baseHeight = 30;
		static CGSize initialSize = CGSize.Empty;

		public EntryRenderer()
		{
			Frame = new RectangleF(0, 20, 320, 40);
		}

		public override SizeRequest GetDesiredSize(double widthConstraint, double heightConstraint)
		{
			var baseResult = base.GetDesiredSize(widthConstraint, heightConstraint);

			if (Forms.IsiOS11OrNewer)
				return baseResult;

			NSString testString = new NSString("Tj");
			var testSize = testString.GetSizeUsingAttributes(new UIStringAttributes { Font = Control.Font });
			double height = baseHeight + testSize.Height - initialSize.Height;
			height = Math.Round(height);

			return new SizeRequest(new Size(baseResult.Request.Width, height));
		}

		IElementController ElementController => Element as IElementController;

		protected override void Dispose(bool disposing)
		{
			if (_disposed)
				return;

			_disposed = true;

			if (disposing)
			{
				_defaultTextColor = null;

				if (Control != null)
				{
					_defaultCursorColor = Control.TintColor;
					Control.EditingDidBegin -= OnEditingBegan;
					Control.EditingChanged -= OnEditingChanged;
					Control.EditingDidEnd -= OnEditingEnded;
					Control.ShouldChangeCharacters -= ShouldChangeCharacters;
					_selectedTextRangeObserver?.Dispose();
				}
			}

			base.Dispose(disposing);
		}

		protected override void OnElementChanged(ElementChangedEventArgs<Entry> e)
		{
			base.OnElementChanged(e);

			if (e.NewElement == null)
				return;

			if (Control == null)
			{
				var textField = new UITextField(RectangleF.Empty);
				SetNativeControl(textField);

				// Cache the default text color
				_defaultTextColor = textField.TextColor;

				_useLegacyColorManagement = e.NewElement.UseLegacyColorManagement();

				textField.BorderStyle = UITextBorderStyle.RoundedRect;
				textField.ClipsToBounds = true;

				textField.EditingChanged += OnEditingChanged;
				textField.ShouldReturn = OnShouldReturn;

				textField.EditingDidBegin += OnEditingBegan;
				textField.EditingDidEnd += OnEditingEnded;
				textField.ShouldChangeCharacters += ShouldChangeCharacters;
				_selectedTextRangeObserver = textField.AddObserver("selectedTextRange", NSKeyValueObservingOptions.New, UpdateCursorFromControl);
			}

			// When we set the control text, it triggers the UpdateCursorFromControl event, which updates CursorPosition and SelectionLength;
			// These one-time-use variables will let us initialize a CursorPosition and SelectionLength via ctor/xaml/etc.
			_cursorPositionChangePending = Element.IsSet(Entry.CursorPositionProperty);
			_selectionLengthChangePending = Element.IsSet(Entry.SelectionLengthProperty);

			UpdatePlaceholder();
			UpdatePassword();
			UpdateText();
			UpdateColor();
			UpdateFont();
			UpdateKeyboard();
			UpdateAlignment();
			UpdateAdjustsFontSizeToFitWidth();
			UpdateMaxLength();
			UpdateReturnType();
			UpdateCursorSelection();
			UpdateCursorColor();
		}

		protected override void OnElementPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == Entry.PlaceholderProperty.PropertyName || e.PropertyName == Entry.PlaceholderColorProperty.PropertyName)
				UpdatePlaceholder();
			else if (e.PropertyName == Entry.IsPasswordProperty.PropertyName)
				UpdatePassword();
			else if (e.PropertyName == Entry.TextProperty.PropertyName)
				UpdateText();
			else if (e.PropertyName == Entry.TextColorProperty.PropertyName)
				UpdateColor();
			else if (e.PropertyName == Xamarin.Forms.InputView.KeyboardProperty.PropertyName)
				UpdateKeyboard();
			else if (e.PropertyName == Xamarin.Forms.InputView.IsSpellCheckEnabledProperty.PropertyName)
				UpdateKeyboard();
			else if (e.PropertyName == Entry.IsTextPredictionEnabledProperty.PropertyName)
				UpdateKeyboard();
			else if (e.PropertyName == Entry.HorizontalTextAlignmentProperty.PropertyName)
				UpdateAlignment();
			else if (e.PropertyName == Entry.FontAttributesProperty.PropertyName)
				UpdateFont();
			else if (e.PropertyName == Entry.FontFamilyProperty.PropertyName)
				UpdateFont();
			else if (e.PropertyName == Entry.FontSizeProperty.PropertyName)
				UpdateFont();
			else if (e.PropertyName == VisualElement.IsEnabledProperty.PropertyName)
			{
				UpdateColor();
				UpdatePlaceholder();
			}
			else if (e.PropertyName == Specifics.AdjustsFontSizeToFitWidthProperty.PropertyName)
				UpdateAdjustsFontSizeToFitWidth();
			else if (e.PropertyName == VisualElement.FlowDirectionProperty.PropertyName)
				UpdateAlignment();
			else if (e.PropertyName == Xamarin.Forms.InputView.MaxLengthProperty.PropertyName)
				UpdateMaxLength();
			else if (e.PropertyName == Entry.ReturnTypeProperty.PropertyName)
				UpdateReturnType();
			else if (e.PropertyName == Entry.CursorPositionProperty.PropertyName)
			{
				_cursorPositionChangePending = true;
				UpdateCursorSelection();
			}
			else if (e.PropertyName == Entry.SelectionLengthProperty.PropertyName)
			{
				_selectionLengthChangePending = true;
				UpdateCursorSelection();
			}
			else if (e.PropertyName == Specifics.CursorColorProperty.PropertyName)
				UpdateCursorColor();

			base.OnElementPropertyChanged(sender, e);
		}

		void OnEditingBegan(object sender, EventArgs e)
		{
			UpdateCursorSelection();
			ElementController.SetValueFromRenderer(VisualElement.IsFocusedPropertyKey, true);
		}

		void OnEditingChanged(object sender, EventArgs eventArgs)
		{
			ElementController.SetValueFromRenderer(Entry.TextProperty, Control.Text);
			UpdateCursorFromControl(null);
		}

		void OnEditingEnded(object sender, EventArgs e)
		{
			// Typing aid changes don't always raise EditingChanged event
			if (Control.Text != Element.Text)
			{
				ElementController.SetValueFromRenderer(Entry.TextProperty, Control.Text);
			}

			ElementController.SetValueFromRenderer(VisualElement.IsFocusedPropertyKey, false);
		}

		protected virtual bool OnShouldReturn(UITextField view)
		{
			Control.ResignFirstResponder();
			((IEntryController)Element).SendCompleted();
			return false;
		}

		void UpdateAlignment()
		{
			Control.TextAlignment = Element.HorizontalTextAlignment.ToNativeTextAlignment(((IVisualElementController)Element).EffectiveFlowDirection);
		}

		void UpdateColor()
		{
			var textColor = Element.TextColor;

			if (_useLegacyColorManagement)
			{
				Control.TextColor = textColor.IsDefault || !Element.IsEnabled ? _defaultTextColor : textColor.ToUIColor();
			}
			else
			{
				Control.TextColor = textColor.IsDefault ? _defaultTextColor : textColor.ToUIColor();
			}
		}

		void UpdateAdjustsFontSizeToFitWidth()
		{
			Control.AdjustsFontSizeToFitWidth = Element.OnThisPlatform().AdjustsFontSizeToFitWidth();
		}

		void UpdateFont()
		{
			if (initialSize == CGSize.Empty)
			{
				NSString testString = new NSString("Tj");
				initialSize = testString.StringSize(Control.Font);
			}

			Control.Font = Element.ToUIFont();
		}

		void UpdateKeyboard()
		{
			var keyboard = Element.Keyboard;
			Control.ApplyKeyboard(keyboard);
			if (!(keyboard is Internals.CustomKeyboard))
			{
				if (Element.IsSet(Xamarin.Forms.InputView.IsSpellCheckEnabledProperty))
				{
					if (!Element.IsSpellCheckEnabled)
					{
						Control.SpellCheckingType = UITextSpellCheckingType.No;
					}
				}
				if (Element.IsSet(Xamarin.Forms.Entry.IsTextPredictionEnabledProperty))
				{
					if (!Element.IsTextPredictionEnabled)
					{
						Control.AutocorrectionType = UITextAutocorrectionType.No;
					}
				}
			}
			Control.ReloadInputViews();
		}

		void UpdatePassword()
		{
			if (Element.IsPassword && Control.IsFirstResponder)
			{
				Control.Enabled = false;
				Control.SecureTextEntry = true;
				Control.Enabled = Element.IsEnabled;
				Control.BecomeFirstResponder();
			}
			else
				Control.SecureTextEntry = Element.IsPassword;
		}

		void UpdatePlaceholder()
		{
			var formatted = (FormattedString)Element.Placeholder;

			if (formatted == null)
				return;

			var targetColor = Element.PlaceholderColor;

			if (_useLegacyColorManagement)
			{
				var color = targetColor.IsDefault || !Element.IsEnabled ? _defaultPlaceholderColor : targetColor;
				Control.AttributedPlaceholder = formatted.ToAttributed(Element, color);
			}
			else
			{
				// Using VSM color management; take whatever is in Element.PlaceholderColor
				var color = targetColor.IsDefault ? _defaultPlaceholderColor : targetColor;
				Control.AttributedPlaceholder = formatted.ToAttributed(Element, color);
			}
		}

		void UpdateText()
		{
			// ReSharper disable once RedundantCheckBeforeAssignment
			if (Control.Text != Element.Text)
				Control.Text = Element.Text;
		}

		void UpdateMaxLength()
		{
			var currentControlText = Control.Text;

			if (currentControlText.Length > Element.MaxLength)
				Control.Text = currentControlText.Substring(0, Element.MaxLength);
		}

		bool ShouldChangeCharacters(UITextField textField, NSRange range, string replacementString)
		{
			var newLength = textField?.Text?.Length + replacementString.Length - range.Length;
			return newLength <= Element?.MaxLength;
		}

		void UpdateReturnType()
		{
			if (Control == null || Element == null)
				return;
			Control.ReturnKeyType = Element.ReturnType.ToUIReturnKeyType();
		}

		void UpdateCursorFromControl(NSObservedChange obj)
		{
			var control = Control;
			if (_selectedTextRangeIsUpdating || control == null || Element == null)
				return;

			var currentSelection = control.SelectedTextRange;

			if (!_cursorPositionChangePending)
			{
				int newCursorPosition = (int)control.GetOffsetFromPosition(control.BeginningOfDocument, currentSelection.Start);
				if (newCursorPosition != Element.CursorPosition)
				{
					_selectedTextRangeIsUpdating = true;
					ElementController?.SetValueFromRenderer(Entry.CursorPositionProperty, newCursorPosition);
				}
			}

			if (!_selectionLengthChangePending)
			{
				int selectionLength = (int)control.GetOffsetFromPosition(currentSelection.Start, currentSelection.End);

				if (selectionLength != Element.SelectionLength)
				{
					_selectedTextRangeIsUpdating = true;
					ElementController?.SetValueFromRenderer(Entry.SelectionLengthProperty, selectionLength);
				}
			}

			_selectedTextRangeIsUpdating = false;
		}

		void UpdateCursorSelection()
		{
			var control = Control;
			if (_selectedTextRangeIsUpdating || !(_cursorPositionChangePending || _selectionLengthChangePending) || control == null || Element == null)
				return;

			// If this is run from the ctor, the control is likely too early in its lifecycle to be first responder yet. 
			// Anything done here will have no effect, so we'll skip this work until later.
			// We'll try again when the control does become first responder later OnEditingBegan
			if (control.BecomeFirstResponder())
			{
				int cursorPosition = Element.CursorPosition;

				UITextPosition start;
				bool cursorPositionSet = Element.IsSet(Entry.CursorPositionProperty);
				if (cursorPositionSet)
					start = control.GetPosition(control.BeginningOfDocument, cursorPosition);
				else
					start = control.EndOfDocument;

				int startOffset = (int)control.GetOffsetFromPosition(control.BeginningOfDocument, start);

				UITextPosition end;
				bool selectionLengthSet = Element.IsSet(Entry.SelectionLengthProperty);
				if (selectionLengthSet)
					end = control.GetPosition(start, Math.Max(startOffset, Math.Min(control.Text.Length - cursorPosition, Element.SelectionLength))) ?? start;
				else
					end = start;

				int endOffset = (int)control.GetOffsetFromPosition(control.BeginningOfDocument, end);

				// Let's enforce that end is always greater than or equal to start
				if (endOffset < startOffset)
					end = start;

				var currentSelection = control.SelectedTextRange;
				if (currentSelection.Start != start || currentSelection.End != end)
				{
					_selectedTextRangeIsUpdating = true;
					control.SelectedTextRange = control.GetTextRange(start, end);
					_selectedTextRangeIsUpdating = false;
				}

				_cursorPositionChangePending = _selectionLengthChangePending = false;
			}
		}

		void UpdateCursorColor()
		{
			var control = Control;
			if (control == null || Element == null)
				return;

			if (Element.IsSet(Specifics.CursorColorProperty))
			{
				var color = Element.OnThisPlatform().GetCursorColor();
				if (color == Color.Default)
					control.TintColor = _defaultCursorColor;
				else
					control.TintColor = color.ToUIColor();
			}
		}
	}
}
