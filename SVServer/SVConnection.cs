using System.Text;
using MessagePack;
using SVCommon;
using Yggdrasil.Logging;
using Yggdrasil.Network.Framing;
using Yggdrasil.Network.TCP;

namespace SVServer;

public class SVConnection : TcpConnection
{
    public string Nick;
    public DateTime ConnectionOpened = DateTime.Now;
    public bool Host = false;
    public bool UserDisconnected;
    public bool IsAuthenticatedSuccessfully;

    private readonly LengthPrefixFramer _framer = new(20000);

    protected override void ReceiveData(byte[] buffer, int length)
    {
        if (UserDisconnected) return;
        _framer.ReceiveData(buffer, length);
    }

    public SVConnection()
    {
        _framer.MessageReceived += FramerReceiveData;
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

        Program.EventListener.HandlePacket(this, finalBytes, packetId);
    }
}