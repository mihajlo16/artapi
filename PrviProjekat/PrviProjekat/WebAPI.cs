using System.Net;
using System.Text;

namespace PrviProjekat;

internal static class WebAPI
{
    private static readonly HttpListener listener = new();
    private static readonly Cache cache = new();
    private static readonly HttpClient httpClient = new();

    private const string BaseApiUrl = "https://api.artic.edu/api/v1/artworks/search?q=";
    private const string InternalApiUrl = "http://localhost";
    private const int InternalApiPort = 5080;

    static WebAPI()
    {
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
        httpClient.DefaultRequestHeaders.Add("AIC-User-Agent", "MyChicagoApp (me@domain.com)");
        httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    public static void Start()
    {
        var apiUrl = $"{InternalApiUrl}:{InternalApiPort}/";

        listener.Prefixes.Add(apiUrl);
        listener.Start();

        Logger.Info($"Server started on {apiUrl}");
        Logger.Info("Press ENTER to stop.");

        var listenerThread = new Thread(Listen)
        {
            IsBackground = true,
            Name = "HttpListenerThread"
        };
        listenerThread.Start();
    }

    private static void Listen()
    {
        try
        {
            while (listener.IsListening)
            {
                var context = listener.GetContext();
                ThreadPool.QueueUserWorkItem(_ => HandleRequest(context));
            }
        }
        catch (HttpListenerException)
        {
            Logger.Info("Server stopped.");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error in listener: {ex.Message}");
        }
    }

    public static void Stop()
    {
        try
        {
            if (listener.IsListening)
            {
                listener.Stop();
                listener.Close();
            }

            Logger.Info("Server successfully stopped.");
            Logger.Stop();
        }
        catch (Exception ex)
        {
            Logger.Error($"Error while stopping server: {ex.Message}");
        }
    }

    private static void HandleRequest(HttpListenerContext context)
    {
        try
        {
            var request = context.Request;

            if (request.HttpMethod == "OPTIONS")
            {
                AddCorsHeaders(context.Response);
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.Close();
                return;
            }

            if (request.HttpMethod != "GET")
            {
                ReturnText(context, "Only GET method is supported.", HttpStatusCode.MethodNotAllowed);
                return;
            }

            if (request.RawUrl == "/favicon.ico")
            {
                context.Response.StatusCode = (int)HttpStatusCode.NoContent;
                context.Response.Close();
                return;
            }

            string? author = request.QueryString["author"];
            if (string.IsNullOrWhiteSpace(author))
            {
                ReturnText(context, "Query parameter 'author' is missing.", HttpStatusCode.BadRequest);
                return;
            }

            string cacheKey = $"author={author}".ToLower();
            string apiUrl = $"{BaseApiUrl}{author}";

            string? response = cache.GetOrAdd(cacheKey, () =>
            {
                Logger.Request($"Calling API: {apiUrl}");
                string apiResponse = httpClient.GetStringAsync(apiUrl).GetAwaiter().GetResult();

                if (string.IsNullOrWhiteSpace(apiResponse))
                    return "Empty API response.";

                if (apiResponse.Contains("\"data\":[]"))
                    return $"No artworks found for author '{author}'.";

                return apiResponse;
            });

            if (string.IsNullOrWhiteSpace(response) || response.StartsWith("Empty API response"))
                ReturnText(context, response ?? "Empty API response", HttpStatusCode.BadRequest);
            else if (response.StartsWith("No artworks"))
                ReturnText(context, response, HttpStatusCode.NoContent);
            else
                ReturnJson(context, response, HttpStatusCode.OK);
        }
        catch (Exception ex)
        {
            Logger.Error($"Error handling request: {ex.Message}");
            ReturnText(context, $"Server error: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    private static void ReturnJson(HttpListenerContext context, string json, HttpStatusCode status)
    {
        byte[] buffer = Encoding.UTF8.GetBytes(json);
        var response = context.Response;
        response.ContentType = "application/json; charset=utf-8";
        response.StatusCode = (int)status;
        AddCorsHeaders(response);

        using var output = response.OutputStream;
        output.Write(buffer, 0, buffer.Length);

        Logger.Request($"Request completed: {context.Request.RawUrl}, Status: {status}");
    }

    private static void ReturnText(HttpListenerContext context, string text, HttpStatusCode status)
    {
        byte[] buffer = Encoding.UTF8.GetBytes(text);
        var response = context.Response;
        response.ContentType = "text/plain; charset=utf-8";
        response.StatusCode = (int)status;
        AddCorsHeaders(response);

        using var output = response.OutputStream;
        output.Write(buffer, 0, buffer.Length);

        Logger.Info($"Response {status}: {text}");
    }

    private static void AddCorsHeaders(HttpListenerResponse response)
    {
        response.AddHeader("Access-Control-Allow-Origin", "*");
        response.AddHeader("Access-Control-Allow-Methods", "GET, OPTIONS");
        response.AddHeader("Access-Control-Allow-Headers", "Content-Type");
    }
}