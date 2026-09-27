using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace DesktopCountdown
{
    internal sealed class ClockSource
    {
        private readonly Func<DateTime> utcNow;
        private readonly Func<long> timestamp;
        private readonly Func<string, int, byte[], byte[]> exchange;

        public ClockSource()
            : this(() => DateTime.UtcNow, () => Stopwatch.GetTimestamp(), Stopwatch.Frequency, ExchangeNtp)
        {
        }

        public ClockSource(Func<DateTime> utcNow, Func<long> timestamp, long frequency,
            Func<string, int, byte[], byte[]> exchange)
        {
            if (utcNow == null) throw new ArgumentNullException("utcNow");
            if (timestamp == null) throw new ArgumentNullException("timestamp");
            if (exchange == null) throw new ArgumentNullException("exchange");
            if (frequency <= 0) throw new ArgumentOutOfRangeException("frequency");
            this.utcNow = utcNow;
            this.timestamp = timestamp;
            this.exchange = exchange;
            Frequency = frequency;
        }

        public long Frequency { get; private set; }
        public DateTime UtcNow() { return utcNow(); }
        public long Timestamp() { return timestamp(); }
        public byte[] Exchange(string host, int timeoutMilliseconds, byte[] request)
        {
            return exchange(host, timeoutMilliseconds, request);
        }

        private static byte[] ExchangeNtp(string host, int timeoutMilliseconds, byte[] request)
        {
            using (UdpClient client = new UdpClient())
            {
                client.Client.ReceiveTimeout = timeoutMilliseconds;
                client.Connect(host, 123);
                client.Send(request, request.Length);
                IPEndPoint endpoint = null;
                return client.Receive(ref endpoint);
            }
        }
    }
}
