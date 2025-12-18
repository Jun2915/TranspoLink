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
    // Define constant Session Keys
    public const string SESSION_BOOKING_PROCESS_VM = "BookingProcessVM";
    public const string SESSION_BOOKING_SEATS = "BookingSeats";
    public const string SESSION_SELECTED_SEATS = "SelectedSeats";
    public const string SESSION_SUCCESS = "SuccessMessage";
    public const string SESSION_ERROR = "ErrorMessage";
    public const string SESSION_INFO = "InfoMessage";

    // STEP 1: Seat Selection (GET)
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
            // 从 Session 中获取之前可能选择的座位（如果从 ReviewAndPay 返回）
            SelectedSeats = HttpContext.Session.Get<List<string>>(SESSION_SELECTED_SEATS) ?? new List<string>(),
        };

        HttpContext.Session.Remove(SESSION_SELECTED_SEATS);

        return View(vm);
    }

    // STEP 1: Seat Selection (POST) - 处理座位选择表单提交
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Book(SeatSelectionVM vm)
    {
        // 1. 模型验证
        if (!ModelState.IsValid || vm.SelectedSeats == null || vm.SelectedSeats.Count == 0)
        {
            // --- 🛠️ 补全：重新加载 Trip 数据以供视图显示 ---
            vm.Trip = db.Trips
                .Include(t => t.Route)
                .Include(t => t.Vehicle).ThenInclude(v => v.Driver)
                .FirstOrDefault(t => t.Id == vm.TripId);

            if (vm.Trip != null)
            {
                // 重新生成座位图数据
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



        // 🛠️ 获取 MemberId
        vm.MemberId = User.Identity?.Name;

        if (string.IsNullOrEmpty(vm.MemberId))
        {
            HttpContext.Session.SetString(SESSION_ERROR, "Member session expired or ID missing.");
            return RedirectToAction("Index", "Home");
        }

        // 2. 从 Session 中获取或初始化 BookingVM
        var actualSeats = new List<string>();
        foreach (var s in vm.SelectedSeats)
        {
            if (s.Contains(','))
            {
                // 如果是 "2C, 6C"，拆分为 ["2C", "6C"]
                actualSeats.AddRange(s.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()));
            }
            else
            {
                actualSeats.Add(s);
            }
        }

        var trip = db.Trips.FirstOrDefault(t => t.Id == vm.TripId);
        if (trip == null) return RedirectToAction("Index", "Home");

        // 初始化 BookingVM
        var sessionVm = new BookingVM
        {
            TripId = vm.TripId,
            BasePricePerTicket = trip.Price,
            Passengers = new List<PassengerVM>()
        };

        // 为拆分后的每一个座位创建一个独立的乘客
        for (int i = 0; i < actualSeats.Count; i++)
        {
            sessionVm.Passengers.Add(new PassengerVM
            {
                SeatNumber = actualSeats[i], // 这里保证每个乘客只有一个座号
                Name = "",
                Age = 0,
                TicketType = "Adult"
            });
        }

        // 使用你定义的扩展方法存储 Session
        HttpContext.Session.Set(SESSION_BOOKING_PROCESS_VM, sessionVm);

        return RedirectToAction("ReviewAndPay");
    }


    // STEP 2: Review & Pay (GET) - 旅客详情和支付页面
    [HttpGet]
    public IActionResult ReviewAndPay()
    {
        // 1. 从 Session 中获取 BookingVM
        var jsonString = HttpContext.Session.GetString(SESSION_BOOKING_PROCESS_VM);

        if (string.IsNullOrEmpty(jsonString))
        {
            HttpContext.Session.SetString(SESSION_ERROR, "Booking process interrupted. (Session String Empty)");
            return RedirectToAction("Index", "Home");
        }

        // 反序列化 JSON 字符串为 BookingVM 对象
        var sessionVm = System.Text.Json.JsonSerializer.Deserialize<BookingVM>(jsonString);

        // 🛠️ 添加调试信息
        Console.WriteLine($"DEBUG: Passenger count in session: {sessionVm?.Passengers?.Count ?? 0}");
        if (sessionVm?.Passengers != null)
        {
            Console.WriteLine($"DEBUG: Seats in session: {string.Join(", ", sessionVm.Passengers.Select(p => p.SeatNumber))}");
        }

        if (sessionVm == null)
        {
            HttpContext.Session.SetString(SESSION_ERROR, "Booking process interrupted. (JSON Deserialization Failed)");
            return RedirectToAction("Index", "Home");
        }

        // 2. 重新加载完整的 Trip 信息
        var trip = db.Trips
            .Include(t => t.Route)
            .Include(t => t.Vehicle).ThenInclude(v => v.Driver)
            .FirstOrDefault(t => t.Id == sessionVm.TripId);

        if (trip == null)
        {
            HttpContext.Session.SetString(SESSION_ERROR, "Trip not found or no longer available.");
            return RedirectToAction("Index", "Home");
        }

        sessionVm.Trip = trip;

        // 🛠️ 添加更多调试：检查乘客和座位
        if (sessionVm.Passengers.Count == 0 || sessionVm.Passengers.Any(p => string.IsNullOrEmpty(p.SeatNumber)))
        {
            Console.WriteLine($"DEBUG ERROR: No seats or invalid seats. Count: {sessionVm.Passengers.Count}");
            foreach (var p in sessionVm.Passengers)
            {
                Console.WriteLine($"  Passenger - Seat: '{p.SeatNumber}', Name: '{p.Name}', Age: {p.Age}");
            }

            HttpContext.Session.SetString(SESSION_ERROR, "No seats selected. Please select a seat first.");
            return RedirectToAction("Book", new { id = sessionVm.TripId });
        }

        return View(sessionVm);
    }


    // STEP 2: Review and Pay (POST) - Final Booking Submission
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ReviewAndPay(BookingVM vm)
    {
        var sessionVm = HttpContext.Session.Get<BookingVM>(SESSION_BOOKING_PROCESS_VM);
        if (sessionVm == null)
        {
            HttpContext.Session.SetString(SESSION_ERROR, "Session expired. Please start over.");
            return RedirectToAction("Index", "Home");
        }

        // 合并POST数据和Session数据
        sessionVm.ContactEmail = vm.ContactEmail;
        sessionVm.ContactPhone = vm.ContactPhone;
        sessionVm.HasTravelInsurance = vm.HasTravelInsurance;
        sessionVm.HasRefundGuarantee = vm.HasRefundGuarantee;
        sessionVm.PaymentMethod = vm.PaymentMethod;
        sessionVm.Passengers = vm.Passengers;

        // 重新加载Trip信息 (需要在 ModelState.IsValid 失败时显示 View)
        sessionVm.Trip = db.Trips
            .Include(t => t.Route)
            .Include(t => t.Vehicle).ThenInclude(v => v.Driver)
            .FirstOrDefault(t => t.Id == vm.TripId);

        if (ModelState.IsValid)
        {
            // 在存储到 Session 之前移除 Trip
            sessionVm.Trip = null;
            HttpContext.Session.Set(SESSION_BOOKING_PROCESS_VM, sessionVm);

            // --- DATABASE INSERT ---
            try
            {
                var newBooking = new Booking
                {
                    // ... (omitted database fields logic) ...
                    Status = "PendingPayment",
                    IsPaid = false,
                    BookingReference = hp.GenerateBookingRef(),
                };

                db.Bookings.Add(newBooking);
                db.SaveChanges();

                // 保存座位信息到Session供确认页面使用
                HttpContext.Session.Set(SESSION_BOOKING_SEATS,
                    vm.Passengers.ToDictionary(p => p.SeatNumber, p => p.Name));

                HttpContext.Session.SetString(SESSION_SUCCESS,
                    $"Booking {newBooking.BookingReference} created! Proceeding to payment gateway.");

                return RedirectToAction("Confirmation", new { @ref = newBooking.BookingReference });
            }
            catch (Exception ex)
            {
                // 如果数据库插入失败，重新加载 Trip 用于 View
                sessionVm.Trip = db.Trips.Include(t => t.Route).Include(t => t.Vehicle).ThenInclude(v => v.Driver).FirstOrDefault(t => t.Id == vm.TripId);

                // 确保在返回 View 前将更新后的数据存回 Session
                sessionVm.Trip = null;
                HttpContext.Session.Set(SESSION_BOOKING_PROCESS_VM, sessionVm);
                HttpContext.Session.SetString(SESSION_ERROR,
                    "Database error during booking: " + ex.Message);
                return View(sessionVm);
            }
        }

        // 验证失败，重新显示表单
        // 恢复 Trip 导航属性，因为 View 需要它
        sessionVm.Trip = db.Trips.Include(t => t.Route).Include(t => t.Vehicle).ThenInclude(v => v.Driver).FirstOrDefault(t => t.Id == vm.TripId);

        // 确保在返回 View 前将更新后的数据存回 Session
        sessionVm.Trip = null;
        HttpContext.Session.Set(SESSION_BOOKING_PROCESS_VM, sessionVm);
        return View(sessionVm);
    }



    // STEP 3: Confirmation/Success Page
    [HttpGet]
    public IActionResult Confirmation(string @ref)
    {
        var booking = db.Bookings
            .Include(b => b.Trip).ThenInclude(t => t.Route)
            .Include(b => b.Trip).ThenInclude(t => t.Vehicle).ThenInclude(v => v.Driver)
            .FirstOrDefault(b => b.BookingReference == @ref);

        var seatsData = HttpContext.Session.Get<Dictionary<string, string>>(SESSION_BOOKING_SEATS);

        // 移除座位数据
        HttpContext.Session.Remove(SESSION_BOOKING_SEATS);

        if (booking == null)
        {
            HttpContext.Session.SetString(SESSION_INFO, "Booking not found.");
            return RedirectToAction("Index", "Home");
        }

        ViewBag.SeatsData = seatsData;

        return View(booking);
    }

    // Debugging action
    [HttpGet]
    public IActionResult DebugSession()
    {
        var sessionVm = HttpContext.Session.Get<BookingVM>("BookingProcessVM");

        if (sessionVm == null)
        {
            return Content("No BookingVM in Session");
        }

        return Content($"Session Data:\n" +
                       $"TripId: {sessionVm.TripId}\n" +
                       $"Passengers: {sessionVm.Passengers?.Count ?? 0}\n" +
                       $"Seats: {string.Join(", ", sessionVm.Passengers?.Select(p => p.SeatNumber) ?? new List<string>())}");
    }
}