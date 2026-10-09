using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace SettleTrail.Tests;

public sealed class ApiContractTests
{
    [Fact]
    public async Task Http_contract_covers_success_validation_missing_resources_and_conflicts()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"settletrail-api-{Guid.NewGuid():N}.db");
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var configuration = Directory.GetParent(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))!.Name;
        var apiAssembly = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "src", "SettleTrail.Api", "bin", configuration, "net10.0", "SettleTrail.Api.dll"));
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet", $"\"{apiAssembly}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        process.StartInfo.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
        process.StartInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        process.StartInfo.Environment["Logging__LogLevel__Default"] = "Error";
        process.StartInfo.Environment["Logging__LogLevel__Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware"] = "None";
        process.StartInfo.Environment["ConnectionStrings__SettleTrail"] = $"Data Source={databasePath}";
        process.Start();

        try
        {
            using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
            await WaitForHealthAsync(client, process);

            await AssertProblemAsync(await client.PostAsJsonAsync("/accounts", new { name = " " }), 400, "invalid_request");
            await AssertProblemAsync(await client.PostAsJsonAsync("/accounts", new { name = new string('x', 101) }), 400, "invalid_request");
            await AssertProblemAsync(await client.PostAsJsonAsync("/accounts", new { }), 400, "invalid_request");
            await AssertProblemAsync(await client.PostAsync("/accounts", new StringContent("{", Encoding.UTF8, "application/json")), 400, "invalid_request");
            await AssertProblemAsync(await client.GetAsync("/accounts/not-a-guid"), 400, "invalid_request");
            await AssertProblemAsync(await client.GetAsync($"/accounts/{Guid.NewGuid()}"), 404, "resource_not_found");
            await AssertProblemAsync(await client.GetAsync($"/payments/{Guid.NewGuid()}"), 404, "resource_not_found");

            var alice = await CreateAccountAsync(client, " Alice ");
            var bob = await CreateAccountAsync(client, "Bob");
            Assert.Equal("Alice", alice.GetProperty("name").GetString());
            var aliceId = alice.GetProperty("id").GetGuid();
            var bobId = bob.GetProperty("id").GetGuid();

            await AssertProblemAsync(await client.PostAsJsonAsync($"/accounts/{aliceId}/deposits", new { amountMinor = 0, idempotencyKey = "bad" }), 400, "invalid_request");
            await AssertProblemAsync(await client.PostAsJsonAsync($"/accounts/{aliceId}/deposits", new { amountMinor = 100 }), 400, "invalid_request");
            await AssertProblemAsync(await client.PostAsJsonAsync($"/accounts/{Guid.NewGuid()}/deposits", new { amountMinor = 100, idempotencyKey = "missing" }), 404, "resource_not_found");
            await AssertProblemAsync(await client.PostAsJsonAsync("/payments", new { sourceAccountId = Guid.Empty, destinationAccountId = bobId, amountMinor = 100, idempotencyKey = "bad-guid" }), 400, "invalid_request");
            await AssertProblemAsync(await client.PostAsJsonAsync("/payments", new { sourceAccountId = "not-a-guid", destinationAccountId = bobId, amountMinor = 100, idempotencyKey = "bad-guid" }), 400, "invalid_request");

            var deposit = await client.PostAsJsonAsync($"/accounts/{aliceId}/deposits", new { amountMinor = 10_000, idempotencyKey = "deposit-alice-001" });
            Assert.Equal(HttpStatusCode.OK, deposit.StatusCode);
            var depositBody = await ReadJsonAsync(deposit);
            Assert.Equal(10_000, depositBody.GetProperty("amountMinor").GetInt64());
            var retry = await client.PostAsJsonAsync($"/accounts/{aliceId}/deposits", new { amountMinor = 10_000, idempotencyKey = "deposit-alice-001" });
            Assert.Equal(depositBody.GetProperty("id").GetGuid(), (await ReadJsonAsync(retry)).GetProperty("id").GetGuid());
            await AssertProblemAsync(await client.PostAsJsonAsync($"/accounts/{aliceId}/deposits", new { amountMinor = 200, idempotencyKey = "deposit-alice-001" }), 409, "idempotency_key_conflict");

            var transfer = await client.PostAsJsonAsync("/payments", new { sourceAccountId = aliceId, destinationAccountId = bobId, amountMinor = 2_500, idempotencyKey = "transfer-001" });
            Assert.Equal(HttpStatusCode.OK, transfer.StatusCode);
            var paymentId = (await ReadJsonAsync(transfer)).GetProperty("id").GetGuid();
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/payments/{paymentId}")).StatusCode);
            Assert.Equal(7_500, (await ReadJsonAsync(await client.GetAsync($"/accounts/{aliceId}"))).GetProperty("balanceMinor").GetInt64());
            await AssertProblemAsync(await client.PostAsJsonAsync("/payments", new { sourceAccountId = bobId, destinationAccountId = aliceId, amountMinor = 5_000, idempotencyKey = "no-funds" }), 409, "insufficient_funds");

            var document = await ReadJsonAsync(await client.GetAsync("/openapi/v1.json"));
            var paths = document.GetProperty("paths");
            Assert.True(paths.GetProperty("/accounts").GetProperty("post").GetProperty("responses").TryGetProperty("400", out _));
            Assert.True(paths.GetProperty("/payments").GetProperty("post").GetProperty("responses").TryGetProperty("409", out _));
            Assert.True(paths.GetProperty("/payments").GetProperty("post").GetProperty("requestBody").GetProperty("content").TryGetProperty("application/json", out _));
        }
        catch (Exception exception)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            var output = await process.StandardOutput.ReadToEndAsync();
            var errors = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"API contract test failed. API output: {output}\nAPI errors: {errors}", exception);
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            SqliteCleanup(databasePath);
        }
    }

    private static async Task<JsonElement> CreateAccountAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/accounts", new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        return await ReadJsonAsync(response);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, int status, string code)
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await ReadJsonAsync(response);
        Assert.Equal(status, body.GetProperty("status").GetInt32());
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("detail").GetString()));
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static async Task WaitForHealthAsync(HttpClient client, Process process)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (!timeout.IsCancellationRequested)
        {
            if (process.HasExited)
                throw new InvalidOperationException($"API exited: {await process.StandardError.ReadToEndAsync()}");
            try
            {
                if ((await client.GetAsync("/health", timeout.Token)).IsSuccessStatusCode) return;
            }
            catch (HttpRequestException) { }
            await Task.Delay(100, timeout.Token);
        }
        throw new TimeoutException("API did not start.");
    }

    private static void SqliteCleanup(string path)
    {
        foreach (var suffix in new[] { "", "-wal", "-shm" })
            if (File.Exists(path + suffix)) File.Delete(path + suffix);
    }
}
