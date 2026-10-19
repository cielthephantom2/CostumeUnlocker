using System.Net;
using System.Net.Sockets;

namespace LastEpochLAN.Core;

public static class LanNetworks
{
    // Accept direct Internet, LAN and VPN IPv4 peers. Routing/port forwarding
    // is external; a peer must not be rejected for lacking a local subnet match.
    public static bool IsSupported(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address=address.MapToIPv4();
        if (address.AddressFamily!=AddressFamily.InterNetwork) return false;
        var bytes=address.GetAddressBytes();
        return bytes[0]!=0 && bytes[0]<224;
    }

    // Optional subnet diagnostic retained for callers; never an access rule.
    public static bool IsSameSubnet(IPAddress peer, IPAddress local, IPAddress mask)
    {
        if (peer.AddressFamily != AddressFamily.InterNetwork || local.AddressFamily != AddressFamily.InterNetwork ||
            mask.AddressFamily != AddressFamily.InterNetwork) return false;
        var p = peer.GetAddressBytes(); var l = local.GetAddressBytes(); var m = mask.GetAddressBytes();
        if (p[0] == 0 || p[0] >= 224 || l[0] == 0 || l[0] >= 224 || IPAddress.IsLoopback(local)) return false;
        bool sawZero = false, anyOne = false;
        foreach (var part in m)
            for (var bit = 7; bit >= 0; bit--)
                if ((part & (1 << bit)) == 0) sawZero = true;
                else { if (sawZero) return false; anyOne = true; }
        if (!anyOne) return false;
        for (var i = 0; i < 4; i++) if ((p[i] & m[i]) != (l[i] & m[i])) return false;
        return true;
    }
}
