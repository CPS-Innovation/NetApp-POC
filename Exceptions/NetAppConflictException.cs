namespace Cps.S3Spike.Exceptions;

public class NetAppConflictException : Exception
{
    public NetAppConflictException()
        : base("Conflict occurred while accessing NetApp API.")
    {
    }
}