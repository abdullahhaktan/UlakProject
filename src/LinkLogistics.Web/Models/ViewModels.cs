using System.ComponentModel.DataAnnotations;

namespace LinkLogistics.Web.Models;

public sealed class LoginViewModel
{
    [Required]
    [Display(Name = "Phone")]
    public string Phone { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }

    public string? Error { get; set; }
}

public sealed class ErrorViewModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
