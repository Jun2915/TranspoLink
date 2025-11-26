using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace TranspoLink.Models;

#nullable disable warnings

// View Models ----------------------------------------------------------------

public class LoginVM
{
    [StringLength(100)]
    [EmailAddress]
    public string? Email { get; set; }

    [StringLength(20)]
    [DisplayName("Phone Number")]
    public string? Phone { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    public bool RememberMe { get; set; }
}

public class RegisterVM
{
    [StringLength(100)]
    [EmailAddress]
    [Remote("CheckEmail", "Account", ErrorMessage = "Duplicated {0}.")]
    public string? Email { get; set; }

    [StringLength(20)]
    [DisplayName("Phone Number")]
    [Remote("CheckPhone", "Account", ErrorMessage = "Duplicated {0}.")] 
    public string? PhoneNumber { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [Compare("Password")]
    [DataType(DataType.Password)]
    [DisplayName("Confirm Password")]
    public string Confirm { get; set; }

    [StringLength(100)]
    public string Name { get; set; }

    public IFormFile Photo { get; set; }
}

public class UpdatePasswordVM
{
    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    [DisplayName("Current Password")]
    public string Current { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    [DisplayName("New Password")]
    public string New { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [Compare("New")]
    [DataType(DataType.Password)]
    [DisplayName("Confirm Password")]
    public string Confirm { get; set; }
}

public class UpdateProfileVM
{
    public string? Email { get; set; }
    public string? Phone { get; set; }

    [StringLength(100)]
    public string Name { get; set; }

    public string? PhotoURL { get; set; }

    public IFormFile? Photo { get; set; }
}

public class ResetPasswordVM
{
    [StringLength(100)]
    [EmailAddress]
    public string Email { get; set; }
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
    public string? TransportType { get; set; } // Bus, Train, Ferry
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