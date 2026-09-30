using Microsoft.AspNetCore.Components;

namespace FuncPulse.Web.Components;

public partial class App : ComponentBase
{
    [Inject]
    private IConfiguration Configuration { get; set; } = default!;

    private string BaseHref
    {
        get
        {
            var pathBase = Configuration["PathBase"];
            if (string.IsNullOrWhiteSpace(pathBase))
            {
                return "/";
            }
            
            return pathBase.EndsWith('/') ? pathBase : pathBase + "/";
        }
    }
}
