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

    // Override to hand incoming data to the framer
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

    // Send packet
    public void Send<T>(T obj, MessageType packetId)
    {
        List<byte> bytes = new();
        bytes.AddRange(BitConverter.GetBytes((int)packetId));
        bytes.AddRange(MessagePackSerializer.Serialize(obj));
        Send(_framer.Frame(bytes.ToArray()));
    }
    
    // Send ping
    public void SendPing()
    {
        List<byte> bytes = new();
        bytes.AddRange(BitConverter.GetBytes((int)MessageType.Ping));
        Send(_framer.Frame(bytes.ToArray()));
    }

    // Parse data after framed
    private void FramerReceiveData(byte[] bytes)
    {
        // If less then the size of an int then return, not good data
        if (bytes.Length < sizeof(int))
        {
            return;
        }

        // Get packet id from first bytes
        int packetId = BitConverter.ToInt32(bytes, 0);

        // Make array of data after packet id
        byte[] finalBytes = new byte[bytes.Length - sizeof(int)];
        Array.Copy(bytes, sizeof(int), finalBytes, 0, finalBytes.Length);

        // Send to packet handler
        Program.EventListener?.HandlePacket(this, finalBytes, packetId);
    }
}
// PB end