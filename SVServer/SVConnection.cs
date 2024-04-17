// PB start
using MessagePack;
using SVCommon;
using Yggdrasil.Network.Framing;
using Yggdrasil.Network.TCP;

namespace SVServer;

public class SvConnection : TcpConnection
{
    public string? Nick;
    public DateTime ConnectionOpened = DateTime.Now;
    public bool UserDisconnected;
    public bool IsAuthenticatedSuccessfully;
    public DateTime LastPingTime = DateTime.Now;

    private readonly LengthPrefixFramer _framer = new(20000);

    protected override void ReceiveData(byte[] buffer, int length)
    {
        if (UserDisconnected) return;
        _framer.ReceiveData(buffer, length);
    }

    public SvConnection()
    {
        _framer.MessageReceived += FramerReceiveData;
    }

    public void FillUserInfo(string? nick)
    {
        Nick = nick;
    }

    public void Send<T>(T obj, MessageType packetId)
    {
        List<byte> bytes = new();
        bytes.AddRange(BitConverter.GetBytes((int)packetId));
        bytes.AddRange(MessagePackSerializer.Serialize(obj));
        Send(_framer.Frame(bytes.ToArray()));
    }
    
    public void SendPing()
    {
        List<byte> bytes = new();
        bytes.AddRange(BitConverter.GetBytes((int)MessageType.Ping));
        Send(_framer.Frame(bytes.ToArray()));
    }

    private void FramerReceiveData(byte[] bytes)
    {
        if (bytes.Length < sizeof(int))
        {
            return;
        }

        int packetId = BitConverter.ToInt32(bytes, 0);

        byte[] finalBytes = new byte[bytes.Length - sizeof(int)];

        Array.Copy(bytes, sizeof(int), finalBytes, 0, finalBytes.Length);

        Program.EventListener?.HandlePacket(this, finalBytes, packetId);
    }
}
// PB end