using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace TranspoLink.Models;

#nullable disable warnings

// Custom Attribute for Mandatory Checkbox
public class MustBeTrueAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        return value is bool b && b;
    }
}

// View Models ----------------------------------------------------------------

public class LoginVM
{
    [Required(ErrorMessage = "Please enter your Email or Phone Number.")]
    [StringLength(100)]
    [DisplayName("Email / Phone Number")]
    public string Input { get; set; }

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    public bool RememberMe { get; set; }

    [MustBeTrue(ErrorMessage = "You must agree to the Terms and Conditions.")]
    public bool TermsAgreed { get; set; }
}

public class RegisterVM
{
    [Required(ErrorMessage = "Please enter your Email or Phone Number.")]
    [StringLength(100)]
    [DisplayName("Email / Phone Number")]
    public string Input { get; set; }

    [Required]
    [DataType(DataType.Password)]
    // Regex: At least 1 Upper, 1 Number, 1 Symbol, Min 8 chars
    [RegularExpression(@"^(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,}$",
        ErrorMessage = "Password must be 8+ chars, with 1 Uppercase, 1 Number, and 1 Symbol.")]
    public string Password { get; set; }

    [Required]
    [Compare("Password", ErrorMessage = "Passwords do not match.")]
    [DataType(DataType.Password)]
    [DisplayName("Confirm Password")]
    public string Confirm { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; }

    public IFormFile? Photo { get; set; }

    [MustBeTrue(ErrorMessage = "You must agree to the Terms and Conditions.")]
    public bool TermsAgreed { get; set; }
}

public class UpdatePasswordVM
{
    [Required]
    [DataType(DataType.Password)]
    [DisplayName("Current Password")]
    public string Current { get; set; }

    [Required]
    [DataType(DataType.Password)]
    [DisplayName("New Password")]
    [RegularExpression(@"^(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,}$",
        ErrorMessage = "Password must be 8+ chars, with 1 Uppercase, 1 Number, and 1 Symbol.")]
    public string New { get; set; }

    [Required]
    [Compare("New", ErrorMessage = "Passwords do not match.")]
    [DataType(DataType.Password)]
    [DisplayName("Confirm Password")]
    public string Confirm { get; set; }
}

public class UpdateProfileVM
{
    [EmailAddress]
    [StringLength(100)]
    public string? Email { get; set; }

    [StringLength(11, ErrorMessage = "Phone number cannot exceed 11 digits.")]
    [RegularExpression(@"^[0-9+\-\s]*$", ErrorMessage = "Invalid phone format.")]
    public string? Phone { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; }

    public string? PhotoURL { get; set; }
    public string? Role { get; set; }
    public IFormFile? Photo { get; set; }
}

public class SearchTripVM
{
    [Required]
    [StringLength(100)]
    public string Origin { get; set; }

    [Required]
    [StringLength(100)]
    public string Destination { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [DisplayName("Departure Date")]
    public DateTime DepartDate { get; set; }

    [DataType(DataType.Date)]
    [DisplayName("Return Date")]
    public DateTime? ReturnDate { get; set; }

    [Range(1, 10)]
    public int Passengers { get; set; } = 1;

    [MaxLength(20)]
    public string? TransportType { get; set; }
}

public class BookTripVM
{
    [Required]
    public int TripId { get; set; }

    [Required]
    [Range(1, 10)]
    public int NumberOfSeats { get; set; } = 1;

    public string? SpecialRequests { get; set; }
}

public class ForgotPasswordVM
{
    [Required]
    [StringLength(100)]
    public string EmailOrPhone { get; set; }
}

public class VerifyOtpVM
{
    [Required]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP must be 6 digits")]
    public string Otp { get; set; }
}

public class ResetPasswordVM
{
    [Required]
    [DataType(DataType.Password)]
    [DisplayName("New Password")]
    [RegularExpression(@"^(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,}$",
        ErrorMessage = "Password must be 8+ chars, with 1 Uppercase, 1 Number, and 1 Symbol.")]
    public string NewPassword { get; set; }

    [Required]
    [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
    [DataType(DataType.Password)]
    [DisplayName("Confirm Password")]
    public string ConfirmPassword { get; set; }
}

public class AdminVM
{
    public string? Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; }

    [Required]
    [EmailAddress]
    [StringLength(100)]
    public string Email { get; set; }

    [Required]
    [StringLength(11, ErrorMessage = "Phone number cannot exceed 11 digits.")]
    [RegularExpression(@"^[0-9+\-\s]*$", ErrorMessage = "Phone number can only contain numbers, +, - and spaces.")]
    public string Phone { get; set; }

    [DataType(DataType.Password)]
    public string? Password { get; set; }

    [Compare("Password")]
    [DataType(DataType.Password)]
    [DisplayName("Confirm Password")]
    public string? ConfirmPassword { get; set; }

    public IFormFile? Photo { get; set; }
    public string? ExistingPhotoURL { get; set; }
    public bool IsBlocked { get; set; }
}

public class TimelineItemVM
{
    public string Title { get; set; }
    public string Time { get; set; }
    public string Type { get; set; }
    public string Icon { get; set; }
    public string CssClass { get; set; }
}