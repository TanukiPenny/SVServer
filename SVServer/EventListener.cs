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
        if (conn.IsAuthenticatedSuccessfully)
        {
            return;
        }
        
        if (Program.ConnectedUsers.Exists(c => c.Nick == login.Nick))
        {
            Program.DisconnectUser(conn, "User already exists!");
            Console.WriteLine($"{login.Nick} from {conn.Address} tried to connect with taken nick!");
            return;
        }

        Program.UserAuthed(conn);
        Console.WriteLine($"Login approved from {conn.Address} with nick {login.Nick}");
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