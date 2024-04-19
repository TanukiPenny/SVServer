// PB start
namespace SVServer;

// Simple class to hold the state of the server
public class State
{
    public SvConnection? Host;
    public Uri? CurrentMedia;
    public long? CurrentMediaTime;
    public bool? Paused;
}
// PB end