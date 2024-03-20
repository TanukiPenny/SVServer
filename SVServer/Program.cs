using SVServer;
using Yggdrasil.Network.TCP;

internal class Program
{
    public static readonly List<SVConnection> ConnectedUsers = new();
    public static readonly List<SVConnection> UnauthedUsers = new();

    public static TcpConnectionAcceptor<SVConnection> TcpConnectionAcceptor;
    public static EventListener EventListener;
    
    public static Thread ServerLoopThread;
    
    public static bool ShutDown = false;
    
    private static DateTime lastTestPingCheck = DateTime.Now;

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
        conn.Closed += (connection, type) => { DisconnectUser(conn); };

        lock (UnauthedUsers)
        {
            UnauthedUsers.Add(conn);
            Console.WriteLine($"User Connected: {conn.Address}");
        }
    }

    public void UserAuthed(SVConnection conn)
    {
        lock (UnauthedUsers)
        {
            UnauthedUsers.Remove(conn);
        }

        conn.IsAuthenticatedSuccessfully = true;

        ConnectedUsers.Add(conn);
    }

    public static void DisconnectUser(SVConnection conn)
    {
        if (conn == null || conn.UserDisconnected) return;

        conn.UserDisconnected = true;


        if (conn.Status == ConnectionStatus.Open)
        {
            conn.Close();
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

    private static void ServerLoop()
    {
        while (!ShutDown)
        {
            if (DateTime.Now.Subtract(lastTestPingCheck).TotalMilliseconds >= 5000)
            {
                foreach (SVConnection connection in UnauthedUsers)
                {
                    connection.SendPing();
                }
                Console.WriteLine("Ping Sent!");
                lastTestPingCheck = DateTime.Now;
            }
        }
    }
}