namespace AspireDeezNuts.ApiService.Data;

public class SeedDataOptions
{
    public const string SeedData = "SeedData";
    
    public UserSeedData? InitialAdmin { get; set; }
    public UserSeedData? InitialUser { get; set; }
}

public class UserSeedData
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}