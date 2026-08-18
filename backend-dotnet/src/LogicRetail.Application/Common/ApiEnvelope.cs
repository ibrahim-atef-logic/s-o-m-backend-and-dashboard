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
    public string? ItemNumber { get; init; }
    public string? SalesId { get; init; }
    public long? ExistingLineRecId { get; init; }
    public decimal? ExistingQuantity { get; init; }
}

public static class ApiEnvelope
{
    public static object Ok(object? data) => new { success = true, data };

    public static object Fail(string code, string message) =>
        Fail(code, message, null, null, null, null);

    public static object Fail(
        string code,
        string message,
        string? itemNumber,
        string? salesId,
        long? existingLineRecId,
        decimal? existingQuantity)
    {
        if (itemNumber is null && salesId is null && existingLineRecId is null && existingQuantity is null)
        {
            return new { success = false, error = new { code, message } };
        }

        return new
        {
            success = false,
            error = new
            {
                code,
                message,
                itemNumber,
                salesId,
                existingLineRecId,
                existingQuantity,
            },
        };
    }

    public static object Fail(AppException ex) =>
        Fail(ex.Code, ex.Message, ex.ItemNumber, ex.SalesId, ex.ExistingLineRecId, ex.ExistingQuantity);
}
