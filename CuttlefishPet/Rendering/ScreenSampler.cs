using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using CuttlefishPet.Interop;

namespace CuttlefishPet.Rendering;

/// <summary>
/// Captures the screen behind a pet for the camouflage skin. The pet's visual is
/// hidden for a couple of frames so it doesn't photograph itself.
/// </summary>
public static class ScreenSampler
{
    public static async Task<BitmapSource?> CaptureBehindAsync(UIElement petVisual, Rect physRect)
    {
        int w = (int)physRect.Width, h = (int)physRect.Height;
        if (w <= 0 || h <= 0) return null;

        petVisual.Visibility = Visibility.Hidden;
        try
        {
            await Task.Delay(60); // let the compositor actually present the hidden state

            using var bmp = new System.Drawing.Bitmap(w, h);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
                g.CopyFromScreen((int)physRect.X, (int)physRect.Y, 0, 0, bmp.Size);

            var hBitmap = bmp.GetHbitmap();
            try
            {
                var src = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                src.Freeze();
                return src;
            }
            finally
            {
                Win32.DeleteObject(hBitmap);
            }
        }
        catch
        {
            return null; // capture is best-effort; camo just won't engage
        }
        finally
        {
            petVisual.Visibility = Visibility.Visible;
        }
    }
}
