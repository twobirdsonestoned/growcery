using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Growcery.Api.Tests;

// Reads API responses with the same settings the API writes them with (enums as strings).
public static class HttpJsonExtensions
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static async Task<T> ReadJsonAsync<T>(this HttpContent content) =>
        (await content.ReadFromJsonAsync<T>(Options))!;

    public static async Task<T> GetJsonAsync<T>(this HttpClient client, string url) =>
        (await client.GetFromJsonAsync<T>(url, Options))!;
}
