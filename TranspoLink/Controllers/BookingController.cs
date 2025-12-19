using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TranspoLink.Models;
using System.Linq;
using System.Collections.Generic;
using System;
using Microsoft.AspNetCore.Http;

namespace TranspoLink.Controllers;

[Authorize(Roles = "Member")]
public class BookingController(DB db, Helper hp) : Controller
{
    public const string SESSION_BOOKING_PROCESS_VM = "BookingProcessVM";
    public const string SESSION_BOOKING_SEATS = "BookingSeats";
    public const string SESSION_SELECTED_SEATS = "SelectedSeats";
    public const string SESSION_SUCCESS = "SuccessMessage";
    public const string SESSION_ERROR = "ErrorMessage";
    public const string SESSION_INFO = "InfoMessage";

    // View Order List (Index)
    [HttpGet]
    public IActionResult Index()
    {
        var memberId = User.Identity?.Name;
        var myBookings = db.Bookings
            .Include(b => b.Trip).ThenInclude(t => t.Route)
            .Include(b => b.Trip).ThenInclude(t => t.Vehicle)
            .Where(b => b.MemberId == memberId)
            .OrderByDescending(b => b.BookingDate)
            .ToList();

        return View(myBookings);
    }

    // =========================================================================
    // NEW ACTION: List of Available Trips
    // =========================================================================
    [HttpGet]
    public IActionResult TripList(string search)
    {
        // 1. Filter for trips that are "Scheduled" and in the future
        var query = db.Trips
            .Include(t => t.Route)
            .Include(t => t.Vehicle)
            .Where(t => t.Status == "Scheduled" && t.DepartureTime > DateTime.Now)
            .AsQueryable();

        // 2. Optional: Simple search by Origin or Destination
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(t => t.Route.Origin.Contains(search) ||
                                     t.Route.Destination.Contains(search));
        }

        var trips = query.OrderBy(t => t.DepartureTime).ToList();

