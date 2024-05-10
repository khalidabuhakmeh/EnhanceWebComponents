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