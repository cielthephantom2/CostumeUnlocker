using System.Globalization;

namespace LastEpochLAN.Core;

public sealed record ReleaseVersion(Version Number,int? Development) : IComparable<ReleaseVersion>
{
    public static bool TryParse(string? value,out ReleaseVersion version)
    {
        version=null!;
        if(string.IsNullOrEmpty(value) || value.Length>64) return false;
        var parts=value.Split("-dev.",StringSplitOptions.None);
        if(parts.Length is <1 or >2 || parts[0].Split('.').Length!=3 || !Version.TryParse(parts[0],out var number)) return false;
        int? development=null;
        if(parts.Length==2)
        {
            if(!int.TryParse(parts[1],NumberStyles.None,CultureInfo.InvariantCulture,out int build)) return false;
            development=build;
        }
        version=new(number,development);return true;
    }
    public int CompareTo(ReleaseVersion? other)
    {
        if(other==null) return 1;
        int number=Number.CompareTo(other.Number);
        if(number!=0) return number;
        if(Development==other.Development) return 0;
        if(Development==null) return 1;
        if(other.Development==null) return -1;
        return Development.Value.CompareTo(other.Development.Value);
    }
}