        ViewBag.Search = search;
        return View(trips);
    }

    // Step 1: Seat Selection (GET)
    [HttpGet]
    public IActionResult Book(string id)
    {
        var trip = db.Trips
            .Include(t => t.Route)
            .Include(t => t.Vehicle).ThenInclude(v => v.Driver)
            .FirstOrDefault(t => t.Id == id);

        if (trip == null)
        {
            HttpContext.Session.SetString(SESSION_ERROR, "Trip not found or no longer available.");
            return RedirectToAction("Index", "Home");
        }

        var totalSeats = trip.Vehicle?.TotalSeats ?? 40;
        var bookedSeats = hp.GetBookedSeatsForTrip(id);
        var allSeats = hp.GenerateSeatLayout(totalSeats);

        var availableSeats = allSeats.ToDictionary(
            seat => seat,
            seat => !bookedSeats.Contains(seat)
        );

        var vm = new SeatSelectionVM
        {
            TripId = trip.Id,
            Trip = trip,
            AvailableSeats = availableSeats,
            SelectedSeats = HttpContext.Session.Get<List<string>>(SESSION_SELECTED_SEATS) ?? []
        };

        HttpContext.Session.Remove(SESSION_SELECTED_SEATS);

        return View(vm);
    }

    // Step 1: Seat Selection (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Book(SeatSelectionVM vm)
    {
        if (!ModelState.IsValid || vm.SelectedSeats == null || vm.SelectedSeats.Count == 0)
        {
            vm.Trip = db.Trips
                .Include(t => t.Route)
                .Include(t => t.Vehicle).ThenInclude(v => v.Driver)
                .FirstOrDefault(t => t.Id == vm.TripId);

            if (vm.Trip != null)
            {
                var totalSeats = vm.Trip.Vehicle?.TotalSeats ?? 40;
                var bookedSeats = hp.GetBookedSeatsForTrip(vm.TripId);
                var allSeats = hp.GenerateSeatLayout(totalSeats);

                vm.AvailableSeats = allSeats.ToDictionary(
                    seat => seat,
                    seat => !bookedSeats.Contains(seat)
                );
            }

            if (vm.SelectedSeats == null || vm.SelectedSeats.Count == 0)
            {
                ModelState.AddModelError("SelectedSeats", "Please select at least one seat.");
            }

            return View(vm);
        }

        vm.MemberId = User.Identity?.Name;

        if (string.IsNullOrEmpty(vm.MemberId))
        {
            HttpContext.Session.SetString(SESSION_ERROR, "Session expired.");
            return RedirectToAction("Index", "Home");
        }

        var actualSeats = new List<string>();
        foreach (var s in vm.SelectedSeats)
        {
            if (s.Contains(','))
            {
                actualSeats.AddRange(s.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()));
            }
            else
            {
                actualSeats.Add(s);
            }
        }

        var trip = db.Trips.FirstOrDefault(t => t.Id == vm.TripId);
        if (trip == null)
            return RedirectToAction("Index", "Home");

        var sessionVm = new BookingVM
        {
            TripId = vm.TripId,
            BasePricePerTicket = trip.Price,
            Passengers = []
        };

        foreach (var seat in actualSeats)
        {
            sessionVm.Passengers.Add(new PassengerVM
            {
                SeatNumber = seat,
                Name = "",
                Age = 0,
                TicketType = "Adult"
            });
        }

        HttpContext.Session.Set(SESSION_BOOKING_PROCESS_VM, sessionVm);

        return RedirectToAction("ReviewAndPay");
    }

    // Step 2: Review and Pay (GET)
    [HttpGet]
    public IActionResult ReviewAndPay()
    {
        var sessionVm = HttpContext.Session.Get<BookingVM>(SESSION_BOOKING_PROCESS_VM);

        if (sessionVm == null)
        {
            HttpContext.Session.SetString(SESSION_ERROR, "Booking session expired.");
            return RedirectToAction("Index", "Home");
        }

        var trip = db.Trips
            .Include(t => t.Route)
            .Include(t => t.Vehicle).ThenInclude(v => v.Driver)
            .FirstOrDefault(t => t.Id == sessionVm.TripId);

        if (trip == null)
        {
            HttpContext.Session.SetString(SESSION_ERROR, "Trip unavailable.");
            return RedirectToAction("Index", "Home");
        }

        sessionVm.Trip = trip;

        if (sessionVm.Passengers.Count == 0)
        {
            HttpContext.Session.SetString(SESSION_ERROR, "No seats selected.");
            return RedirectToAction("Book", new { id = sessionVm.TripId });
        }

        return View(sessionVm);
    }

    // Step 2: Review and Pay (POST) - Saves data to Session and moves to Payment
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ReviewAndPay(BookingVM vm)
    {
        var sessionVm = HttpContext.Session.Get<BookingVM>(SESSION_BOOKING_PROCESS_VM);
        if (sessionVm == null)
            return RedirectToAction("Index", "Home");

        // Sync Data
        sessionVm.ContactEmail = vm.ContactEmail;
        sessionVm.ContactPhone = vm.ContactPhone;
        sessionVm.HasTravelInsurance = vm.HasTravelInsurance;
        sessionVm.HasBoardingPass = vm.HasBoardingPass;
        sessionVm.Passengers = vm.Passengers;

        if (!ModelState.IsValid)
        {
            sessionVm.Trip = db.Trips.Include(t => t.Route).FirstOrDefault(t => t.Id == sessionVm.TripId);
            return View(sessionVm);
        }

        // Save updated data to session
        sessionVm.Trip = null; // Clear circular reference
        HttpContext.Session.Set(SESSION_BOOKING_PROCESS_VM, sessionVm);

        return RedirectToAction("Payment");
    }

    // Step 3: Payment (GET)
    [HttpGet]
    public IActionResult Payment()
    {
        var sessionVm = HttpContext.Session.Get<BookingVM>(SESSION_BOOKING_PROCESS_VM);
        if (sessionVm == null)
            return RedirectToAction("Index", "Home");

        sessionVm.Trip = db.Trips.Include(t => t.Route).FirstOrDefault(t => t.Id == sessionVm.TripId);

        return View(sessionVm);
    }

    // Step 3: Process Payment (POST) - Finalizes Booking and Saves to DB
    [HttpPost]
    public IActionResult ProcessPayment()
    {
        var sessionVm = HttpContext.Session.Get<BookingVM>(SESSION_BOOKING_PROCESS_VM);
        if (sessionVm == null)
            return Json(new { success = false, message = "Session Expired" });

        try
        {
            // 1. Create Booking
            var newBooking = new Booking
            {
                BookingReference = hp.GenerateBookingRef(),
                TripId = sessionVm.TripId,
                MemberId = User.Identity?.Name ?? "Guest",
                Status = "Paid",
                TotalAmount = sessionVm.FinalTotal,
                CreatedAt = DateTime.Now,
                BookingDate = DateTime.Now,
                NumberOfSeats = sessionVm.Passengers.Count
            };

            db.Bookings.Add(newBooking);
            db.SaveChanges(); // Save to get Booking ID

            // 2. Create Passengers
            if (sessionVm.Passengers != null)
            {
                foreach (var pvm in sessionVm.Passengers)
                {
                    var passenger = new Passenger
                    {
                        BookingId = newBooking.Id,
                        Name = pvm.Name,
                        Age = pvm.Age,
                        SeatNumber = pvm.SeatNumber,
                        TicketType = pvm.TicketType
                    };
                    db.Passengers.Add(passenger);
                }
                db.SaveChanges();
            }

            HttpContext.Session.Set(SESSION_BOOKING_SEATS,
                sessionVm.Passengers.ToDictionary(p => p.SeatNumber, p => p.Name));

            HttpContext.Session.SetString(SESSION_SUCCESS, "Payment Successful!");
            HttpContext.Session.Remove(SESSION_BOOKING_PROCESS_VM);

            // Return Success with Redirect URL
            return Json(new { success = true, redirectUrl = Url.Action("Confirmation", new { @ref = newBooking.BookingReference }) });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult Confirmation(string @ref)
    {
        var booking = db.Bookings
            .Include(b => b.Trip).ThenInclude(t => t.Route)
            .Include(b => b.Trip).ThenInclude(t => t.Vehicle).ThenInclude(v => v.Driver)
            .FirstOrDefault(b => b.BookingReference == @ref);

        var seatsData = HttpContext.Session.Get<Dictionary<string, string>>(SESSION_BOOKING_SEATS);
        HttpContext.Session.Remove(SESSION_BOOKING_SEATS);

        if (booking == null)
        {
            return RedirectToAction("Index", "Home");
        }

        ViewBag.SeatsData = seatsData;
        return View(booking);
    }

    [HttpPost]
    public IActionResult CancelBooking(int id)
    {
        var booking = db.Bookings.Find(id);
        if (booking != null && booking.MemberId == User.Identity.Name)
        {
            booking.Status = "Cancelled";
            db.SaveChanges();
        }
        return RedirectToAction("Index");
    }
}