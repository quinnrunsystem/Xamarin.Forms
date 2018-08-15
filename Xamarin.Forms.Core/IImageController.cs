using System;

namespace Xamarin.Forms
{
	public interface IImageController : IViewController
	{
		void SetIsLoading(bool isLoading);
		Aspect Aspect { get; }
		ImageSource Source { get; }
		bool IsOpaque { get; }

		event EventHandler<BindableValueChangedEventArgs> ImageSourceChanged;
		event EventHandler<BindableValueChangedEventArgs> ImageSourceChanging;
		event EventHandler ImageSourcesSourceChanged;

		void RaiseImageSourcePropertyChanged();

		BindableProperty SourceProperty { get; }
		BindableProperty AspectProperty { get; }
		BindableProperty IsOpaqueProperty { get; }
	}
}