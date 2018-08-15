using System;
using System.Threading.Tasks;
using Android.Graphics;
using Android.Graphics.Drawables;
using AImageView = Android.Widget.ImageView;

namespace Xamarin.Forms.Platform.Android
{
	internal static class ImageViewExtensions
	{
		// TODO hartez 2017/04/07 09:33:03 Review this again, not sure it's handling the transition from previousImage to 'null' newImage correctly
		public static async Task UpdateBitmap(
			this AImageView imageView,
			IImageController newView,
			IImageController previousView = null,
			ImageSource newImageSource = null,
			ImageSource previousImageSource = null)
		{
			newImageSource = newView?.Source;
			previousImageSource = previousView?.Source;

			if (newImageSource == null || imageView.IsDisposed())
				return;

			if (Device.IsInvokeRequired)
				throw new InvalidOperationException("Image Bitmap must not be updated from background thread");


			if (Equals(previousImageSource, newImageSource))
				return;

			newView?.SetIsLoading(true);

			(imageView as IImageRendererController)?.SkipInvalidate();

			imageView.SetImageResource(global::Android.Resource.Color.Transparent);

			bool setByImageViewHandler = false;
			Bitmap bitmap = null;

			if (newImageSource != null)
			{
				var imageViewHandler = Internals.Registrar.Registered.GetHandlerForObject<IImageViewHandler>(newImageSource);
				if (imageViewHandler != null)
				{
					try
					{
						await imageViewHandler.LoadImageAsync(newImageSource, imageView);
						setByImageViewHandler = true;
					}
					catch (TaskCanceledException)
					{
						newView?.SetIsLoading(false);
					}
				}
				else
				{
					var imageSourceHandler = Internals.Registrar.Registered.GetHandlerForObject<IImageSourceHandler>(newImageSource);
					try
					{
						bitmap = await imageSourceHandler.LoadImageAsync(newImageSource, imageView.Context);
					}
					catch (TaskCanceledException)
					{
						newView?.SetIsLoading(false);
					}
				}
			}

			// Check if the source on the new image has changed since the image was loaded
			if (!Equals(newView?.Source, newImageSource))
			{
				bitmap?.Dispose();
				return;
			}

			if (!setByImageViewHandler && !imageView.IsDisposed())
			{
				if (bitmap == null && newImageSource is FileImageSource)
					imageView.SetImageResource(ResourceManager.GetDrawableByName(((FileImageSource)newImageSource).File));
				else
					imageView.SetImageBitmap(bitmap);
			}

			bitmap?.Dispose();
			newView?.SetIsLoading(false);
			newView?.NativeSizeChanged();

		}
	}
}
