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

        var loginResponse = new LoginResponse();
        
        if (Program.ConnectedUsers.Exists(c => c.Nick == login.Nick))
        {
            loginResponse.Success = false;
            conn.Send(loginResponse, MessageType.LoginResponse);
            Program.DisconnectUser(conn, "User already exists!");
            Console.WriteLine($"{login.Nick} from {conn.Address} tried to connect with taken nick!");
            return;
        }

        conn.FillUserInfo(login.Nick);
        Program.UserAuthed(conn);
        Console.WriteLine($"Login approved from {conn.Address} with nick {login.Nick}");
        loginResponse.Success = true;
        conn.Send(loginResponse, MessageType.LoginResponse);

        if (Program.ConnectedUsers.Count == 0)
        {
            conn.IsHost = true;
        }
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