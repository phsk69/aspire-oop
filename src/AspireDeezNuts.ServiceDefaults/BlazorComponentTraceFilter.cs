using System.Diagnostics;
using OpenTelemetry;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// OpenTelemetry trace processor to filter out noisy Blazor Server component rendering traces
/// while preserving important application traces
/// </summary>
public class BlazorComponentTraceFilter : BaseProcessor<Activity>
{
    private static readonly HashSet<string> FilteredOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        // Blazor Server component rendering operations that generate excessive traces
        "Microsoft.AspNetCore.Components.Server.ComponentHub/OnRenderCompleted",
        "Microsoft.AspNetCore.Components.Server.ComponentHub/OnLocationChanged",
        "Microsoft.AspNetCore.Components.Server.ComponentHub/BeginInvoke",
        "Microsoft.AspNetCore.Components.Server.ComponentHub/EndInvoke",
        
        // Additional noisy Blazor operations
        "Microsoft.AspNetCore.Components.Server.Circuits.RemoteRenderer/UpdateDisplay",
        "Microsoft.AspNetCore.Components.RenderTree.Renderer/ProcessPendingRender",
        "Microsoft.AspNetCore.Components.Rendering.ComponentState/RenderIntoBatch",
        
        // SignalR operations related to Blazor Server that can be noisy
        "Microsoft.AspNetCore.SignalR.Hub.OnConnectedAsync",
        "Microsoft.AspNetCore.SignalR.Hub.OnDisconnectedAsync"
    };

    private static readonly HashSet<string> FilteredActivitySources = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft.AspNetCore.Components.Server",
        "Microsoft.AspNetCore.Components.Rendering",
        "Microsoft.AspNetCore.Components.RenderTree"
    };

    public override void OnStart(Activity activity)
    {
        // Don't process activities we're going to filter out
        if (ShouldFilterActivity(activity))
        {
            activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
            return;
        }

        base.OnStart(activity);
    }

    public override void OnEnd(Activity activity)
    {
        // Don't export filtered activities
        if (ShouldFilterActivity(activity))
        {
            return;
        }

        base.OnEnd(activity);
    }

    private static bool ShouldFilterActivity(Activity activity)
    {
        // Filter by operation name
        if (!string.IsNullOrEmpty(activity.OperationName) &&
            FilteredOperations.Contains(activity.OperationName))
        {
            return true;
        }

        // Filter by activity source name
        if (activity.Source != null &&
            FilteredActivitySources.Contains(activity.Source.Name))
        {
            return true;
        }

        // Filter by display name patterns
        if (!string.IsNullOrEmpty(activity.DisplayName))
        {
            if (activity.DisplayName.Contains("ComponentHub", StringComparison.OrdinalIgnoreCase) ||
                activity.DisplayName.Contains("OnRenderCompleted", StringComparison.OrdinalIgnoreCase) ||
                activity.DisplayName.Contains("Blazor", StringComparison.OrdinalIgnoreCase) &&
                activity.DisplayName.Contains("Render", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Keep all other activities
        return false;
    }
}