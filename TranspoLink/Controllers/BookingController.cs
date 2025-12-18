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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ReviewAndPay(BookingVM vm)
    {
        var sessionVm = HttpContext.Session.Get<BookingVM>(SESSION_BOOKING_PROCESS_VM);
        if (sessionVm == null)
        {
            HttpContext.Session.SetString(SESSION_ERROR, "Session expired.");
            return RedirectToAction("Index", "Home");
        }

        sessionVm.ContactEmail = vm.ContactEmail;
        sessionVm.ContactPhone = vm.ContactPhone;
        sessionVm.HasTravelInsurance = vm.HasTravelInsurance;
        sessionVm.HasRefundGuarantee = vm.HasRefundGuarantee;
        sessionVm.PaymentMethod = vm.PaymentMethod;
        sessionVm.Passengers = vm.Passengers;

        var trip = db.Trips
            .Include(t => t.Route)
            .Include(t => t.Vehicle).ThenInclude(v => v.Driver)
            .FirstOrDefault(t => t.Id == vm.TripId);

        if (ModelState.IsValid && trip != null)
        {
            try
            {
                var newBooking = new Booking
                {
                    MemberId = User.Identity?.Name,
                    TripId = trip.Id,
                    BookingDate = DateTime.Now,
                    NumberOfSeats = sessionVm.Passengers.Count,
                    TotalPrice = trip.Price * sessionVm.Passengers.Count,
                    Status = "PendingPayment",
                    IsPaid = false,
                    BookingReference = hp.GenerateBookingRef()
                };

                db.Bookings.Add(newBooking);
                db.SaveChanges();

                foreach (var p in sessionVm.Passengers)
                {
                    var newPassenger = new Passenger
                    {
                        BookingId = newBooking.Id,
                        Name = p.Name,
                        Age = p.Age,
                        SeatNumber = p.SeatNumber,
                        TicketType = p.TicketType
                    };
                    db.Passengers.Add(newPassenger);
                }
                db.SaveChanges();

                HttpContext.Session.Set(SESSION_BOOKING_SEATS,
                    vm.Passengers.ToDictionary(p => p.SeatNumber, p => p.Name));

                HttpContext.Session.SetString(SESSION_SUCCESS,
                    $"Booking {newBooking.BookingReference} created! Proceeding to payment...");

                HttpContext.Session.Remove(SESSION_BOOKING_PROCESS_VM);

                return RedirectToAction("Confirmation", new { @ref = newBooking.BookingReference });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Database Error: " + ex.Message);
            }
        }

        sessionVm.Trip = trip;
        sessionVm.Trip = null;
        HttpContext.Session.Set(SESSION_BOOKING_PROCESS_VM, sessionVm);
        sessionVm.Trip = trip;

        return View(sessionVm);
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
}