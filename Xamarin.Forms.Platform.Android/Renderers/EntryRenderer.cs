using System;
using System.Collections.Generic;
using System.ComponentModel;
using Android.Content;
using Android.Content.Res;
using Android.Text;
using Android.Text.Method;
using Android.Util;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using Java.Lang;
using Xamarin.Forms.PlatformConfiguration.AndroidSpecific;

namespace Xamarin.Forms.Platform.Android
{
	public class EntryRenderer : ViewRenderer<Entry, FormsEditText>, ITextWatcher, TextView.IOnEditorActionListener
	{
		TextColorSwitcher _hintColorSwitcher;
		TextColorSwitcher _textColorSwitcher;
		bool _disposed;
		ImeAction _currentInputImeFlag;
		IElementController ElementController => Element as IElementController;

		bool _cursorPositionChangePending = false;
		bool _selectionLengthChangePending = false;
		bool _selectionIsUpdating;
		int? _defaultSelectionStart;
		int? _defaultSelectionEnd;

		public EntryRenderer(Context context) : base(context)
		{
			AutoPackage = false;
		}

		[Obsolete("This constructor is obsolete as of version 2.5. Please use EntryRenderer(Context) instead.")]
		public EntryRenderer()
		{
			AutoPackage = false;
		}

		bool TextView.IOnEditorActionListener.OnEditorAction(TextView v, ImeAction actionId, KeyEvent e)
		{
			// Fire Completed and dismiss keyboard for hardware / physical keyboards
			if (actionId == ImeAction.Done || actionId == _currentInputImeFlag || (actionId == ImeAction.ImeNull && e.KeyCode == Keycode.Enter && e.Action == KeyEventActions.Up))
			{
				Control.ClearFocus();
				v.HideKeyboard();
				((IEntryController)Element).SendCompleted();
			}

			return true;
		}

		void ITextWatcher.AfterTextChanged(IEditable s)
		{
		}

		void ITextWatcher.BeforeTextChanged(ICharSequence s, int start, int count, int after)
		{
		}

		void ITextWatcher.OnTextChanged(ICharSequence s, int start, int before, int count)
		{
			if (string.IsNullOrEmpty(Element.Text) && s.Length() == 0)
				return;

			((IElementController)Element).SetValueFromRenderer(Entry.TextProperty, s.ToString());
		}

		protected override FormsEditText CreateNativeControl()
		{
			return new FormsEditText(Context);
		}

		protected override void OnElementChanged(ElementChangedEventArgs<Entry> e)
		{
			base.OnElementChanged(e);

			HandleKeyboardOnFocus = true;

			if (e.OldElement == null)
			{
				var textView = CreateNativeControl();

				textView.AddTextChangedListener(this);
				textView.SetOnEditorActionListener(this);
				textView.OnKeyboardBackPressed += OnKeyboardBackPressed;
				textView.SelectionChanged += SelectionChanged;

				var useLegacyColorManagement = e.NewElement.UseLegacyColorManagement();

				_textColorSwitcher = new TextColorSwitcher(textView.TextColors, useLegacyColorManagement);
				_hintColorSwitcher = new TextColorSwitcher(textView.HintTextColors, useLegacyColorManagement);
				SetNativeControl(textView);
			}

			// When we set the control text, it triggers the SelectionChanged event, which updates CursorPosition and SelectionLength;
			// These one-time-use variables will let us initialize a CursorPosition and SelectionLength via ctor/xaml/etc.
			_cursorPositionChangePending = Element.IsSet(Entry.CursorPositionProperty);
			_selectionLengthChangePending = Element.IsSet(Entry.SelectionLengthProperty);

			Control.Hint = Element.Placeholder;
			Control.Text = Element.Text;
			UpdateInputType();

			UpdateColor();
			UpdateAlignment();
			UpdateFont();
			UpdatePlaceholderColor();
			UpdateMaxLength();
			UpdateImeOptions();
			UpdateReturnType();
			UpdateCursorSelection();
		}

		protected override void Dispose(bool disposing)
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;

			if (disposing)
			{
				if (Control != null)
				{
					Control.OnKeyboardBackPressed -= OnKeyboardBackPressed;
					Control.SelectionChanged -= SelectionChanged;
				}
			}

			base.Dispose(disposing);
		}

