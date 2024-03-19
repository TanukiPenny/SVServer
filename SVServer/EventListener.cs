using SVCommon;

namespace SVServer;

public class EventListener : PacketHandler<SVConnection>
{
    public override void OnPing(SVConnection conn)
    {
        Console.WriteLine("Ping!");
    }
}