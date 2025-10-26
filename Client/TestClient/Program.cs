using System.Collections.Concurrent;
using System.Diagnostics;

Console.WriteLine("=== ChicagoArt API Performance Tester ===");
Console.Write("Enter author name: ");
string? author = Console.ReadLine();

if (string.IsNullOrWhiteSpace(author))
{
    Console.WriteLine("Author cannot be empty. Exiting...");
    return;
}

Console.Write("Number of requests to send (default 1000): ");
string? nInput = Console.ReadLine();
if (!int.TryParse(nInput, out int n) || n <= 0)
    n = 1000;

Console.Write("Max parallel requests (default 5): ");
string? pInput = Console.ReadLine();
if (!int.TryParse(pInput, out int parallelism) || parallelism <= 0)
    parallelism = 5;

Console.WriteLine();
Console.WriteLine($"Testing author '{author}' with {n} requests, {parallelism} parallel threads...");
Console.WriteLine("Starting test...\n");

int ok = 0, failed = 0;
ParallelOptions options = new() { MaxDegreeOfParallelism = parallelism };
string query = $"?author={Uri.EscapeDataString(author)}";
Uri baseUri = new("http://localhost:5080/");

ConcurrentDictionary<string, bool> distinctResponses = new();

string projectDir = Directory.GetParent(AppContext.BaseDirectory)!.Parent!.Parent!.Parent!.FullName;
string resultsDir = Path.Combine(projectDir, "Results");
Directory.CreateDirectory(resultsDir);

using HttpClient httpClient = new() { BaseAddress = baseUri };
Stopwatch stopwatch = Stopwatch.StartNew();

await Parallel.ForAsync(0, n, options, async (i, token) =>
{
    try
    {
        var response = await httpClient.GetAsync(query, token);

        if (!response.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)response.StatusCode}");

        var content = await response.Content.ReadAsStringAsync(token);
        if (string.IsNullOrWhiteSpace(content))
            throw new Exception("Empty API response");

        distinctResponses.TryAdd(content, true);
        Interlocked.Increment(ref ok);
    }
    catch
    {
        Interlocked.Increment(ref failed);
    }
});

stopwatch.Stop();

Console.WriteLine("\nSaving results to disk...");

int fileIndex = 1;
foreach (var kvp in distinctResponses.Keys)
{
    string filePath = Path.Combine(resultsDir, $"Result_{fileIndex++}.json");
    await File.WriteAllTextAsync(filePath, kvp);
}

Console.WriteLine();
Console.WriteLine("=== Performance test complete ===");
Console.WriteLine($"Author: {author}");
Console.WriteLine($"Requests sent: {n}");
Console.WriteLine($"Successful: {ok}");
Console.WriteLine($"Failed: {failed}");
Console.WriteLine($"Distinct results: {distinctResponses.Count}");
Console.WriteLine($"Results folder: {resultsDir}");
Console.WriteLine($"Total time: {stopwatch.Elapsed.TotalSeconds:F2} s");
Console.WriteLine($"Average per request: {stopwatch.Elapsed.TotalMilliseconds / n:F2} ms");
Console.WriteLine($"Requests per second: {n / stopwatch.Elapsed.TotalSeconds:F2}");

Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();