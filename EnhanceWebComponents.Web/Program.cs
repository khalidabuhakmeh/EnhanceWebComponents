using EnhanceWebComponents.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapRazorPages();

app.Run();