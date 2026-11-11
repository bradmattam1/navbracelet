namespace HapticNav.Core.Geo;

public readonly record struct GeoPoint(double Latitude, double Longitude)
{
    private const double EarthRadiusMeters = 6371000.0;

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
    private static double ToDegrees(double radians) => radians * 180.0 / Math.PI;

    /// <summary>Great-circle distance in metres (haversine).</summary>
    public double DistanceMetersTo(GeoPoint other)
    {
        double dLat = ToRadians(other.Latitude - Latitude);
        double dLon = ToRadians(other.Longitude - Longitude);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(ToRadians(Latitude)) * Math.Cos(ToRadians(other.Latitude)) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1.0, Math.Sqrt(a)));
    }

    /// <summary>Initial bearing from this point to <paramref name="other"/>, degrees clockwise from north [0, 360).</summary>
    public double BearingDegreesTo(GeoPoint other)
    {
        double lat1 = ToRadians(Latitude);
        double lat2 = ToRadians(other.Latitude);
        double dLon = ToRadians(other.Longitude - Longitude);
        double y = Math.Sin(dLon) * Math.Cos(lat2);
        double x = Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);
        return (ToDegrees(Math.Atan2(y, x)) + 360.0) % 360.0;
    }

    /// <summary>Moves this point <paramref name="distanceMeters"/> along <paramref name="bearingDegrees"/>.</summary>
    public GeoPoint Move(double distanceMeters, double bearingDegrees)
    {
        double angular = distanceMeters / EarthRadiusMeters;
        double lat1 = ToRadians(Latitude);
        double lon1 = ToRadians(Longitude);
        double brg = ToRadians(bearingDegrees);
        double lat2 = Math.Asin(Math.Sin(lat1) * Math.Cos(angular) +
                                Math.Cos(lat1) * Math.Sin(angular) * Math.Cos(brg));
        double lon2 = lon1 + Math.Atan2(Math.Sin(brg) * Math.Sin(angular) * Math.Cos(lat1),
                                        Math.Cos(angular) - Math.Sin(lat1) * Math.Sin(lat2));
        return new GeoPoint(ToDegrees(lat2), (ToDegrees(lon2) + 540.0) % 360.0 - 180.0);
    }
}
