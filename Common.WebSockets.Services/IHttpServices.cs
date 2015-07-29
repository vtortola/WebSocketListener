namespace Common.WebSockets.Services
{
    public interface IHttpServices
    {
        string UrlDecode(string url);
        string GetStatusDescription(int code);
    }
}