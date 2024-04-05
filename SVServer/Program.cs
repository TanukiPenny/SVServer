using Serilog;
using SVCommon;
using SVCommon.Packet;
using Yggdrasil.Network.TCP;

namespace SVServer;

internal static class Program
{
    public static List<SvConnection> ConnectedUsers = new();
    private static List<SvConnection> _unAuthedUsers = new();

    private static TcpConnectionAcceptor<SvConnection> _tcpConnectionAcceptor = new("172.21.0.2", 9052);
    public static EventListener EventListener = new();
    public static State State = new();

    private static Thread? _serverLoopThread;

    private static readonly DateTime LastAuthUserCheck = DateTime.Now;
    private static readonly DateTime LastPingCheck = DateTime.Now;


    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration().WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}").MinimumLevel.Debug().CreateLogger();
        
        Log.Information("Server Started!");
        
        _tcpConnectionAcceptor.ConnectionAccepted += AddConnection;
        _tcpConnectionAcceptor.AcceptionException += TcpConnectionAcceptorOnAcceptionException;
        _tcpConnectionAcceptor.ConnectionClosed += TcpConnectionAcceptorOnConnectionClosed;
        _tcpConnectionAcceptor.Listen();

        _serverLoopThread = new Thread(ServerLoop);
        _serverLoopThread.Start();
    }

    private static void TcpConnectionAcceptorOnConnectionClosed(SvConnection conn, ConnectionCloseType closeType)
    {
        Log.Warning("Connection with {connectionAd} was {closeType}", conn.Address, closeType.ToString().ToLower());
    }

    private static void TcpConnectionAcceptorOnAcceptionException(Exception obj)
    {
        Log.Error(obj, "Exception in Tcp Connection Acceptor");
    }

    private static void AddConnection(SvConnection conn)
    {
        conn.Closed += (_, _) => { DisconnectUser(conn); };

        lock (_unAuthedUsers)
        {
            _unAuthedUsers.Add(conn);
            Log.Information("User connected from {connAd}", conn.Address);
        }
    }

    public static void DisconnectUser(SvConnection conn, string? message = null)
    {
        if (conn.UserDisconnected) return;
        
        conn.UserDisconnected = true;
        
        if (message != null)
        {
            var disconnectMessage = new DisconnectMessage
            {
                Message = message
            };
            conn.Send(disconnectMessage, MessageType.DisconnectMessage);
        }

        if (conn.IsAuthenticatedSuccessfully)
        {
            ConnectedUsers.Remove(conn);
        }
        else
        {
            lock (_unAuthedUsers)
            {
                _unAuthedUsers.Remove(conn);
            }
        }

        if (ConnectedUsers.Count == 0)
        {
            State.CurrentMediaTime = null;
            State.Host = null;
            State.CurrentMedia = null;
            State.Paused = null;
            Log.Information("Last user disconnected, state was cleared");
            return;
        }

        if (conn == State.Host)
        {
            var oldestConnection = ConnectedUsers.OrderBy(connection => connection.ConnectionOpened).First();
            State.Host = oldestConnection;
            var hostChange = new HostChange
            {
                Nick = oldestConnection.Nick!
            };
            foreach (SvConnection connection in ConnectedUsers)
            {
                connection.Send(hostChange, MessageType.HostChange);
            }
        }
        Log.Information("Disconnected user from {connAd}", conn.Address);
        conn.Close();

        var userLeave = new UserLeave
        {
            Nick = conn.Nick
        };
        foreach (SvConnection connection in ConnectedUsers)
        {
            connection.Send(userLeave, MessageType.UserLeave);
        }
    }

    public static void UserAuthed(SvConnection conn)
    {
        lock (_unAuthedUsers)
        {
            _unAuthedUsers.Remove(conn);
        }

        conn.IsAuthenticatedSuccessfully = true;

        var userJoin = new UserJoin
        {
            Nick = conn.Nick
        };
        foreach (SvConnection connection in ConnectedUsers)
        {
            connection.Send(userJoin, MessageType.UserJoin);
        }
        
        ConnectedUsers.Add(conn);
    }

    private static void ServerLoop()
    {
        while (true)
        {
            if (DateTime.Now.Subtract(LastAuthUserCheck).TotalMilliseconds >= 10000)
            {
                lock (_unAuthedUsers)
                {
                    SvConnection[] usersToCheck = _unAuthedUsers.ToArray();
                    
                    foreach (SvConnection conn in usersToCheck)
                    {
                        if (DateTime.Now.Subtract(conn.ConnectionOpened).TotalSeconds <= 10)
                            continue;
                        
                        Log.Information("Kicked user from {connAd} for not sending login within 10 seconds", conn.Address);

                        DisconnectUser(conn, "Login timeout reached!");
                    }
                }
            }
            
            if (DateTime.Now.Subtract(LastPingCheck).TotalMilliseconds >= 10000)
            {
                foreach (SvConnection connection in ConnectedUsers)
                {
                    if (DateTime.Now.Subtract(connection.LastPingTime).TotalMilliseconds >= 30000)
                    {
                        DisconnectUser(connection, "Client stopped responding to pings");
                    }
                    connection.SendPing();
                }
            }
            Log.Verbose("Server loop done");
            Thread.Sleep(1000);
        }
        Log.CloseAndFlush();
        // ReSharper disable once FunctionNeverReturns
    }
}