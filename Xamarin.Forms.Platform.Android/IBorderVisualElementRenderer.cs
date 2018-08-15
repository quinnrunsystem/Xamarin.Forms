using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using AColor = Android.Graphics.Color;

namespace Xamarin.Forms.Platform.Android
{
	public interface IBorderVisualElementRenderer : IVisualElementRenderer
	{
		float ShadowRadius { get; }
		float ShadowDx { get; }
		float ShadowDy { get; }
		AColor ShadowColor { get; }
		bool UseDefaultPadding();
		bool UseDefaultShadow();
		bool IsShadowEnabled();
	}
}