		protected override void OnElementPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == Entry.PlaceholderProperty.PropertyName)
				Control.Hint = Element.Placeholder;
			else if (e.PropertyName == Entry.IsPasswordProperty.PropertyName)
				UpdateInputType();
			else if (e.PropertyName == Entry.TextProperty.PropertyName)
			{
				if (Control.Text != Element.Text)
				{
					Control.Text = Element.Text;
					if (Control.IsFocused)
					{
						Control.SetSelection(Control.Text.Length);
						Control.ShowKeyboard();
					}
				}
			}
			else if (e.PropertyName == Entry.TextColorProperty.PropertyName)
				UpdateColor();
			else if (e.PropertyName == InputView.KeyboardProperty.PropertyName)
				UpdateInputType();
			else if (e.PropertyName == InputView.IsSpellCheckEnabledProperty.PropertyName)
				UpdateInputType();
			else if (e.PropertyName == Entry.IsTextPredictionEnabledProperty.PropertyName)
				UpdateInputType();
			else if (e.PropertyName == Entry.HorizontalTextAlignmentProperty.PropertyName)
				UpdateAlignment();
			else if (e.PropertyName == Entry.FontAttributesProperty.PropertyName)
				UpdateFont();
			else if (e.PropertyName == Entry.FontFamilyProperty.PropertyName)
				UpdateFont();
			else if (e.PropertyName == Entry.FontSizeProperty.PropertyName)
				UpdateFont();
			else if (e.PropertyName == Entry.PlaceholderColorProperty.PropertyName)
				UpdatePlaceholderColor();
			else if (e.PropertyName == VisualElement.FlowDirectionProperty.PropertyName)
				UpdateAlignment();
			else if (e.PropertyName == InputView.MaxLengthProperty.PropertyName)
				UpdateMaxLength();
			else if (e.PropertyName == PlatformConfiguration.AndroidSpecific.Entry.ImeOptionsProperty.PropertyName)
				UpdateImeOptions();
			else if (e.PropertyName == Entry.ReturnTypeProperty.PropertyName)
				UpdateReturnType();
			else if (e.PropertyName == Entry.SelectionLengthProperty.PropertyName)
			{
				_selectionLengthChangePending = true;
				UpdateCursorSelection();
			}
			else if (e.PropertyName == Entry.CursorPositionProperty.PropertyName)
			{
				_cursorPositionChangePending = true;
				UpdateCursorSelection();
			}

			base.OnElementPropertyChanged(sender, e);
		}

		protected virtual NumberKeyListener GetDigitsKeyListener(InputTypes inputTypes)
		{
			// Override this in a custom renderer to use a different NumberKeyListener
			// or to filter out input types you don't want to allow
			// (e.g., inputTypes &= ~InputTypes.NumberFlagSigned to disallow the sign)
			return LocalizedDigitsKeyListener.Create(inputTypes);
		}

		protected virtual void UpdateImeOptions()
		{
			if (Element == null || Control == null)
				return;
			var imeOptions = Element.OnThisPlatform().ImeOptions();
			_currentInputImeFlag = imeOptions.ToAndroidImeOptions();
			Control.ImeOptions = _currentInputImeFlag;
		}

		void UpdateAlignment()
		{
			Control.UpdateHorizontalAlignment(Element.HorizontalTextAlignment, Context.HasRtlSupport());
		}

		void UpdateColor()
		{
			_textColorSwitcher.UpdateTextColor(Control, Element.TextColor);
		}

		void UpdateFont()
		{
			Control.Typeface = Element.ToTypeface();
			Control.SetTextSize(ComplexUnitType.Sp, (float)Element.FontSize);
		}

		void UpdateInputType()
		{
			Entry model = Element;
			var keyboard = model.Keyboard;

			Control.InputType = keyboard.ToInputType();
			if (!(keyboard is Internals.CustomKeyboard))
			{
				if (model.IsSet(InputView.IsSpellCheckEnabledProperty))
				{
					if ((Control.InputType & InputTypes.TextFlagNoSuggestions) != InputTypes.TextFlagNoSuggestions)
					{
						if (!model.IsSpellCheckEnabled)
							Control.InputType = Control.InputType | InputTypes.TextFlagNoSuggestions;
					}
				}
				if (model.IsSet(Entry.IsTextPredictionEnabledProperty))
				{
					if ((Control.InputType & InputTypes.TextFlagNoSuggestions) != InputTypes.TextFlagNoSuggestions)
					{
						if (!model.IsTextPredictionEnabled)
							Control.InputType = Control.InputType | InputTypes.TextFlagNoSuggestions;
					}
				}
			}

			if (keyboard == Keyboard.Numeric)
			{
				Control.KeyListener = GetDigitsKeyListener(Control.InputType);
			}

			if (model.IsPassword && ((Control.InputType & InputTypes.ClassText) == InputTypes.ClassText))
				Control.InputType = Control.InputType | InputTypes.TextVariationPassword;
			if (model.IsPassword && ((Control.InputType & InputTypes.ClassNumber) == InputTypes.ClassNumber))
				Control.InputType = Control.InputType | InputTypes.NumberVariationPassword;
		}

		void UpdatePlaceholderColor()
		{
			_hintColorSwitcher.UpdateTextColor(Control, Element.PlaceholderColor, Control.SetHintTextColor);
		}

		void OnKeyboardBackPressed(object sender, EventArgs eventArgs)
		{
			Control?.ClearFocus();
		}

		void UpdateMaxLength()
		{
			var currentFilters = new List<IInputFilter>(Control?.GetFilters() ?? new IInputFilter[0]);

			for (var i = 0; i < currentFilters.Count; i++)
			{
				if (currentFilters[i] is InputFilterLengthFilter)
				{
					currentFilters.RemoveAt(i);
					break;
				}
			}

			currentFilters.Add(new InputFilterLengthFilter(Element.MaxLength));

			Control?.SetFilters(currentFilters.ToArray());

			var currentControlText = Control?.Text;

			if (currentControlText.Length > Element.MaxLength)
				Control.Text = currentControlText.Substring(0, Element.MaxLength);
		}

		void UpdateReturnType()
		{
			if (Control == null || Element == null)
				return;

			Control.ImeOptions = Element.ReturnType.ToAndroidImeAction();
			_currentInputImeFlag = Control.ImeOptions;
		}

		void SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (_selectionIsUpdating || Control == null || Element == null)
				return;

			int cursorPosition = Element.CursorPosition;

			if (!_cursorPositionChangePending)
			{
				var start = cursorPosition;

				if (Control.SelectionStart != start)
				{
					_selectionIsUpdating = true;
					ElementController?.SetValueFromRenderer(Entry.CursorPositionProperty, Control.SelectionStart);
				}
			}

			if (!_selectionLengthChangePending)
			{
				int elementSelectionLength = System.Math.Min(Control.Text.Length - cursorPosition, Element.SelectionLength);

				var controlSelectionLength = Control.SelectionEnd - Control.SelectionStart;
				if (controlSelectionLength != elementSelectionLength)
				{
					_selectionIsUpdating = true;
					ElementController?.SetValueFromRenderer(Entry.SelectionLengthProperty, controlSelectionLength);
				}
			}
			_selectionIsUpdating = false;
		}

		void UpdateCursorSelection()
		{
			if (_selectionIsUpdating || !(_cursorPositionChangePending || _selectionLengthChangePending) || Control == null || Element == null)
				return;

			int selectionStart = Control.SelectionStart;
			if (!_defaultSelectionStart.HasValue)
				_defaultSelectionStart = selectionStart;

			int selectionEnd = Control.SelectionEnd;
			if (!_defaultSelectionEnd.HasValue)
				_defaultSelectionEnd = selectionEnd;

			int start;
			bool cursorPositionSet = Element.IsSet(Entry.CursorPositionProperty);
			if (cursorPositionSet)
				start = Element.CursorPosition;
			else
				start = (int)_defaultSelectionStart;

			int end;
			bool selectionLengthSet = Element.IsSet(Entry.SelectionLengthProperty);
			if (selectionLengthSet)
				end = System.Math.Min(Control.Length(), start + Element.SelectionLength);
			else
				end = (int)_defaultSelectionEnd;

			// Let's enforce that end is always greater than or equal to start
			end = System.Math.Max(start, end);

			// And if we just cleared both set values and our custom renderer didn't have a Selection, default to end of text
			// Our biggest risk here is that a custom renderer is explicitly setting the start and end position to 0
			if (!cursorPositionSet && !selectionLengthSet && start == 0 && end == 0)
				end = start = Control.Length();

			if (selectionStart != start || selectionEnd != end)
			{
				_selectionIsUpdating = true;
				Control.SetSelection(start, end);
				Control.RequestFocus();
				_selectionIsUpdating = false;
			}

			_cursorPositionChangePending = _selectionLengthChangePending = false;
		}
	}
}