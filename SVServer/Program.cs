using SVServer;
using Yggdrasil.Network.TCP;

internal class Program
{
    public static List<SVConnection> ConnectedUsers = new();
    public static List<SVConnection> UnauthedUsers = new();

    public static TcpConnectionAcceptor<SVConnection> TcpConnectionAcceptor;
    public static EventListener EventListener;

    public static Thread ServerLoopThread;

    public static bool ShutDown = false;

    private static DateTime _lastUnauthUserCheck = DateTime.Now;


    public static void Main(string[] args)
    {
        EventListener = new EventListener();

        TcpConnectionAcceptor = new TcpConnectionAcceptor<SVConnection>("127.0.0.1", 9052);
        TcpConnectionAcceptor.ConnectionAccepted += AddConnection;
        TcpConnectionAcceptor.Listen();

        ServerLoopThread = new Thread(ServerLoop);
        ServerLoopThread.Start();
    }

    public static void AddConnection(SVConnection conn)
    {
        conn.Closed += (_, _) => { DisconnectUser(conn); };

        lock (UnauthedUsers)
        {
            UnauthedUsers.Add(conn);
            Console.WriteLine($"User Connected: {conn.Address}");
        }
    }

    public static void DisconnectUser(SVConnection conn, string message = null)
    {
        conn.UserDisconnected = true;

        if (message != null)
        {
            // Send DisconnectMessage
        }

        if (conn.IsAuthenticatedSuccessfully)
        {
            ConnectedUsers.Remove(conn);
        }
        else
        {
            lock (UnauthedUsers)
            {
                UnauthedUsers.Remove(conn);
            }
        }
    }

    public static void UserAuthed(SVConnection conn)
    {
        lock (UnauthedUsers)
        {
            UnauthedUsers.Remove(conn);
        }

        conn.IsAuthenticatedSuccessfully = true;

        foreach (SVConnection connection in ConnectedUsers)
        {
            // connection.Send();
        }
        
        ConnectedUsers.Add(conn);
    }

    private static void ServerLoop()
    {
        while (!ShutDown)
        {
            if (DateTime.Now.Subtract(_lastUnauthUserCheck).TotalMilliseconds >= 10000)
            {
                lock (UnauthedUsers)
                {
                    SVConnection[] usersToCheck = UnauthedUsers.ToArray();
                    
                    foreach (SVConnection conn in usersToCheck)
                    {
                        if (DateTime.Now.Subtract(conn.ConnectionOpened).TotalSeconds <= 10)
                            continue;

                        Console.WriteLine($"Kicked user from {conn.Address} for not sending login within 10 seconds");

                        DisconnectUser(conn, "Login timeout reached!");
                    }
                }
            }

            Thread.Sleep(900);
        }
    }
}