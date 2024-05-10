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