namespace TotemBff.Services;

public sealed class LaboratoryApiException : Exception
{
    public LaboratoryApiException(string message) : base(message)
    {
    }
}
