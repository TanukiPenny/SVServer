using MessagePack;
using Serilog;
using Serilog.Events;
using SVCommon;
using SVCommon.Packet;
using static SVServer.Program;

namespace SVServer;

public class EventListener : PacketHandler<SvConnection>
{
    public override void OnPing(SvConnection conn)
    {
        conn.LastPingTime = DateTime.Now;
        Log.Verbose("Ping received from {connAd}", conn.Address);
    }
    
    public override void OnBasicMessage(SvConnection conn, BasicMessage msg)
    {
        Log.Information("BasicMessage received from {connAd}: {msg}", conn.Address, msg.Message);
    }


    public override void OnNewMedia(SvConnection conn, NewMedia newMedia)
    {
        if (conn != Program.State.Host) return;
        
        Program.State.CurrentMedia = newMedia.Uri;

        foreach (SvConnection connection in ConnectedUsers)
        {
            if (Program.State.Host == connection) continue;
            connection.Send(newMedia, MessageType.NewMedia);
        }
        
        Log.Information("NewMedia received from {conn}: {newMediaUri}", conn.Address, newMedia.Uri);
    }

    public override void OnTimeSync(SvConnection conn, TimeSync timeSync)
    {
        if (conn != Program.State.Host) return;
        
        Program.State.CurrentMediaTime = timeSync.Time;
        
        foreach (SvConnection connection in ConnectedUsers)
        {
            if (Program.State.Host == conn) continue;
            connection.Send(timeSync, MessageType.TimeSync);
        }
        
        Log.Verbose("TimeSync received from {conn}: {timeSyncTime}", conn.Address, timeSync.Time);
    }

    public override void OnLogin(SvConnection conn, Login login)
    {
        Log.Information("Login received from {connAd}: {nick}", conn.Address, login.Nick);
        if (conn.IsAuthenticatedSuccessfully)
        {
            return;
        }

        if (ConnectedUsers.Count >= 10)
        {
            DisconnectUser(conn, "Server is full, please try again later!");
        }

        var loginResponse = new LoginResponse();
        
        if (ConnectedUsers.Exists(c => c.Nick == login.Nick))
        {
            loginResponse.Success = false;
            loginResponse.Host = false;
            conn.Send(loginResponse, MessageType.LoginResponse);
            Log.Warning("{nick} from {connAd} tried to connect with taken nick!", login.Nick, conn.Address);
            DisconnectUser(conn, "User already exists!");
            return;
        }
        
        if (ConnectedUsers.Count == 0)
        {
            Program.State.Host = conn;
            loginResponse.Host = true;
        }
        else
        {
            loginResponse.Host = false;
        }

        conn.FillUserInfo(login.Nick);
        
        UserAuthed(conn);
        
        Log.Information("Login approved from {connAd}: {nick}", conn.Address, login.Nick);
        
        loginResponse.Success = true;
        conn.Send(loginResponse, MessageType.LoginResponse);

        if (Program.State.CurrentMedia == null) return;
        var newMedia = new NewMedia
        {
            Uri = Program.State.CurrentMedia
        };
        conn.Send(newMedia, MessageType.NewMedia);
        
        if (Program.State.CurrentMediaTime == null) return;
        var timeSync = new TimeSync
        {
            Time = (long)Program.State.CurrentMediaTime
        };
        conn.Send(timeSync, MessageType.TimeSync);
    }

    public override void OnSerializationException(MessagePackSerializationException exception, int packetId)
    {
        Log.Error(exception, "Exception in serialization");
    }

    public override void OnByteLengthMismatch(SvConnection conn, int readBytes, int totalBytes)
    {
        Log.Warning("Byte Length Mismatch - Read: {readBytes}, Total: {totalBytes}", readBytes, totalBytes);
    }

    public override void OnPacketHandlerException(Exception exception, int packetId)
    {
        Log.Error(exception, "Exception in packet handler"); 
    }
}