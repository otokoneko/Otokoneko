using Google.Protobuf;
using MessagePack;
using Newtonsoft.Json;
using SuperSocket.ProtoBase;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Otokoneko.Base.Network
{
    public enum ProxyType
    {
        Socks4,
        Socks5,
    }

    [MessagePackObject]
    public class Proxy
    {
        [Key(0)]
        public ProxyType Type { get; set; }
        [Key(1)]
        public string Host { get; set; }
        [Key(2)]
        public ushort Port { get; set; }
    }

    [MessagePackObject]
    public class HostAndPort
    {
        [Key(0)]
        public string Host { get; set; }
        [Key(1)]
        public ushort Port { get; set; }
    }

    [MessagePackObject]
    public class ServerConfig
    {
        [Key(0)]
        public List<HostAndPort> Hosts;
        [Key(1)]
        public string CertificateHash { get; set; }
        [Key(2)]
        public string ServerId { get; set; }
        [Key(3)]
        public string ServerName { get; set; }
        [SerializationConstructor]
        public ServerConfig() { }
        public ServerConfig(string configString)
        {
            var jsonString = Encoding.UTF8.GetString(Convert.FromBase64String(configString));
            var config = JsonConvert.DeserializeObject<ServerConfig>(jsonString);
            Hosts = config.Hosts ?? throw new ArgumentException($"Invalid argument: {nameof(configString)}");
            if (Hosts.Count == 0)
            {
                throw new ArgumentException($"Invalid argument: {nameof(configString)}");
            }
            CertificateHash = config.CertificateHash ?? throw new ArgumentException($"Invalid argument: {nameof(configString)}");
            ServerId = config.CertificateHash ?? throw new ArgumentException($"Invalid argument: {nameof(configString)}");
            ServerName = config.ServerName ?? throw new ArgumentException($"Invalid argument: {nameof(configString)}");
        }
    }

    public class Responses : IAsyncEnumerable<Response>
    {
        private readonly IAsyncEnumerable<object> _objects;
        private readonly Func<object, bool, Response> _serializer;

        public IAsyncEnumerator<Response> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new ResponseEnumerator(_objects.GetAsyncEnumerator(cancellationToken), _serializer);
        }

        public Responses(IAsyncEnumerable<object> objects, Func<object, bool, Response> serializer)
        {
            _objects = objects;
            _serializer = serializer;
        }
    }

    public class ResponseEnumerator : IAsyncEnumerator<Response>
    {
        public async ValueTask DisposeAsync()
        {
            await _dataGenerator.DisposeAsync();
        }

        public async ValueTask<bool> MoveNextAsync()
        {
            if (!_hasInit)
            {
                _hasInit = true;
                if (!await _dataGenerator.MoveNextAsync()) return false;
            }

            if (Current?.Completed == true) return false;
            var data = _dataGenerator.Current;
            var completed = !await _dataGenerator.MoveNextAsync();
            Current = _serializer(data, completed);
            return true;
        }

        public Response Current { get; private set; }

        private readonly IAsyncEnumerator<object> _dataGenerator;
        private readonly Func<object, bool, Response> _serializer;
        private bool _hasInit;

        public ResponseEnumerator(IAsyncEnumerator<object> dataGenerator, Func<object, bool, Response> serializer)
        {
            _dataGenerator = dataGenerator;
            _serializer = serializer;
            _hasInit = false;
        }
    }

    public class FixedHeaderResponseDecoder : FixedHeaderPipelineFilter<Response>
    {
        public FixedHeaderResponseDecoder() : base(4)
        {
        }

        protected override int GetBodyLengthFromHeader(ref ReadOnlySequence<byte> buffer)
        {
            var reader = new SequenceReader<byte>(buffer);
            reader.TryReadBigEndian(out int len);
            return len;
        }
    }

    public class ResponseDecoder : IPackageDecoder<Response>
    {
        public Response Decode(ref ReadOnlySequence<byte> buffer, object context)
        {
            var reader = new SequenceReader<byte>(buffer);
            reader.TryReadBigEndian(out int len);
            return Response.Parser.ParseFrom(buffer.Slice(4, len));
        }
    }

    public class FixedHeaderRequestDecoder : FixedHeaderPipelineFilter<Request>
    {
        public FixedHeaderRequestDecoder() : base(4)
        {
        }

        protected override int GetBodyLengthFromHeader(ref ReadOnlySequence<byte> buffer)
        {
            var reader = new SequenceReader<byte>(buffer);
            reader.TryReadBigEndian(out int len);
            return len;
        }

        protected override Request DecodePackage(ref ReadOnlySequence<byte> buffer)
        {
            var reader = new SequenceReader<byte>(buffer);
            reader.TryReadBigEndian(out int len);
            return Request.Parser.ParseFrom(buffer.Slice(4, len));
        }
    }

    public class MessageEncoder : IPackageEncoder<IMessage>
    {
        public int Encode(IBufferWriter<byte> writer, IMessage pack)
        {
            var len = pack.CalculateSize();
            var bytes = BitConverter.GetBytes(len);
            Array.Reverse(bytes);
            writer.Write(bytes);
            writer.Write(pack.ToByteArray());
            return 4 + len;
        }
    }
}