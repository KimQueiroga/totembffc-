namespace TotemBff.Services;

public sealed class LaboratoryApiException : Exception
{
    public LaboratoryApiException(string message, int? statusCode = null) : base(message)
    {
        StatusCode = statusCode;
    }

    public int? StatusCode { get; }
}
