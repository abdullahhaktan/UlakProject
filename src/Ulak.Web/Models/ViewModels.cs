using System.ComponentModel.DataAnnotations;

namespace Ulak.Web.Models;

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

public sealed class SignUpViewModel
{
    [Required]
    [Display(Name = "Firma adı")]
    [StringLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Adınız")]
    [StringLength(120)]
    public string AdminName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Telefon")]
    [StringLength(20)]
    public string Phone { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [MinLength(6)]
    [Display(Name = "Şifre")]
    public string Password { get; set; } = string.Empty;

    public string? Error { get; set; }
}

public sealed class ErrorViewModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
