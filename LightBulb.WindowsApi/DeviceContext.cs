﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using LightBulb.WindowsApi.Internal;

namespace LightBulb.WindowsApi
{
    public partial class DeviceContext : IDisposable
    {
        private int _gammaChannelOffset;

        public IntPtr Handle { get; }

        public DeviceContext(IntPtr handle) => Handle = handle;

        ~DeviceContext() => Dispose();

        private void SetGammaRamp(GammaRamp ramp)
        {
            if (!NativeMethods.SetDeviceGammaRamp(Handle, ref ramp))
                Debug.WriteLine("Could not set gamma ramp.");
        }

        public void SetGamma(double redMultiplier, double greenMultiplier, double blueMultiplier)
        {
            var ramp = new GammaRamp
            {
                Red = new ushort[256],
                Green = new ushort[256],
                Blue = new ushort[256]
            };

            // Create linear ramps for each color
            for (var i = 0; i < 256; i++)
            {
                ramp.Red[i] = (ushort) (i * 255 * redMultiplier);
                ramp.Green[i] = (ushort) (i * 255 * greenMultiplier);
                ramp.Blue[i] = (ushort) (i * 255 * blueMultiplier);
            }

            // Some drivers will ignore request to change gamma if the ramp is the same as the last time,
            // even if it was reset somehow in-between (for example, by screen going to sleep).
            // In order to work around this, we add a small random deviation to each ramp to make sure
            // they're always unique, forcing the drivers to refresh the device context every time.
            _gammaChannelOffset = ++_gammaChannelOffset % 5;
            ramp.Red[255] = (ushort) (ramp.Red[255] + _gammaChannelOffset);
            ramp.Green[255] = (ushort) (ramp.Green[255] + _gammaChannelOffset);
            ramp.Blue[255] = (ushort) (ramp.Blue[255] + _gammaChannelOffset);

            SetGammaRamp(ramp);
        }

        public void Dispose()
        {
            // Reset gamma
            SetGamma(1, 1, 1);

            if (!NativeMethods.DeleteDC(Handle))
                Debug.WriteLine("Could not dispose device context.");

            GC.SuppressFinalize(this);
        }
    }

    public partial class DeviceContext
    {
        public static DeviceContext? TryGetFromDeviceName(string deviceName)
        {
            var handle = NativeMethods.CreateDC(deviceName, null, null, IntPtr.Zero);
            return handle != IntPtr.Zero
                ? new DeviceContext(handle)
                : null;
        }

        public static IReadOnlyList<DeviceContext> FromAllMonitors()
        {
            var result = new List<DeviceContext>();

            foreach (var screen in Screen.AllScreens)
            {
                var deviceContext = TryGetFromDeviceName(screen.DeviceName);
                if (deviceContext != null)
                    result.Add(deviceContext);
            }

            return result;
        }
    }
}