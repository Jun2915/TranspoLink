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


    [HttpGet]
    public IActionResult ReviewAndPay()
    {
        // 统一使用 Get<T> 扩展方法，避免手动解析 JSON 字符串
        var sessionVm = HttpContext.Session.Get<BookingVM>(SESSION_BOOKING_PROCESS_VM);

        if (sessionVm == null)
        {
            HttpContext.Session.SetString(SESSION_ERROR, "Booking session expired.");
            return RedirectToAction("Index", "Home");
        }

      
        sessionVm.Trip = db.Trips
            .Include(t => t.Route)
            .Include(t => t.Vehicle).ThenInclude(v => v.Driver)
            .FirstOrDefault(t => t.Id == sessionVm.TripId);

        if (sessionVm.Trip == null) return RedirectToAction("Index", "Home");

        return View(sessionVm);
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ReviewAndPay(BookingVM vm)
    {
        var sessionVm = HttpContext.Session.Get<BookingVM>(SESSION_BOOKING_PROCESS_VM);
        if (sessionVm == null) return RedirectToAction("Index", "Home");

        // 同步数据
        sessionVm.ContactEmail = vm.ContactEmail;
        sessionVm.ContactPhone = vm.ContactPhone;
        sessionVm.HasTravelInsurance = vm.HasTravelInsurance;
        sessionVm.HasBoardingPass = vm.HasBoardingPass;
        sessionVm.Passengers = vm.Passengers;

        if (!ModelState.IsValid)
        {
            // 🔥 核心调试代码：打印出所有验证失败的原因
            foreach (var modelState in ModelState.Values)
            {
                foreach (var error in modelState.Errors)
                {
                    // 在 VS 下方的 "Output" 窗口查看这里的输出
                    System.Diagnostics.Debug.WriteLine("验证失败原因: " + error.ErrorMessage);
                }
            }

            // 验证失败必须重装 Trip 才能返回 View
            sessionVm.Trip = db.Trips.Include(t => t.Route).FirstOrDefault(t => t.Id == sessionVm.TripId);
            return View(sessionVm);
        }

        // 验证成功
        sessionVm.Trip = null;
        HttpContext.Session.Set(SESSION_BOOKING_PROCESS_VM, sessionVm);
        return RedirectToAction("Payment");
    }

    // STEP 3: Payment (GET)
    [HttpGet]
    public IActionResult Payment()
    {
        var sessionVm = HttpContext.Session.Get<BookingVM>(SESSION_BOOKING_PROCESS_VM);
        if (sessionVm == null) return RedirectToAction("Index", "Home");

        // 加载支付页面所需数据
        sessionVm.Trip = db.Trips.Include(t => t.Route).FirstOrDefault(t => t.Id == sessionVm.TripId);

        return View(sessionVm);
    }

    // STEP 3: 处理最终支付 (POST)
    [HttpPost]
    public IActionResult ProcessPayment()
    {
        var sessionVm = HttpContext.Session.Get<BookingVM>(SESSION_BOOKING_PROCESS_VM);
        if (sessionVm == null) return Json(new { success = false });

        try
        {
            var newBooking = new Booking
            {
                BookingReference = hp.GenerateBookingRef(),
                TripId = sessionVm.TripId,
                MemberId = User.Identity?.Name ?? "Guest",
                Status = "Paid",
                TotalAmount = sessionVm.FinalTotal, // 对应 sessionVm.FinalTotal
                CreatedAt = DateTime.Now
            };

            db.Bookings.Add(newBooking);
            db.SaveChanges();

            HttpContext.Session.Remove(SESSION_BOOKING_PROCESS_VM);
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            Console.WriteLine("ModelState Errors: " + string.Join(", ", errors));

            // 重新加载数据返回 View
            sessionVm.Trip = db.Trips.Include(t => t.Route).FirstOrDefault(t => t.Id == sessionVm.TripId);
            return View(sessionVm);
        }
    }


    // 查看订单列表
    [HttpGet]
    public IActionResult BookingList()
    {
        var memberId = User.Identity?.Name;

        if (string.IsNullOrEmpty(memberId))
        {
            return RedirectToAction("Login", "Account");
        }

        // 加载 Trip 和 Route 信息以供显示
        var bookings = db.Bookings
            .Include(b => b.Trip)
                .ThenInclude(t => t.Route)
            .Where(b => b.MemberId == memberId)
            .OrderByDescending(b => b.CreatedAt)
            .ToList();

        return View(bookings);
    }

    // 取消订单
    [HttpPost]
    public IActionResult CancelBooking(int id)
    {
        var booking = db.Bookings.Find(id);
        if (booking != null && booking.MemberId == User.Identity.Name)
        {
            booking.Status = "Cancelled";
            db.SaveChanges();
        }
        return RedirectToAction("BookingList");
    }
}