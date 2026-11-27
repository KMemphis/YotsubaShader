using System.Net;
using System.Net.Sockets;

namespace YotsubaShader.Hub;

internal static class PortPicker
{
    public static int Free()
    {
        using var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        return ((IPEndPoint)l.LocalEndpoint).Port;
    }
}
