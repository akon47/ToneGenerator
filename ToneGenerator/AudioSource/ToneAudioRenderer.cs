using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace ToneGenerator.AudioSource
{
	public class ToneAudioRenderer : NotifyPropertyBase, IDisposable
	{
		static private Dictionary<string, string> friendlyNameCache = new Dictionary<string, string>();
		static public IEnumerable<string> EnumerateDeviceNames()
		{
            using (MMDeviceEnumerator enumerator = new MMDeviceEnumerator())
            {
                int waveOutDevices = WaveOut.DeviceCount;
                for (int waveOutDevice = 0; waveOutDevice < waveOutDevices; waveOutDevice++)
                {
                    WaveOutCapabilities deviceInfo = WaveOut.GetCapabilities(waveOutDevice);
					lock (friendlyNameCache)
					{
						if (friendlyNameCache.ContainsKey(deviceInfo.ProductName))
						{
							yield return friendlyNameCache[deviceInfo.ProductName];
							continue;
						}

						foreach (MMDevice device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
						{
							if (device.State != DeviceState.NotPresent && device.FriendlyName.StartsWith(deviceInfo.ProductName))
							{
								friendlyNameCache.Add(deviceInfo.ProductName, device.FriendlyName);
								yield return device.FriendlyName;
								break;
							}
						}
					}
                }
            }
		}

		private class NotificationClient : NAudio.CoreAudioApi.Interfaces.IMMNotificationClient
		{
			private AutoResetEvent needToReset;
			public NotificationClient(ref AutoResetEvent _needToReset)
			{
				needToReset = _needToReset;
			}

			void NAudio.CoreAudioApi.Interfaces.IMMNotificationClient.OnDeviceStateChanged(string deviceId, DeviceState newState)
			{
			}

			void NAudio.CoreAudioApi.Interfaces.IMMNotificationClient.OnDeviceAdded(string pwstrDeviceId) { }
			void NAudio.CoreAudioApi.Interfaces.IMMNotificationClient.OnDeviceRemoved(string deviceId)
			{
			}

			void NAudio.CoreAudioApi.Interfaces.IMMNotificationClient.OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
			{
				if(flow == DataFlow.Render && role == Role.Console)
				{
					needToReset?.Set();
				}
			}
			void NAudio.CoreAudioApi.Interfaces.IMMNotificationClient.OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
		}

		private string renderDeviceName = "default";
		public string RenderDeviceName
		{
			get => renderDeviceName;
			set => SetProperty(ref renderDeviceName, value);
		}

		private double volume = 0.5d;
		public double Volume
		{
			get => volume;
			set => SetProperty(ref volume, value);
		}

		private double frequency = 1000d;
		public double Frequency 
		{
			get => frequency;
			set => SetProperty(ref frequency, value);
		}

		private bool isMuted = true;
		public bool IsMuted
		{
			get => isMuted;
			set => SetProperty(ref isMuted, value);
		}

		private CircularBuffer circularBuffer;
		private Thread renderWorkerThread, generateWorkerThread;
		private ManualResetEvent needToStop;
		public ToneAudioRenderer()
		{
			needToStop = new ManualResetEvent(false);

			circularBuffer = new CircularBuffer(800 * 2 * 2 * 2);

			generateWorkerThread = new Thread(new ThreadStart(GenerateWorkerThreadHandler)) { Name = "GenerateWorkerThread", IsBackground = true };
			generateWorkerThread.Start();

			renderWorkerThread = new Thread(new ThreadStart(RenderWorkerThreadHandler)) { Name = "RenderWorkerThread", IsBackground = true };
			renderWorkerThread.Start();
		}

		private void GenerateWorkerThreadHandler()
		{
			const int samples = 800;
			const int samplesBytes = samples * 2 * 2;

			double t = 0;
			double tincr = 0, amp = 0;

			double currentFrequency = -1;
			double currentVolume = -1;

			IntPtr audioBuffer = Marshal.AllocHGlobal(samplesBytes);
			while (!needToStop.WaitOne(0, false))
			{
				if (circularBuffer.MaxLength - circularBuffer.Count >= samplesBytes)
				{
					if (this.isMuted)
					{
						circularBuffer.WriteZero(samplesBytes);
					}
					else
					{
						circularBuffer.Write(audioBuffer, 0, samplesBytes);
					}

					if (currentFrequency != this.frequency)
					{
						currentFrequency = this.frequency;
						tincr = 2 * Math.PI * currentFrequency / 48000;
					}
					if(currentVolume != this.volume)
					{
						currentVolume = this.volume;
						amp = 32767 * currentVolume;
					}

					unsafe
					{
						short* q = (short*)audioBuffer.ToPointer();
						for (int j = 0; j < samples; j++)
						{
							int v = (int)(Math.Sin(t) * amp);
							for (int k = 0; k < 2; k++)
								*q++ = (short)v;
							t += tincr;
						}
					}
				}
			}
			Marshal.FreeHGlobal(audioBuffer);
		}

		private void RenderWorkerThreadHandler()
		{
			int framePerBytes = ((48000 / 60) * 4);

			BufferedWaveProvider bufferedWaveProvider = new BufferedWaveProvider(new WaveFormat(48000, 2));
			bufferedWaveProvider.BufferLength = framePerBytes * 2;
			bufferedWaveProvider.DiscardOnBufferOverflow = true;

			using (NAudio.CoreAudioApi.MMDeviceEnumerator enumerator = new MMDeviceEnumerator())
			{
				AutoResetEvent needToReset = new AutoResetEvent(false);
				NotificationClient notificationClient = new NotificationClient(ref needToReset);
				enumerator.RegisterEndpointNotificationCallback(notificationClient);

				byte[] samples = new byte[framePerBytes];

				string currentRenderDeviceName = renderDeviceName;
				while (!needToStop.WaitOne(0, false))
				{
					bufferedWaveProvider.ClearBuffer();

					currentRenderDeviceName = renderDeviceName;

					if (!string.IsNullOrWhiteSpace(currentRenderDeviceName))
					{
						MMDevice mmDevice = null;

						if (currentRenderDeviceName.Equals("default", StringComparison.OrdinalIgnoreCase) && enumerator.HasDefaultAudioEndpoint(DataFlow.Render, Role.Console))
							mmDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
						else
						{
							foreach(var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
							{
								if(currentRenderDeviceName.Contains(device.FriendlyName))
								{
									mmDevice = device;
									break;
								}
								else
								{
									device.Dispose();
								}
							}
						}

						if (mmDevice != null)
						{
							try
							{
								using (WasapiOut waveOut = new WasapiOut(mmDevice, AudioClientShareMode.Shared, true, 1))
								{
									waveOut.Init(bufferedWaveProvider);
									waveOut.Play();

									while (!needToStop.WaitOne(0, false))
									{
										if (circularBuffer?.Count > 0 && bufferedWaveProvider.BufferedBytes <= framePerBytes)
										{
											int count = circularBuffer.Read(samples, 0, framePerBytes);
											bufferedWaveProvider.AddSamples(samples, 0, count);
										}
										else
										{
											if (needToReset.WaitOne(0, false) || needToStop.WaitOne(1) || (currentRenderDeviceName != renderDeviceName))
												break;
										}
									}

									waveOut.Dispose();
								}
							}
							catch { }
							finally
							{
								mmDevice?.Dispose();
							}
						}
						else
						{
							if (needToStop.WaitOne(500))
								break;
						}
					}
					else
					{
						if (needToStop.WaitOne(100))
							break;
						else
							continue;
					}

					if (needToStop.WaitOne(100, false))
						break;
				}

				enumerator.UnregisterEndpointNotificationCallback(notificationClient);
				needToReset?.Dispose();
			}
		}

		public void Dispose()
		{
			if (needToStop != null)
				needToStop.Set();
			if (renderWorkerThread != null)
			{
				if (renderWorkerThread.IsAlive && !renderWorkerThread.Join(1000))
					renderWorkerThread.Abort();
				renderWorkerThread = null;
			}
			if (generateWorkerThread != null)
			{
				if (generateWorkerThread.IsAlive && !generateWorkerThread.Join(1000))
					generateWorkerThread.Abort();
				generateWorkerThread = null;
			}
			if (needToStop != null)
				needToStop.Close();
			needToStop = null;
		}
	}
}
