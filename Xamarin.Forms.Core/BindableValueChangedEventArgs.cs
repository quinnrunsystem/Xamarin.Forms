using System;
using System.Collections.Generic;
using System.Text;

namespace Xamarin.Forms
{
	public class BindableValueChangedEventArgs : EventArgs
	{
		public BindableValueChangedEventArgs()
		{

		}

		public BindableValueChangedEventArgs(object owner, object oldValue, object newValue)
		{
			Owner = owner;
			OldValue = oldValue;
			NewValue = newValue;
		}

		public object Owner { get; }
		public object OldValue { get; }
		public object NewValue { get; }
	}
}
