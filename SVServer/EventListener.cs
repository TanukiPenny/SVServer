using MessagePack;
using SVCommon;
using SVCommon.Packet;

namespace SVServer;

public class EventListener : PacketHandler<SvConnection>
{
    public override void OnPing(SvConnection conn)
    {
        conn.LastPingTime = DateTime.Now;
    }
    
    public override void OnBasicMessage(SvConnection conn, BasicMessage msg)
    {
        Console.WriteLine(msg);
    }


    public override void OnNewMedia(SvConnection conn, NewMedia newMedia)
    {
        if (!conn.IsHost) return;
        foreach (SvConnection connection in Program.ConnectedUsers)
        {
            connection.Send(newMedia, MessageType.NewMedia);
        }
    }

    public override void OnTimeSync(SvConnection conn, TimeSync timeSync)
    {
        if (!conn.IsHost) return;
        foreach (SvConnection connection in Program.ConnectedUsers)
        {
            connection.Send(timeSync, MessageType.TimeSync);
        }
    }

    public override void OnLogin(SvConnection conn, Login login)
    {
        if (conn.IsAuthenticatedSuccessfully)
        {
            return;
        }

        var loginResponse = new LoginResponse();
        
        if (Program.ConnectedUsers.Exists(c => c.Nick == login.Nick))
        {
            loginResponse.Success = false;
            loginResponse.Host = false;
            conn.Send(loginResponse, MessageType.LoginResponse);
            Program.DisconnectUser(conn, "User already exists!");
            Console.WriteLine($"{login.Nick} from {conn.Address} tried to connect with taken nick!");
            return;
        }
        
        if (Program.ConnectedUsers.Count == 0)
        {
            conn.IsHost = true;
            loginResponse.Host = true;
        }
        else
        {
            conn.IsHost = false;
            loginResponse.Host = false;
        }

        conn.FillUserInfo(login.Nick);
        Program.UserAuthed(conn);
        Console.WriteLine($"Login approved from {conn.Address} with nick {login.Nick}");
        loginResponse.Success = true;
        
        conn.Send(loginResponse, MessageType.LoginResponse);
    }

    public override void OnSerializationException(MessagePackSerializationException exception, int packetId)
    {
        Console.WriteLine(exception);  
    }

    public override void OnByteLengthMismatch(SvConnection conn, int readBytes, int totalBytes)
    {
        Console.WriteLine($"Byte Length Mismatch - Read: {readBytes}, Total: {totalBytes}");  
    }

    public override void OnPacketHandlerException(Exception exception, int packetId)
    {
        Console.WriteLine(exception);   
    }
}