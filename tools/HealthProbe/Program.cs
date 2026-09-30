using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
try
{
    using var response = await client.GetAsync(args.Length >= 1 ? args[0] : "http://localhost:8080/health");
    if (response.IsSuccessStatusCode && args.Contains("--print"))
        Console.Write(await response.Content.ReadAsStringAsync());
    return response.IsSuccessStatusCode ? 0 : 1;
}
catch (HttpRequestException) { return 1; }
catch (TaskCanceledException) { return 1; }
