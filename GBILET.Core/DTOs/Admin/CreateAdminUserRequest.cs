using System.ComponentModel.DataAnnotations;
using GBILET.Core.Entities.Admin;

namespace GBILET.Core.DTOs.Admin;

public class CreateAdminUserRequest
{
    [Required(ErrorMessage = "Kullanıcı adı gerekli")]
    [MinLength(3, ErrorMessage = "Kullanıcı adı en az 3 karakter olmalı")]
    [MaxLength(64, ErrorMessage = "Kullanıcı adı en fazla 64 karakter olabilir")]
    [RegularExpression(@"^[a-zA-Z0-9_.-]+$", ErrorMessage = "Kullanıcı adı sadece harf, rakam, nokta, alt çizgi ve tire içerebilir")]
    public string Username { get; set; } = null!;

    [Required(ErrorMessage = "Email gerekli")]
    [EmailAddress(ErrorMessage = "Geçersiz email formatı")]
    [MaxLength(256)]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Ad soyad gerekli")]
    [MinLength(2)]
    [MaxLength(128)]
    public string FullName { get; set; } = null!;

    [Required(ErrorMessage = "Parola gerekli")]
    [MinLength(8, ErrorMessage = "Parola en az 8 karakter olmalı")]
    [MaxLength(128)]
    [RegularExpression(@"^(?=.*[A-Z])(?=.*\d).+$",
        ErrorMessage = "Parola en az 1 büyük harf ve 1 rakam içermeli")]
    public string Password { get; set; } = null!;

    [Required(ErrorMessage = "Rol gerekli")]
    public AdminRole Role { get; set; }
}