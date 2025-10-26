using System.Net;
using System.Text;

namespace DrugiProjekat;

internal static class WebAPI
{
    private static readonly HttpListener listener = new();
    private static readonly Cache cache = new();
    private static readonly HttpClient httpClient = new();
    private static CancellationTokenSource? _cts;

    private const string BaseApiUrl = "https://api.artic.edu/api/v1/artworks/search?q=";
    private const string InternalApiUrl = "http://localhost";
    private const int InternalApiPort = 5080;

    static WebAPI()
    {
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
        httpClient.DefaultRequestHeaders.Add("AIC-User-Agent", "MyChicagoApp (me@domain.com)");
        httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    public static void StartAsync()
    {
        _cts = new CancellationTokenSource();
        Logger.Start(_cts.Token);

        var apiUrl = $"{InternalApiUrl}:{InternalApiPort}/";
        listener.Prefixes.Add(apiUrl);
        listener.Start();

        Logger.Info($"Server started on {apiUrl}");
        Logger.Info("Press ENTER to stop.");

        _ = ListenAsync(_cts.Token);
    }

    private static async Task ListenAsync(CancellationToken token)
    {
        try
        {
            while (listener.IsListening && !token.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync();
                _ = HandleRequestAsync(context);
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

    public static async Task StopAsync()
    {
        try
        {
            if (_cts is not null && !_cts.IsCancellationRequested)
                _cts.Cancel();

            if (listener.IsListening)
            {
                listener.Stop();
                listener.Close();
            }

            Logger.Info("Server successfully stopped.");
            await Logger.StopAsync();
        }
        catch (Exception ex)
        {
            Logger.Error($"Error while stopping server: {ex.Message}");
        }
    }

    private static async Task HandleRequestAsync(HttpListenerContext context)
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
                await ReturnTextAsync(context, "Only GET method is supported.", HttpStatusCode.MethodNotAllowed);
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
                await ReturnTextAsync(context, "Query parameter 'author' is missing.", HttpStatusCode.BadRequest);
                return;
            }

            string cacheKey = $"author={author}".ToLower();
            string apiUrl = $"{BaseApiUrl}{author}";

            string? response = await cache.GetOrAddAsync(cacheKey, async () =>
            {
                Logger.Request($"Calling API: {apiUrl}");
                string apiResponse = await httpClient.GetStringAsync(apiUrl);

                if (string.IsNullOrWhiteSpace(apiResponse))
                    return "Empty API response.";

                if (apiResponse.Contains("\"data\":[]"))
                    return $"No artworks found for author '{author}'.";

                return apiResponse;
            });

            if (string.IsNullOrWhiteSpace(response) || response.StartsWith("Empty API response", StringComparison.Ordinal))
                await ReturnTextAsync(context, response ?? "Empty API response", HttpStatusCode.BadRequest);
            else if (response.StartsWith("No artworks", StringComparison.Ordinal))
                await ReturnTextAsync(context, response, HttpStatusCode.NoContent);
            else
                await ReturnJsonAsync(context, response, HttpStatusCode.OK);
        }
        catch (Exception ex)
        {
            Logger.Error($"Error handling request: {ex.Message}");
            await ReturnTextAsync(context, $"Server error: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    private static async ValueTask ReturnJsonAsync(HttpListenerContext context, string json, HttpStatusCode status)
    {
        byte[] buffer = Encoding.UTF8.GetBytes(json);
        var response = context.Response;
        response.ContentType = "application/json; charset=utf-8";
        response.StatusCode = (int)status;
        AddCorsHeaders(response);

        await using var output = response.OutputStream;
        await output.WriteAsync(buffer.AsMemory());

        Logger.Request($"Request completed: {context.Request.RawUrl}, Status: {status}");
    }

    private static async ValueTask ReturnTextAsync(HttpListenerContext context, string text, HttpStatusCode status)
    {
        byte[] buffer = Encoding.UTF8.GetBytes(text);
        var response = context.Response;
        response.ContentType = "text/plain; charset=utf-8";
        response.StatusCode = (int)status;
        AddCorsHeaders(response);

        await using var output = response.OutputStream;
        await output.WriteAsync(buffer.AsMemory());

        Logger.Info($"Response {status}: {text}");
    }

    private static void AddCorsHeaders(HttpListenerResponse response)
    {
        response.AddHeader("Access-Control-Allow-Origin", "*");
        response.AddHeader("Access-Control-Allow-Methods", "GET, OPTIONS");
        response.AddHeader("Access-Control-Allow-Headers", "Content-Type");
    }
}