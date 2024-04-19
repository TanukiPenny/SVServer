// PB start
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
        // Keep track of last ping response for each connection
        conn.LastPingTime = DateTime.Now;
        Log.Verbose("Ping received from {connAd}", conn.Address);
    }
    
    public override void OnBasicMessage(SvConnection conn, BasicMessage msg)
    {
        Log.Information("BasicMessage received from {connAd}: {msg}", conn.Address, msg.Message);
    }

    public override void OnChatMessage(SvConnection conn, ChatMessage msg)
    {
        // Forward chat message to everyone
        foreach (SvConnection connection in ConnectedUsers)
        {
            connection.Send(msg, MessageType.ChatMessage);
        }
        
        Log.Information("ChatMessage received from {connAd}", conn.Address);
    }

    public override void OnPause(SvConnection conn)
    {
        // if not host return
        if (Program.State.Host != conn) return;
        
        Program.State.Paused = true;
        
        // Forward to everyone but host
        foreach (SvConnection connection in ConnectedUsers)
        {
            if (Program.State.Host == connection) continue;
            connection.Send(new Pause(), MessageType.Pause);
        }
        
        Log.Information("Pause received from {connAd}", conn.Address);
    }

    public override void OnStop(SvConnection conn)
    {
        // if not host return
        if (Program.State.Host != conn) return;
        
        // Reset state
        Program.State.CurrentMediaTime = null;
        Program.State.Host = null;
        Program.State.CurrentMedia = null;
        Program.State.Paused = null;
        Log.Information("Stop received, state was cleared");
        
        // Forward to everyone but host
        foreach (SvConnection connection in ConnectedUsers)
        {
            if (Program.State.Host == connection) continue;
            connection.Send(new Stop(), MessageType.Stop);
        }
    }

    public override void OnPlay(SvConnection conn, Play play)
    {
        // if not host return
        if (conn != Program.State.Host) return;
        
        Program.State.CurrentMedia = play.Uri;
        Program.State.Paused = false;

        // Forward to everyone but host
        foreach (SvConnection connection in ConnectedUsers)
        {
            if (Program.State.Host == connection) continue;
            connection.Send(play, MessageType.Play);
        }
        
        Log.Information("Play received from {conn}: {playUri}", conn.Address, play.Uri);
    }

    public override void OnTimeSync(SvConnection conn, TimeSync timeSync)
    {
        // if not host return
        if (conn != Program.State.Host) return;
        
        Program.State.CurrentMediaTime = timeSync.Time;
        
        // Forward to everyone but host
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
        
        // Only run for people that are unauthed
        if (conn.IsAuthenticatedSuccessfully)
        {
            return;
        }
        
        // Create response
        var loginResponse = new LoginResponse();

        // Kicked if to many people connected
        if (ConnectedUsers.Count >= 10)
        {
            loginResponse.Success = false;
            loginResponse.Host = false;
            conn.Send(loginResponse, MessageType.LoginResponse);
            Log.Warning("{nick} from {connAd} tried to connect when the server was full!", login.Nick, conn.Address);
            DisconnectUser(conn, "Server is full, please try again later!");
            return;
        }
        
        // kick if nick is too short
        if (login.Nick.Length < 3)
        {
            loginResponse.Success = false;
            loginResponse.Host = false;
            conn.Send(loginResponse, MessageType.LoginResponse);
            Log.Warning("{nick} from {connAd} tried to connect with a nick under 3 characters!", login.Nick, conn.Address);
            DisconnectUser(conn, "Nick to short!");
            return;
        }
        
        // kick if nick already exists
        if (ConnectedUsers.Exists(c => c.Nick == login.Nick))
        {
            loginResponse.Success = false;
            loginResponse.Host = false;
            conn.Send(loginResponse, MessageType.LoginResponse);
            Log.Warning("{nick} from {connAd} tried to connect with taken nick!", login.Nick, conn.Address);
            DisconnectUser(conn, "Nick already exists!");
            return;
        }
        
        // assign host if first connection
        if (ConnectedUsers.Count == 0)
        {
            Program.State.Host = conn;
            loginResponse.Host = true;
        }
        else
        {
            loginResponse.Host = false;
        }

        // and nick to connection
        conn.FillUserInfo(login.Nick);
        
        // Auth user
        UserAuthed(conn);
        
        Log.Information("Login approved from {connAd}: {nick}", conn.Address, login.Nick);
        
        // Send login response
        loginResponse.Success = true;
        conn.Send(loginResponse, MessageType.LoginResponse);
        
        // Send joins of users already connected
        foreach (SvConnection connection in ConnectedUsers)
        {
            if (conn == connection) continue;
            var userJoin = new UserJoin
            {
                Nick = connection.Nick
            };
            conn.Send(userJoin, MessageType.UserJoin);
        }

        // Send host info
        var hostChange = new HostChange
        {
            Nick = Program.State.Host?.Nick
        };
        conn.Send(hostChange, MessageType.HostChange);

        // if media is playing send it
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
// PB end