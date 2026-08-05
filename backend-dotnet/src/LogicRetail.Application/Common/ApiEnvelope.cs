namespace LogicRetail.Application.Common;

public sealed class AppException : Exception
{
    public AppException(string message, int statusCode, string code)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    public int StatusCode { get; }
    public string Code { get; }
}

public static class ApiEnvelope
{
    public static object Ok(object? data) => new { success = true, data };

    public static object Fail(string code, string message) =>
        new { success = false, error = new { code, message } };
}
