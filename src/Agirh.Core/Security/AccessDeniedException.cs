namespace Agirh.Core.Security;

public sealed class AccessDeniedException : Exception
{
    public AccessDeniedException(string message) : base(message)
    {
    }
}
