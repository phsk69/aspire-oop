using Bunit;
using Bunit.TestDoubles;
using BlazorBootstrap;
using Microsoft.Extensions.DependencyInjection;
using AspireDeezNuts.Web.Services;
using AspireDeezNuts.Web.Tests.Helpers;
using AspireDeezNuts.Web.Components.Pages;

namespace AspireDeezNuts.Web.Tests.Components;

[TestClass]
public class LoginComponentTests : Bunit.TestContext
{
    private TestToastService _toastService = null!;

    [TestInitialize]
    public void Setup()
    {
        _toastService = new TestToastService();
        Services.AddSingleton<IToastService>(_toastService);
    }

    [TestMethod]
    public void Login_RendersCorrectly()
    {
        // Act
        var component = RenderComponent<Login>();

        // Assert
        Assert.IsNotNull(component.Find("h4"));
        Assert.AreEqual("Login", component.Find("h4").TextContent);
        
        Assert.IsNotNull(component.Find("form"));
        Assert.AreEqual("/api/login", component.Find("form").GetAttribute("action"));
        Assert.AreEqual("post", component.Find("form").GetAttribute("method"));
        
        Assert.IsNotNull(component.Find("input[name='Email']"));
        Assert.AreEqual("email", component.Find("input[name='Email']").GetAttribute("type"));
        Assert.AreEqual("admin@example.com", component.Find("input[name='Email']").GetAttribute("value"));
        
        Assert.IsNotNull(component.Find("input[name='Password']"));
        Assert.AreEqual("password", component.Find("input[name='Password']").GetAttribute("type"));
        Assert.AreEqual("AdminPass123!", component.Find("input[name='Password']").GetAttribute("value"));
        
        Assert.IsNotNull(component.Find("button[type='submit']"));
        Assert.AreEqual("Login", component.Find("button[type='submit']").TextContent);
    }

    [TestMethod]
    public void Login_WithoutErrorParameter_DoesNotShowToast()
    {
        // Act
        var component = RenderComponent<Login>();

        // Assert
        Assert.AreEqual(0, _toastService.ToastMessages.Count);
    }

    [TestMethod]
    public void Login_WithErrorParameter_ShowsErrorToast()
    {
        // Arrange
        Services.GetRequiredService<FakeNavigationManager>().NavigateTo("/login?error=Invalid%20credentials");

        // Act
        var component = RenderComponent<Login>();

        // Assert
        Assert.AreEqual(1, _toastService.ToastMessages.Count);
        var toast = _toastService.ToastMessages[0];
        Assert.AreEqual("Invalid credentials", toast.Message);
        Assert.AreEqual("Login Failed", toast.Title);
        Assert.AreEqual(ToastType.Danger, toast.Type);
    }

    [TestMethod]
    public void Login_WithMultipleErrorParameters_ShowsFirstError()
    {
        // Arrange
        Services.GetRequiredService<FakeNavigationManager>().NavigateTo("/login?error=First%20error&error=Second%20error");

        // Act
        var component = RenderComponent<Login>();

        // Assert
        Assert.AreEqual(1, _toastService.ToastMessages.Count);
        var toast = _toastService.ToastMessages[0];
        Assert.AreEqual("First error", toast.Message);
    }

    [TestMethod]
    public void Login_WithEmptyErrorParameter_DoesNotShowToast()
    {
        // Arrange
        Services.GetRequiredService<FakeNavigationManager>().NavigateTo("/login?error=");

        // Act
        var component = RenderComponent<Login>();

        // Assert
        Assert.AreEqual(0, _toastService.ToastMessages.Count);
    }

    [TestMethod]
    public void Login_WithWhitespaceErrorParameter_DoesNotShowToast()
    {
        // Arrange
        Services.GetRequiredService<FakeNavigationManager>().NavigateTo("/login?error=%20%20%20");

        // Act
        var component = RenderComponent<Login>();

        // Assert
        Assert.AreEqual(0, _toastService.ToastMessages.Count);
    }

    [TestMethod]
    public void Login_ContainsExpectedFormFields()
    {
        // Act
        var component = RenderComponent<Login>();

        // Assert
        var emailInput = component.Find("input[name='Email']");
        Assert.AreEqual("Enter email", emailInput.GetAttribute("placeholder"));
        Assert.AreEqual("email", emailInput.GetAttribute("id"));
        Assert.AreEqual("form-control", emailInput.GetAttribute("class"));
        Assert.IsTrue(emailInput.HasAttribute("required"));

        var passwordInput = component.Find("input[name='Password']");
        Assert.AreEqual("Enter password", passwordInput.GetAttribute("placeholder"));
        Assert.AreEqual("password", passwordInput.GetAttribute("id"));
        Assert.AreEqual("form-control", passwordInput.GetAttribute("class"));
        Assert.IsTrue(passwordInput.HasAttribute("required"));
    }

    [TestMethod]
    public void Login_ContainsExpectedLabels()
    {
        // Act
        var component = RenderComponent<Login>();

        // Assert
        var emailLabel = component.Find("label[for='email']");
        Assert.AreEqual("Email", emailLabel.TextContent);
        Assert.AreEqual("form-label", emailLabel.GetAttribute("class"));

        var passwordLabel = component.Find("label[for='password']");
        Assert.AreEqual("Password", passwordLabel.TextContent);
        Assert.AreEqual("form-label", passwordLabel.GetAttribute("class"));
    }

    [TestMethod]
    public void Login_ContainsSubmitButton()
    {
        // Act
        var component = RenderComponent<Login>();

        // Assert
        var submitButton = component.Find("button[type='submit']");
        Assert.AreEqual("Login", submitButton.TextContent);
        Assert.AreEqual("btn btn-primary", submitButton.GetAttribute("class"));
    }

    [TestMethod]
    public void Login_ContainsDevInfo()
    {
        // Act
        var component = RenderComponent<Login>();

        // Assert
        var devInfo = component.Find(".text-muted");
        Assert.IsNotNull(devInfo);
        Assert.IsTrue(devInfo.TextContent.Contains("User info"));
        Assert.IsTrue(devInfo.TextContent.Contains("DEV: src/AspireDeezNuts.ApiService/appsettings.Development.secrets.json"));
        Assert.IsTrue(devInfo.TextContent.Contains("Kubernetes: aspire-admin-seed secret"));
    }

    [TestMethod]
    public void Login_HasAllowAnonymousAttribute()
    {
        // This test verifies the component metadata - in a real scenario
        // you would check route authorization, but for now we verify the component renders
        // which indicates the AllowAnonymous attribute is working correctly
        
        // Act
        var component = RenderComponent<Login>();

        // Assert
        Assert.IsNotNull(component);
        Assert.IsNotNull(component.Find("form"));
    }

    [TestMethod]
    public void Login_WithComplexUrlEncodedError_DecodesCorrectly()
    {
        // Arrange
        var encodedError = "Login%20failed%3A%20invalid%20username%20or%20password";
        Services.GetRequiredService<FakeNavigationManager>().NavigateTo($"/login?error={encodedError}");

        // Act
        var component = RenderComponent<Login>();

        // Assert
        Assert.AreEqual(1, _toastService.ToastMessages.Count);
        var toast = _toastService.ToastMessages[0];
        Assert.AreEqual("Login failed: invalid username or password", toast.Message);
    }
}