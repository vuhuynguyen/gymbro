namespace WebApi.Requests.Admin;

public class PromoteUserRequest
{
    public string Email { get; set; } = null!;
    public bool IsAdmin { get; set; }
}
