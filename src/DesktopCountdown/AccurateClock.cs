using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DesktopCountdown
{
    public sealed class AccurateClock : IDisposable
    {
        private readonly object gate = new object();
        private TimeSpan offset = TimeSpan.Zero;
        private string status = "使用系统时间，正在等待网络校准";
        private DateTime? lastSyncUtc;
        private Timer periodicTimer;
        private int syncInProgress;
        private string ntpServer;

        public event EventHandler StatusChanged;

        public AccurateClock(string server)
        {
            ntpServer = string.IsNullOrWhiteSpace(server) ? "time.windows.com" : server.Trim();
        }

        public DateTime UtcNow
        {
            get
            {
                lock (gate) return DateTime.UtcNow + offset;
            }
        }

        public DateTime LocalNow
        {
            get { return UtcNow.ToLocalTime(); }
        }

        public TimeSpan Offset
        {
            get { lock (gate) return offset; }
        }

        public string Status
        {
            get { lock (gate) return status; }
        }

        public DateTime? LastSyncUtc
        {
            get { lock (gate) return lastSyncUtc; }
        }

        public void Start()
        {
            periodicTimer = new Timer(delegate { SyncAsync(); }, null, TimeSpan.FromSeconds(2), TimeSpan.FromHours(6));
        }

        public void ChangeServer(string server)
        {
            string next = string.IsNullOrWhiteSpace(server) ? "time.windows.com" : server.Trim();
            if (!string.Equals(next, ntpServer, StringComparison.OrdinalIgnoreCase))
            {
                ntpServer = next;
                SyncAsync();
            }
        }

        public Task SyncAsync()
        {
            if (Interlocked.Exchange(ref syncInProgress, 1) != 0) return Task.FromResult(false);
            return Task.Run(delegate
            {
                try
                {
                    SetStatus("正在通过 " + ntpServer + " 校准时间");
                    List<NtpSample> samples = new List<NtpSample>();
                    for (int i = 0; i < 3; i++)
                    {
                        try { samples.Add(Query(ntpServer, 1500)); }
                        catch { }
                    }

                    if (samples.Count == 0)
                        throw new InvalidOperationException("未收到时间服务器响应");

                    List<NtpSample> ordered = samples.OrderBy(s => s.Offset.TotalMilliseconds).ToList();
                    NtpSample selected = ordered[ordered.Count / 2];
                    lock (gate)
                    {
                        offset = selected.Offset;
                        lastSyncUtc = DateTime.UtcNow;
                        status = string.Format("已校准：偏差 {0:+0;-0;0} ms，网络往返 {1:0} ms",
                            selected.Offset.TotalMilliseconds, selected.RoundTrip.TotalMilliseconds);
                    }
                    RaiseStatusChanged();
                }
                catch (Exception ex)
                {
                    SetStatus("网络校准暂不可用，继续使用系统时间（" + ex.Message + "）");
                }
                finally
                {
                    Interlocked.Exchange(ref syncInProgress, 0);
                }
            });
        }

        private static NtpSample Query(string host, int timeoutMilliseconds)
        {
            byte[] request = new byte[48];
            request[0] = 0x1B;
            DateTime sentUtc = DateTime.UtcNow;

            using (UdpClient client = new UdpClient())
            {
                client.Client.ReceiveTimeout = timeoutMilliseconds;
                client.Connect(host, 123);
                client.Send(request, request.Length);
                IPEndPoint endpoint = null;
                byte[] response = client.Receive(ref endpoint);
                DateTime receivedUtc = DateTime.UtcNow;
                if (response == null || response.Length < 48)
                    throw new InvalidOperationException("时间服务器返回的数据不完整");

                DateTime serverUtc = ReadTimestamp(response, 40);
                DateTime midpointUtc = sentUtc + TimeSpan.FromTicks((receivedUtc - sentUtc).Ticks / 2);
                return new NtpSample
                {
                    Offset = serverUtc - midpointUtc,
                    RoundTrip = receivedUtc - sentUtc
                };
            }
        }

        private static DateTime ReadTimestamp(byte[] data, int offsetIndex)
        {
            ulong seconds = ((ulong)data[offsetIndex] << 24) |
                            ((ulong)data[offsetIndex + 1] << 16) |
                            ((ulong)data[offsetIndex + 2] << 8) |
                            data[offsetIndex + 3];
            ulong fraction = ((ulong)data[offsetIndex + 4] << 24) |
                             ((ulong)data[offsetIndex + 5] << 16) |
                             ((ulong)data[offsetIndex + 6] << 8) |
                             data[offsetIndex + 7];
            double milliseconds = seconds * 1000.0 + fraction * 1000.0 / 4294967296.0;
            return new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(milliseconds);
        }

        private void SetStatus(string value)
        {
            lock (gate) status = value;
            RaiseStatusChanged();
        }

        private void RaiseStatusChanged()
        {
            EventHandler handler = StatusChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (periodicTimer != null) periodicTimer.Dispose();
        }

        private sealed class NtpSample
        {
            public TimeSpan Offset { get; set; }
            public TimeSpan RoundTrip { get; set; }
        }
    }
}
