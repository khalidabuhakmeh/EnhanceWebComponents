using System.Collections.Concurrent;
using System.Text;
using Microsoft.AspNetCore.Html;

namespace EnhanceWebComponents.Services;

public class EnhanceRequestContext
{
    private ConcurrentBag<EnhanceResult> Results { get; }
        = new();

    public void Add(EnhanceResult result) =>
        Results.Add(result);
    
    public IHtmlContent Styles()
    {
        return new HtmlString(
            // lang=html
            $"""
             <style enhanced="✨">
                {GetAllStyles()}
             </style>
             """
        );
    }
    
    private string GetAllStyles()
    {
        var sb = new StringBuilder();
        // we only want the unique styles
        foreach (var result in Results.DistinctBy(x => x.Styles)) 
            sb.AppendLine(result.Styles);
        return sb.ToString();
    }
}