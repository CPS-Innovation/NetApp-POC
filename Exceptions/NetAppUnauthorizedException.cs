namespace Cps.S3Spike.Exceptions;

public class NetAppUnauthorizedException : Exception
{
    public NetAppUnauthorizedException()
        : base("Unauthorized access to NetApp API.")
    {
    }
}