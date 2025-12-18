using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace TranspoLink.Models;

#nullable disable warnings

// --- Custom Validation Attributes ---
public class MustBeTrueAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        return value is bool b && b;
    }
}

// --- Auth View Models ---
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

// --- Profile View Models ---
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

// --- Booking Process View Models ---
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

public class SeatSelectionVM
{
    public string TripId { get; set; }
    public Trip? Trip { get; set; }
    public Dictionary<string, bool> AvailableSeats { get; set; } = new Dictionary<string, bool>();

    [Required(ErrorMessage = "Please select at least one seat.")]
    [MinLength(1, ErrorMessage = "Please select at least one seat.")]
    public List<string> SelectedSeats { get; set; } = new List<string>();
    public string? MemberId { get; set; }
}

public class PassengerVM
{
    public string SeatNumber { get; set; }

    [Required(ErrorMessage = "Passenger name is required.")]
    public string Name { get; set; }

    [Range(1, 100, ErrorMessage = "Age must be between 1 and 100.")]
    public int Age { get; set; }
    public string TicketType { get; set; }
}

public class BookingVM
{
    public string TripId { get; set; }
    public Trip Trip { get; set; }
    public decimal BasePricePerTicket { get; set; }
    public List<PassengerVM> Passengers { get; set; } = new List<PassengerVM>();

    [EmailAddress]
    public string ContactEmail { get; set; }
    [Phone]
    public string ContactPhone { get; set; }

    public bool HasTravelInsurance { get; set; } = true;
    public bool HasRefundGuarantee { get; set; }
    public bool HasBoardingPass { get; set; } = true;
    public string PaymentMethod { get; set; }

    public decimal InsurancePrice { get; set; } = 2.00M;
    public decimal RefundGuaranteePrice { get; set; } = 4.00M;
    public decimal BoardingPassPrice { get; set; } = 1.00M;

    public decimal TotalBaseFare => Passengers.Count * BasePricePerTicket;
    public decimal TotalInsuranceFee => HasTravelInsurance ? Passengers.Count * InsurancePrice : 0;
    public decimal TotalRefundFee => HasRefundGuarantee ? Passengers.Count * RefundGuaranteePrice : 0;
    public decimal TotalBoardingPassFee => HasBoardingPass ? Passengers.Count * BoardingPassPrice : 0;
    public decimal FinalTotal => TotalBaseFare + TotalInsuranceFee + TotalRefundFee + TotalBoardingPassFee;
}

// --- Admin & Utility View Models ---
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