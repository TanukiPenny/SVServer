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

    public override void OnPause(SvConnection conn)
    {
        if (Program.State.Host != conn) return;
        
        Program.State.Paused = false;
        
        foreach (SvConnection connection in ConnectedUsers)
        {
            if (Program.State.Host == connection) continue;
            connection.Send(new Pause(), MessageType.Pause);
        }
    }

    public override void OnStop(SvConnection conn)
    {
        if (Program.State.Host != conn) return;
        
        Program.State.CurrentMediaTime = null;
        Program.State.Host = null;
        Program.State.CurrentMedia = null;
        Program.State.Paused = null;
        Log.Information("Stop received, state was cleared");
        
        foreach (SvConnection connection in ConnectedUsers)
        {
            if (Program.State.Host == connection) continue;
            connection.Send(new Stop(), MessageType.Stop);
        }
    }

    public override void OnPlay(SvConnection conn, Play play)
    {
        if (conn != Program.State.Host) return;
        
        Program.State.CurrentMedia = play.Uri;
        Program.State.Paused = false;

        foreach (SvConnection connection in ConnectedUsers)
        {
            if (Program.State.Host == connection) continue;
            connection.Send(play, MessageType.Play);
        }
        
        Log.Information("NewMedia received from {conn}: {playUri}", conn.Address, play.Uri);
    }

    public override void OnTimeSync(SvConnection conn, TimeSync timeSync)
    {
        if (conn != Program.State.Host) return;
        
        Program.State.CurrentMediaTime = timeSync.Time;
        
        foreach (SvConnection connection in ConnectedUsers)
        {
            if (Program.State.Host == connection) continue;
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
        
        var loginResponse = new LoginResponse();

        if (ConnectedUsers.Count >= 10)
        {
            loginResponse.Success = false;
            loginResponse.Host = false;
            conn.Send(loginResponse, MessageType.LoginResponse);
            Log.Warning("{nick} from {connAd} tried to connect when the server was full!", login.Nick, conn.Address);
            DisconnectUser(conn, "Server is full, please try again later!");
            return;
        }
        
        if (login.Nick.Length < 3)
        {
            loginResponse.Success = false;
            loginResponse.Host = false;
            conn.Send(loginResponse, MessageType.LoginResponse);
            Log.Warning("{nick} from {connAd} tried to connect with a nick under 3 characters!", login.Nick, conn.Address);
            DisconnectUser(conn, "Nick to short!");
            return;
        }
        
        if (ConnectedUsers.Exists(c => c.Nick == login.Nick))
        {
            loginResponse.Success = false;
            loginResponse.Host = false;
            conn.Send(loginResponse, MessageType.LoginResponse);
            Log.Warning("{nick} from {connAd} tried to connect with taken nick!", login.Nick, conn.Address);
            DisconnectUser(conn, "Nick already exists!");
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
        
        foreach (SvConnection connection in ConnectedUsers)
        {
            if (conn == connection) continue;
            var userJoin = new UserJoin
            {
                Nick = connection.Nick
            };
            conn.Send(userJoin, MessageType.UserJoin);
        }

        if (Program.State.CurrentMedia == null) return;
        var play = new Play
        {
            Uri = Program.State.CurrentMedia
        };
        conn.Send(play, MessageType.Play);
        
        if (Program.State.CurrentMediaTime == null) return;
        var timeSync = new TimeSync
        {
            Time = (long)Program.State.CurrentMediaTime
        };
        conn.Send(timeSync, MessageType.TimeSync);
        
        if (Program.State.Paused == null || Program.State.Paused == false) return;
        conn.Send(new Pause(), MessageType.Pause);
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