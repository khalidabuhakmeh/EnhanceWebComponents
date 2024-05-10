# ASP.NET Core ❤️ Enhance WASM

This repository contains a proof of concept for rendering JavaScript web components server-side using [Enhance WASM](https://enhance.dev/wasm) using ASP.NET Core Razor TagHelpers and the [Extism](https://extism.org) library to communicate with a Wasm plugin.

Let's see how it's used, then work our way backwards.

```razor
@page
@model IndexModel
@{
    ViewData["Title"] = "Home page";
}

<div class="text-center">
    <my-header class="display-4" enhance-ssr>Hello World</my-header>
    <my-header class="display-3" enhance-ssr>Hello Again</my-header>
    <p>Learn about <a href="https://learn.microsoft.com/aspnet/core">
        building Web apps with ASP.NET Core
    </a>.</p>
</div>
```

In this one code block, we're rendering two similar web components. The component definition is defined in `Program.cs`

```c#
builder.Services.AddSingleton(new EnhanceServerSideRenderer(
    // Note: pull these definitions from somewhere else.
    // you could probably read these from a folder in `wwwroot/js` 
    // and register them by convention `name of file` and `contents`.
    webComponentElements: new()
    {
        {
            "my-header",
            // lang=javascript
            """
            function MyHeader({ html }) 
            {
                return html`<style>h1{color:purple;}</style><h1><slot></slot></h1>` 
            }
            """
        }
    }
));

// a "per request" entity to store results so you 
// can then spit out the scoped CSS styles where you need them
builder.Services.AddScoped<EnhanceRequestContext>();
```

We also have a `EnhanceRequestContext` entity, that is used to store the results of any processed element during a request lifetime.

What's processing our elements? Well, a TagHelper of course!

```c#
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace EnhanceWebComponents.Services;

[HtmlTargetElement(Attributes = EnhanceSsrAttribute)]
public class EnhanceTagHelper(EnhanceServerSideRenderer enhanceServerSideRenderer, EnhanceRequestContext enhanceCtx) : TagHelper
{
    private const string EnhanceSsrAttribute = "enhance-ssr"; 
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.Attributes.Clear();
        foreach (var attribute in context.AllAttributes)
        {
            if (attribute.Name == "enhance-ssr")
                continue;
            
            output.Attributes.Add(attribute);
        }
        output.Content = await output.GetChildContentAsync();

        var sb = new StringBuilder();
        await using var stringWriter = new StringWriter(sb);
        output.WriteTo(stringWriter, HtmlEncoder.Default);

        var input = new EnhanceInput(sb.ToString());
        
        var result = enhanceServerSideRenderer.Process(input);
        // remove outer-wrapper
        output.TagName = "";
        output.Content.SetHtmlContent(result.Body);
        
        // any scoped css goes into the current context
        enhanceCtx.Add(result);
    }
}
```
We register this TagHelper in our `_ViewImports.cshtml` so that the Razor processing knows to call this code.

```razor
@using EnhanceWebComponents
@namespace EnhanceWebComponents.Pages
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
@addTagHelper *, EnhanceWebComponents
```

The major functionality comes from the `EnhanceServerSideRenderer` class, which you can see below. You'll notice that it's not a lot of code.

```c#
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
```
The heavy lifting is done using Wasm and the `enhance-ssr.wasm` artifact in the root of our site.

While the TagHelper is a nice approach, I've also written a few tests to show how you might render components without Razor at all.

```c#
using EnhanceWebComponents.Services;
using Xunit.Abstractions;

namespace EnhanceWebComponents.Tests;

public class EnhanceServerSideRendererTests(ITestOutputHelper output)
{
    private EnhanceServerSideRenderer sut = new(
        webComponentElements: new()
        {
            {
                "my-header",
                // lang=javascript
                """
                function MyHeader({ html }) 
                {
                    return html`<style>h1{color:red;}</style><h1><slot></slot></h1>` 
                }
                """
            },
            {
                "my-component-state",
                // lang=javascript
                """
                function MyComponentState({ html, state }) {
                  const { store } = state
                  return html`<span>${ store?.name }</span>`
                }
                """
            }
        }
    );

    [Fact]
    public void Can_process_web_component()
    {
        var input = new EnhanceInput(
            "<my-header>Hello World</my-header>"
        );

        var result = sut.Process(input);
        
        output.WriteLine(result.Body);

        Assert.NotNull(result);
        Assert.Equal("""<my-header enhanced="✨"><h1>Hello World</h1></my-header>""", result.Body);
        Assert.Equal("my-header h1 {\n  color: red;\n}", result.Styles);
    }
    
    [Fact]
    public void Can_process_web_component_with_state()
    {
        var input = new EnhanceInput(
            "<my-component-state></my-component-state>",
            // accessed via state.store.name in JavaScript
            new { name = "Khalid" }
        );

        var result = sut.Process(input);
        
        output.WriteLine(result.Body);

        Assert.NotNull(result);
        Assert.Equal("""<my-component-state enhanced="✨"><span>Khalid</span></my-component-state>""", result.Body);
    }
}
```

And there you have it. We now have server-side rendered web components in ASP.NET Core Razor Pages.

## Notes

- I currently have the web component implementations hard-coded in C#, but these could easily be pulled from `.js` files.
- When serializing inputs and outputs be sure to disable escaping or else your HTML will be escaped in your JSON.
- The `initialState` is somewhat unnecessary, since you can pass many of the same values through attributes, but options are always good.
- In addition to the `enhance-ssr` attribute, there is likely a way to use an `ehance-ssr` tag to wrap sections with multiple components (like a page), and that might result in performance improvements, but this is a spike not a production ready attempt.
- **Extism** requires **runtimes** for each platform (Windows, macOS, Linux). Be sure to install both the SDK and the runtimes to make it work.

## License

MIT License
Copyright (c) 2024 Khalid Abuhakmeh
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.