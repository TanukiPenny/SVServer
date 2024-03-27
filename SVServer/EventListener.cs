using MessagePack;
using SVCommon;
using SVCommon.Packet;

namespace SVServer;

public class EventListener : PacketHandler<SvConnection>
{
    public override void OnPing(SvConnection conn)
    {
        conn.LastPingTime = DateTime.Now;
        Console.WriteLine($"Ping received from {conn.Address}");
    }
    
    public override void OnBasicMessage(SvConnection conn, BasicMessage msg)
    {
        Console.WriteLine($"BasicMessage received from {conn.Address}: {msg.Message}");
    }


    public override void OnNewMedia(SvConnection conn, NewMedia newMedia)
    {
        if (conn != Program.State.Host) return;
        Program.State.CurrentMedia = newMedia.Uri;
        foreach (SvConnection connection in Program.ConnectedUsers)
        {
            connection.Send(newMedia, MessageType.NewMedia);
        }
        Console.WriteLine($"BasicMessage received from {conn.Address}: {newMedia.Uri}");
    }

    public override void OnTimeSync(SvConnection conn, TimeSync timeSync)
    {
        if (conn != Program.State.Host) return;
        Program.State.CurrentMediaTime = timeSync.Time;
        foreach (SvConnection connection in Program.ConnectedUsers)
        {
            connection.Send(timeSync, MessageType.TimeSync);
        }
        Console.WriteLine($"TimeSync received from {conn.Address}: {timeSync.Time}");
    }

    public override void OnLogin(SvConnection conn, Login login)
    {
        Console.WriteLine($"Login received from {conn.Address}: {login.Nick}");
        if (conn.IsAuthenticatedSuccessfully)
        {
            return;
        }

        if (Program.ConnectedUsers.Count >= 10)
        {
            Program.DisconnectUser(conn, "Server is full, please try again later!");
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
            Program.State.Host = conn;
            loginResponse.Host = true;
        }
        else
        {
            loginResponse.Host = false;
        }

        conn.FillUserInfo(login.Nick);
        Program.UserAuthed(conn);
        Console.WriteLine($"Login approved from {conn.Address} with nick {login.Nick}");
        loginResponse.Success = true;
        
        conn.Send(loginResponse, MessageType.LoginResponse);

        if (Program.State.CurrentMedia == null) return;
        var newMedia = new NewMedia
        {
            Uri = Program.State.CurrentMedia
        };
        conn.Send(newMedia, MessageType.NewMedia);
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