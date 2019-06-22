using System;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;

namespace TestAPP
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        public System.Drawing.Bitmap Argb32BytesToBitmap(byte[] buffer, int width, int height)
        {
            if (buffer.Length != width * height * 4)
                return null;

            var bitmap = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            var bmpData = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, bitmap.PixelFormat);
            Marshal.Copy(buffer, 0, bmpData.Scan0, buffer.Length);
            bitmap.UnlockBits(bmpData);

            return bitmap;
        }

        public BitmapSource BitmapToBitmapSource(System.Drawing.Bitmap bitmap)
        {
            var hBitmap = bitmap.GetHbitmap();
            BitmapSource result = null;

            try
            {
                result = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            }
            catch (Exception ex)
            {

            }
            finally
            {
                //Release resource
                Win32.DeleteObject(hBitmap);
            }

            return result;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ThreadPool.QueueUserWorkItem(s =>
            {
                int width;
                int height;

                var buffers = CaptureToBuffer(out width,out height);
                if(buffers!=null)
                {
                    using (var bitmap = Argb32BytesToBitmap(buffers, width, height))
                    {
                        image.Dispatcher.Invoke((Action)(() =>
                        {
                            image.Source = BitmapToBitmapSource(bitmap);
                        }));
                    }
                }
            });
        }

        private byte[] CaptureToBuffer(out int width,out int height)
        {
            // # of graphics card adapter
            const int numAdapter = 0;

            // # of output device (i.e. monitor)
            const int numOutput = 0;

            const string outputFileName = "ScreenCapture.bmp";

            // Create DXGI Factory1
            var factory = new Factory1();
            var adapter = factory.GetAdapter1(numAdapter);

            // Create device from Adapter
            var device = new Device(adapter);

            // Get DXGI.Output
            var output = adapter.GetOutput(numOutput);
            var output1 = output.QueryInterface<Output1>();

            // Width/Height of desktop to capture
            width = ((SharpDX.Rectangle)output.Description.DesktopBounds).Width;
            height = ((SharpDX.Rectangle)output.Description.DesktopBounds).Height;

            byte[] buffer = null; 

            // Create Staging texture CPU-accessible
            var textureDesc = new Texture2DDescription
            {
                CpuAccessFlags = CpuAccessFlags.Read,
                BindFlags = BindFlags.None,
                Format = Format.B8G8R8A8_UNorm,
                Width = width,
                Height = height,
                OptionFlags = ResourceOptionFlags.None,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = { Count = 1, Quality = 0 },
                Usage = ResourceUsage.Staging
            };
            var screenTexture = new Texture2D(device, textureDesc);

            // Duplicate the output
            var duplicatedOutput = output1.DuplicateOutput(device);

            bool captureDone = false;
            for (int i = 0; !captureDone; i++)
            {
                try
                {
                    SharpDX.DXGI.Resource screenResource;
                    OutputDuplicateFrameInformation duplicateFrameInformation;

                    // Try to get duplicated frame within given time
                    duplicatedOutput.AcquireNextFrame(10000, out duplicateFrameInformation, out screenResource);

                    if (i > 0)
                    {
                        // copy resource into memory that can be accessed by the CPU
                        using (var screenTexture2D = screenResource.QueryInterface<Texture2D>())
                            device.ImmediateContext.CopyResource(screenTexture2D, screenTexture);

                        // Get the desktop capture texture
                        var mapSource = device.ImmediateContext.MapSubresource(screenTexture, 0, MapMode.Read, MapFlags.None);

                        buffer = new byte[width * height * 4];
                        Marshal.Copy(mapSource.DataPointer, buffer, 0, buffer.Length);

                        device.ImmediateContext.UnmapSubresource(screenTexture, 0); 
                        // Capture done
                        captureDone = true; 
                    }

                    screenResource.Dispose();
                    duplicatedOutput.ReleaseFrame();

                }
                catch (SharpDXException e)
                {
                    if (e.ResultCode.Code != SharpDX.DXGI.ResultCode.WaitTimeout.Result.Code)
                    {
                        throw e;
                    }
                }
            }

            duplicatedOutput.Dispose();
            screenTexture.Dispose();
            output1.Dispose();
            output.Dispose();
            device.Dispose();
            adapter.Dispose();
            factory.Dispose();

            // TODO: We should cleanp up all allocated COM objects here

            return buffer;
        }

        private void CaptureToFile()
        {
            // # of graphics card adapter
            const int numAdapter = 0;

            // # of output device (i.e. monitor)
            const int numOutput = 0;

            const string outputFileName = "ScreenCapture.bmp";

            // Create DXGI Factory1
            var factory = new Factory1();
            var adapter = factory.GetAdapter1(numAdapter);

            // Create device from Adapter
            var device = new Device(adapter);

            // Get DXGI.Output
            var output = adapter.GetOutput(numOutput);
            var output1 = output.QueryInterface<Output1>();

            // Width/Height of desktop to capture
            int width = ((Rectangle)output.Description.DesktopBounds).Width;
            int height = ((Rectangle)output.Description.DesktopBounds).Height; 

            // Create Staging texture CPU-accessible
            var textureDesc = new Texture2DDescription
            {
                CpuAccessFlags = CpuAccessFlags.Read,
                BindFlags = BindFlags.None,
                Format = Format.B8G8R8A8_UNorm,
                Width = width,
                Height = height,
                OptionFlags = ResourceOptionFlags.None,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = { Count = 1, Quality = 0 },
                Usage = ResourceUsage.Staging
            };
            var screenTexture = new Texture2D(device, textureDesc);

            // Duplicate the output
            var duplicatedOutput = output1.DuplicateOutput(device);

            bool captureDone = false;
            for (int i = 0; !captureDone; i++)
            {
                try
                {
                    SharpDX.DXGI.Resource screenResource;
                    OutputDuplicateFrameInformation duplicateFrameInformation;

                    // Try to get duplicated frame within given time
                    duplicatedOutput.AcquireNextFrame(10000, out duplicateFrameInformation, out screenResource);

                    if (i > 0)
                    {
                        // copy resource into memory that can be accessed by the CPU
                        using (var screenTexture2D = screenResource.QueryInterface<Texture2D>())
                            device.ImmediateContext.CopyResource(screenTexture2D, screenTexture);

                        // Get the desktop capture texture
                        var mapSource = device.ImmediateContext.MapSubresource(screenTexture, 0, MapMode.Read, MapFlags.None);

                        // Create Drawing.Bitmap
                        var bitmap = new System.Drawing.Bitmap(width, height, PixelFormat.Format32bppArgb);
                        var boundsRect = new System.Drawing.Rectangle(0, 0, width, height);

                        // Copy pixels from screen capture Texture to GDI bitmap
                        var mapDest = bitmap.LockBits(boundsRect, ImageLockMode.WriteOnly, bitmap.PixelFormat);
                        var sourcePtr = mapSource.DataPointer;
                        var destPtr = mapDest.Scan0;
                        for (int y = 0; y < height; y++)
                        {
                            // Copy a single line 
                            Utilities.CopyMemory(destPtr, sourcePtr, width * 4);

#if NET35
                            if(IntPtr.Size==sizeof(Int64))
                            {
                                sourcePtr = new IntPtr(sourcePtr.ToInt64() + mapSource.RowPitch);
                                destPtr = new IntPtr(destPtr.ToInt64() + mapDest.Stride);
                            }
                            else
                            {
                                sourcePtr = new IntPtr(sourcePtr.ToInt32() + mapSource.RowPitch);
                                destPtr = new IntPtr(destPtr.ToInt32() + mapDest.Stride);
                            }
#else
                            // Advance pointers
                            sourcePtr = IntPtr.Add(sourcePtr, mapSource.RowPitch);
                            destPtr = IntPtr.Add(destPtr, mapDest.Stride);
#endif
                        }

                        // Release source and dest locks
                        bitmap.UnlockBits(mapDest);
                        device.ImmediateContext.UnmapSubresource(screenTexture, 0);

                        // Save the output
                        bitmap.Save(outputFileName);

                        // Capture done
                        captureDone = true;
                    }

                    screenResource.Dispose();
                    duplicatedOutput.ReleaseFrame();

                }
                catch (SharpDXException e)
                {
                    if (e.ResultCode.Code != SharpDX.DXGI.ResultCode.WaitTimeout.Result.Code)
                    {
                        throw e;
                    }
                }
            }

            // Display the texture using system associated viewer
            System.Diagnostics.Process.Start(Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, outputFileName)));

            // TODO: We should cleanp up all allocated COM objects here
        }

       
    }
}
