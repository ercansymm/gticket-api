using System.ComponentModel.DataAnnotations;

namespace GBILET.Core.DTOs.Admin;

public class ResetAdminPasswordRequest
{
    [Required(ErrorMessage = "Yeni parola gerekli")]
    [MinLength(8, ErrorMessage = "Parola en az 8 karakter olmalı")]
    [MaxLength(128)]
    [RegularExpression(@"^(?=.*[A-Z])(?=.*\d).+$",
        ErrorMessage = "Parola en az 1 büyük harf ve 1 rakam içermeli")]
    public string NewPassword { get; set; } = null!;
}