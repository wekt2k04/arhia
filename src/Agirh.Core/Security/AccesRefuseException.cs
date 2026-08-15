namespace Agirh.Core.Security;

public sealed class AccesRefuseException : Exception
{
    public AccesRefuseException(string message) : base(message)
    {
    }
}
