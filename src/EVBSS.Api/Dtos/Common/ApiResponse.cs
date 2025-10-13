namespace EVBSS.Api.Dtos.Common;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Errors { get; set; }
}

public class ApiResponse : ApiResponse<object>
{
}