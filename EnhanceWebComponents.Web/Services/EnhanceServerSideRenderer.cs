using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Extism.Sdk;

namespace EnhanceWebComponents.Services;

public class EnhanceServerSideRenderer(Dictionary<string, string> webComponentElements)
{
    private static readonly byte[] Wasm =
        File.ReadAllBytes("enhance-ssr.wasm");

    private readonly Plugin plugin = new(Wasm, [], withWasi: true);

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public EnhanceResult Process(EnhanceInput input)
    {
        var value = new EnhanceInputWithComponents(input.Markup, webComponentElements, input.InitialState);
        var json = JsonSerializer.Serialize(value, Options);
        var result = plugin.Call("ssr", json);
        
        return result is null
            ? throw new Exception("unable to process web component")
            : JsonSerializer.Deserialize<EnhanceResult>(result, Options)!;
    }
}

public record EnhanceInput(
    string Markup,
    object? InitialState = null
);

internal record EnhanceInputWithComponents(
    string Markup,
    Dictionary<string, string> Elements,
    object? InitialState
);

public record EnhanceResult(
    string Document,
    string Body,
    string Styles
);