using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DesktopCountdown
{
    public sealed class AccurateClock : IDisposable
    {
        private static readonly DateTime NtpEpoch = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan MaximumOffset = TimeSpan.FromDays(1);
        private readonly object gate = new object();
        private readonly ClockSource source;
        private TimeSpan offset = TimeSpan.Zero;
        private DateTime calibratedUtc;
        private long calibratedStamp;
        private bool calibrated;
        private string status = "使用系统时间，正在等待网络校准";
        private DateTime? lastSyncUtc;
        private Timer periodicTimer;
        private int syncInProgress;
        private string ntpServer;

        public event EventHandler StatusChanged;

        public AccurateClock(string server)
            : this(server, new ClockSource())
        {
        }

        internal AccurateClock(string server, ClockSource source)
        {
            if (source == null) throw new ArgumentNullException("source");
            this.source = source;
            ntpServer = string.IsNullOrWhiteSpace(server) ? "time.windows.com" : server.Trim();
        }

        public DateTime UtcNow
        {
            get
            {
                lock (gate)
                {
                    if (!calibrated) return source.UtcNow();
                    return calibratedUtc + Elapsed(calibratedStamp, source.Timestamp());
                }
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
                try { SyncCore(); }
                finally { Interlocked.Exchange(ref syncInProgress, 0); }
            });
        }

        private void SyncCore()
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
                    throw new InvalidOperationException("未收到有效的时间服务器响应");

                List<NtpSample> ordered = samples.OrderBy(s => s.Offset.TotalMilliseconds).ToList();
                NtpSample selected = ordered[ordered.Count / 2];
                long nowStamp = source.Timestamp();
                DateTime nowUtc = selected.CorrectedUtc + Elapsed(selected.ReceivedStamp, nowStamp);
                lock (gate)
                {
                    offset = selected.Offset;
                    calibratedUtc = nowUtc;
                    calibratedStamp = nowStamp;
                    calibrated = true;
                    lastSyncUtc = nowUtc;
                    status = string.Format("已校准：偏差 {0:+0;-0;0} ms，网络往返 {1:0} ms",
                        selected.Offset.TotalMilliseconds, selected.RoundTrip.TotalMilliseconds);
                }
                RaiseStatusChanged();
            }
            catch (Exception ex)
            {
                bool hasCalibration;
                lock (gate) hasCalibration = calibrated;
                SetStatus("网络校准暂不可用，继续使用" + (hasCalibration ? "上次校准时间" : "系统时间") +
                    "（" + ex.Message + "）");
            }
        }

        private NtpSample Query(string host, int timeoutMilliseconds)
        {
            byte[] request = new byte[48];
            request[0] = 0x1B;
            DateTime sentUtc = source.UtcNow();
            long sentStamp = source.Timestamp();
            WriteTimestamp(request, 40, sentUtc);
            byte[] response = source.Exchange(host, timeoutMilliseconds, request);
            long receivedStamp = source.Timestamp();
            TimeSpan elapsed = Elapsed(sentStamp, receivedStamp);
            if (elapsed > TimeSpan.FromSeconds(10))
                throw new InvalidOperationException("时间服务器响应过期");
            ValidateResponse(request, response);

            DateTime serverReceiveUtc = ReadTimestamp(response, 32);
            DateTime serverTransmitUtc = ReadTimestamp(response, 40);
            if (serverTransmitUtc < serverReceiveUtc)
                throw new InvalidOperationException("时间服务器时间戳顺序无效");
            DateTime receivedUtc = sentUtc + elapsed;
            TimeSpan measuredOffset = TimeSpan.FromTicks(
                ((serverReceiveUtc - sentUtc).Ticks + (serverTransmitUtc - receivedUtc).Ticks) / 2);
            if (measuredOffset.Duration() > MaximumOffset)
                throw new InvalidOperationException("时间服务器偏差超出安全范围");
            TimeSpan roundTrip = elapsed - (serverTransmitUtc - serverReceiveUtc);
            if (roundTrip < TimeSpan.FromMilliseconds(-5))
                throw new InvalidOperationException("时间服务器往返时间无效");
            if (roundTrip < TimeSpan.Zero) roundTrip = TimeSpan.Zero;
            return new NtpSample
            {
                Offset = measuredOffset,
                RoundTrip = roundTrip,
                CorrectedUtc = receivedUtc + measuredOffset,
                ReceivedStamp = receivedStamp
            };
        }

        private TimeSpan Elapsed(long start, long end)
        {
            if (end < start) throw new InvalidOperationException("单调时钟发生倒退");
            return TimeSpan.FromSeconds((end - start) / (double)source.Frequency);
        }

        private static void ValidateResponse(byte[] request, byte[] response)
        {
            if (response == null || response.Length < 48)
                throw new InvalidOperationException("时间服务器返回的数据不完整");
            int leap = response[0] >> 6;
            int version = (response[0] >> 3) & 7;
            int mode = response[0] & 7;
            if (leap == 3 || (version != 3 && version != 4) || mode != 4 ||
                response[1] == 0 || response[1] > 15)
                throw new InvalidOperationException("时间服务器状态无效");
            for (int i = 0; i < 8; i++)
                if (response[24 + i] != request[40 + i])
                    throw new InvalidOperationException("时间服务器响应与请求不匹配");
            if (TimestampIsZero(response, 32) || TimestampIsZero(response, 40))
                throw new InvalidOperationException("时间服务器时间戳无效");
        }

        private static bool TimestampIsZero(byte[] data, int index)
        {
            for (int i = 0; i < 8; i++)
                if (data[index + i] != 0) return false;
            return true;
        }

        private static void WriteTimestamp(byte[] data, int index, DateTime utc)
        {
            long ticks = (utc - NtpEpoch).Ticks;
            if (ticks < 0) throw new InvalidOperationException("系统时间早于 NTP 起点");
            ulong seconds = (ulong)(ticks / TimeSpan.TicksPerSecond);
            ulong fraction = (ulong)(ticks % TimeSpan.TicksPerSecond) * 4294967296UL /
                (ulong)TimeSpan.TicksPerSecond;
            for (int i = 3; i >= 0; i--)
            {
                data[index + i] = (byte)(seconds & 0xFF);
                seconds >>= 8;
                data[index + 4 + i] = (byte)(fraction & 0xFF);
                fraction >>= 8;
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
            long fractionalTicks = (long)(fraction * (ulong)TimeSpan.TicksPerSecond / 4294967296UL);
            return NtpEpoch.AddSeconds(seconds).AddTicks(fractionalTicks);
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
            public DateTime CorrectedUtc { get; set; }
            public long ReceivedStamp { get; set; }
        }
    }
}
