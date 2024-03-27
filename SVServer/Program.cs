﻿using SVCommon;
using SVCommon.Packet;
using Yggdrasil.Network.TCP;

namespace SVServer;

internal static class Program
{
    public static readonly List<SvConnection> ConnectedUsers = new();
    private static readonly List<SvConnection> UnAuthedUsers = new();

    private static TcpConnectionAcceptor<SvConnection> _tcpConnectionAcceptor = new("172.21.0.2", 9052);
    public static EventListener EventListener = new();
    public static State State = new();

    private static Thread? _serverLoopThread;

    private static readonly DateTime LastAuthUserCheck = DateTime.Now;
    private static readonly DateTime LastPingCheck = DateTime.Now;


    public static void Main(string[] args)
    {
        Console.WriteLine("Server Starting");
        
        _tcpConnectionAcceptor.ConnectionAccepted += AddConnection;
        _tcpConnectionAcceptor.AcceptionException += TcpConnectionAcceptorOnAcceptionException;
        _tcpConnectionAcceptor.ConnectionClosed += TcpConnectionAcceptorOnConnectionClosed;
        _tcpConnectionAcceptor.Listen();

        _serverLoopThread = new Thread(ServerLoop);
        _serverLoopThread.Start();
    }

    private static void TcpConnectionAcceptorOnConnectionClosed(SvConnection conn, ConnectionCloseType closeType)
    {
        Console.WriteLine($"Connection with {conn.Address} was {closeType.ToString().ToLower()}");
    }

    private static void TcpConnectionAcceptorOnAcceptionException(Exception obj)
    {
        Console.WriteLine(obj);
    }

    private static void AddConnection(SvConnection conn)
    {
        conn.Closed += (_, _) => { DisconnectUser(conn); };

        lock (UnAuthedUsers)
        {
            UnAuthedUsers.Add(conn);
            Console.WriteLine($"User connected from {conn.Address}");
        }
    }

    public static void DisconnectUser(SvConnection conn, string? message = null)
    {
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
            lock (UnAuthedUsers)
            {
                UnAuthedUsers.Remove(conn);
            }
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
        
        conn.Close();
        Console.WriteLine($"Disconnected user from {conn.Address}");
    }

    public static void UserAuthed(SvConnection conn)
    {
        lock (UnAuthedUsers)
        {
            UnAuthedUsers.Remove(conn);
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
        Console.WriteLine($"{conn.Nick} was authed");
    }

    private static void ServerLoop()
    {
        while (true)
        {
            Console.WriteLine("Doing Loop");
            if (DateTime.Now.Subtract(LastAuthUserCheck).TotalMilliseconds >= 10000)
            {
                lock (UnAuthedUsers)
                {
                    SvConnection[] usersToCheck = UnAuthedUsers.ToArray();
                    
                    foreach (SvConnection conn in usersToCheck)
                    {
                        if (DateTime.Now.Subtract(conn.ConnectionOpened).TotalSeconds <= 10)
                            continue;

                        Console.WriteLine($"Kicked user from {conn.Address} for not sending login within 10 seconds");

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

            Thread.Sleep(1000);
        }
        // ReSharper disable once FunctionNeverReturns
    }
}