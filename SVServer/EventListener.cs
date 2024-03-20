using MessagePack;
using SVCommon;
using SVCommon.Packet;

namespace SVServer;

public class EventListener : PacketHandler<SVConnection>
{
    public override void OnPing(SVConnection conn)
    {
        Console.WriteLine("Ping Received!");
    }
    
    public override void OnBasicMessage(SVConnection conn, BasicMessage msg)
    {
        Console.WriteLine(msg);
    }

    public override void OnLogin(SVConnection conn, Login login)
    {
        throw new NotImplementedException();
    }

    public override void OnSerializationException(MessagePackSerializationException exception, int packetID)
    {
        Console.WriteLine(exception);  
    }

    public override void OnByteLengthMismatch(SVConnection conn, int readBytes, int totalBytes)
    {
        Console.WriteLine($"Byte Length Mismatch - Read: {readBytes}, Total: {totalBytes}");  
    }

    public override void OnPacketHandlerException(Exception exception, int packetID)
    {
        Console.WriteLine(exception);   
    }
}