using BlazorBootstrap;

namespace AspireDeezNuts.Web.Services;

public interface IToastService
{
    event Action<ToastMessage>? OnShow;
    void ShowSuccess(string message, string? title = null);
    void ShowError(string message, string? title = null);
    void ShowWarning(string message, string? title = null);
    void ShowInfo(string message, string? title = null);
    void Show(ToastMessage message);
}

public class ToastService(ILogger<ToastService> logger) : IToastService
{
    public event Action<ToastMessage>? OnShow;

    public void ShowSuccess(string message, string? title = null)
    {
        Show(new ToastMessage
        {
            Type = ToastType.Success,
            Title = title ?? "Success",
            Message = message,
            AutoHide = true
        });
    }

    public void ShowError(string message, string? title = null)
    {
        Show(new ToastMessage
        {
            Type = ToastType.Danger,
            Title = title ?? "Error",
            Message = message,
            AutoHide = true
        });
    }

    public void ShowWarning(string message, string? title = null)
    {
        Show(new ToastMessage
        {
            Type = ToastType.Warning,
            Title = title ?? "Warning",
            Message = message,
            AutoHide = true
        });
    }

    public void ShowInfo(string message, string? title = null)
    {
        Show(new ToastMessage
        {
            Type = ToastType.Info,
            Title = title ?? "Information",
            Message = message,
            AutoHide = true
        });
    }

    public void Show(ToastMessage message)
    {
        logger.LogDebug("ToastService.Show called: {Type} - {Message}", message.Type, message.Message);
        logger.LogDebug("OnShow event has {SubscriberCount} subscribers", OnShow?.GetInvocationList()?.Length ?? 0);
        OnShow?.Invoke(message);
    }
}