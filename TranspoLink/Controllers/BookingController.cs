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

        if (trip == null) return RedirectToAction("Index", "Home");

        var totalSeats = trip.Vehicle?.TotalSeats ?? 40;

        // 🛠️ 关键修改：传入 db 获取真实的已定座位列表
        var bookedSeats = hp.GetBookedSeatsForVehicle(id, db);

        var allSeats = hp.GenerateSeatLayout(totalSeats);

        // 将这些座位在 AvailableSeats 字典中标记为 false
        var availableSeats = allSeats.ToDictionary(
            seat => seat,
            seat => !bookedSeats.Contains(seat)
        );

        var vm = new SeatSelectionVM
        {
            TripId = trip.Id,
            Trip = trip,
            AvailableSeats = availableSeats,
            SelectedSeats = HttpContext.Session.Get<List<string>>(SESSION_SELECTED_SEATS) ?? new List<string>(),
        };

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
                var bookedSeats = hp.GetBookedSeatsForVehicle(vm.TripId, db);
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
        vm.MemberEmail = User.Identity?.Name;

        if (string.IsNullOrEmpty(vm.MemberEmail))
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

        // 🛠️ 手动移除那些在当前页面没有输入、但会导致验证失败的错误
        ModelState.Remove("PaymentMethod");
        ModelState.Remove("CardNumber");
        ModelState.Remove("ExpiryDate");
        ModelState.Remove("CVV");
        ModelState.Remove("Trip"); // 虽有 ValidateNever 但手动移除更保险

        if (ModelState.IsValid)
        {
            sessionVm.Trip = null;
            HttpContext.Session.Set(SESSION_BOOKING_PROCESS_VM, sessionVm);
            // ✅ 验证通过后，将成功重定向到 Payment 页面
            return RedirectToAction("Payment");
        }

        // ❌ 如果还是验证失败，重新加载 Trip 返回 View
        sessionVm.Trip = db.Trips.Include(t => t.Route).FirstOrDefault(t => t.Id == sessionVm.TripId);
        return View(sessionVm);
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
    public IActionResult ProcessPayment(string expiryDate) // 接收前端传来的日期
    {
        var sessionVm = HttpContext.Session.Get<BookingVM>(SESSION_BOOKING_PROCESS_VM);
        if (sessionVm == null) return Json(new { success = false, message = "Session expired." });

        // 🛠️ 后端日期验证逻辑
        if (!string.IsNullOrEmpty(expiryDate) && expiryDate.Contains('/'))
        {
            try
            {
                var parts = expiryDate.Split('/');
                int month = int.Parse(parts[0]);
                int year = int.Parse("20" + parts[1]);

                var now = DateTime.Now;
                if (year < now.Year || (year == now.Year && month < now.Month))
                {
                    // 如果过期，返回特定标志以便前端刷新
                    return Json(new { success = false, isExpired = true, message = "Card Expired. Re-booking required." });
                }
            }
            catch
            {
                return Json(new { success = false, message = "Invalid date format." });
            }
        }

        using var transaction = db.Database.BeginTransaction();
        try
        {
            var newBooking = new Booking
            {
                BookingReference = hp.GenerateBookingRef(),
                TripId = sessionVm.TripId,
                MemberEmail = User.Identity?.Name,
                Status = "Paid",
                TotalAmount = sessionVm.FinalTotal,
                CreatedAt = DateTime.Now,
                NumberOfSeats = sessionVm.Passengers.Count // 记录座位数
            };

            db.Bookings.Add(newBooking);
            db.SaveChanges();

            // 🛠️ 关键：将每个乘客和座位保存到数据库
            foreach (var p in sessionVm.Passengers)
            {
                db.Passengers.Add(new Passenger
                {
                    BookingId = newBooking.Id,
                    Name = p.Name,
                    Age = p.Age,
                    SeatNumber = p.SeatNumber,
                    TicketType = p.TicketType
                });
            }
            db.SaveChanges();
            transaction.Commit();

            HttpContext.Session.Remove(SESSION_BOOKING_PROCESS_VM);
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            return Json(new { success = false, message = ex.Message });
        }
    }


    // 查看订单列表
    [HttpGet]
    public IActionResult BookingList()
    {
        var userEmail = User.Identity?.Name;
        if (string.IsNullOrEmpty(userEmail)) return RedirectToAction("Login", "Account");

        // 使用 LINQ 投影 (Projection) 直接填充你的新模型
        var bookings = db.Bookings
            .Include(b => b.Trip).ThenInclude(t => t.Route) // 确保预加载关联数据
            .Where(b => b.MemberEmail == userEmail)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BookingListVM
            {
                BookingId = b.Id,
                BookingReference = b.BookingReference,
                Status = b.Status,
                TotalAmount = b.TotalAmount,
                NumberOfSeats = b.NumberOfSeats,
                CreatedAt = b.CreatedAt,
                // 这里的导航属性必须在 DB.cs 中定义好 virtual
                Origin = b.Trip.Route.Origin,
                Destination = b.Trip.Route.Destination,
                DepartureTime = b.Trip.DepartureTime
            })
            .ToList();

        return View(bookings);
    }

    // 取消订单
    [HttpPost]
    public IActionResult CancelBooking(int id)
    {
        var booking = db.Bookings.Find(id);
        if (booking != null && booking.MemberEmail == User.Identity.Name)
        {
            // 将状态改为 Refund Pending，这样 Admin 才能看到审批按钮
            booking.Status = "Refund Pending";
            db.SaveChanges();

            TempData["RefundMessage"] = "Refund money will be processed in 1-3 working days.";
        }
        return RedirectToAction("BookingList");
    }


    [HttpGet]
    public IActionResult GetBookingDetails(int id)
    {
        var userEmail = User.Identity?.Name;

        // 💡 确保查询逻辑正确：根据 BookingId 查找该订单下的所有乘客姓名
        var passengers = db.Passengers
            .Include(p => p.Booking)
            .Where(p => p.BookingId == id && p.Booking.MemberEmail == userEmail)
            .Select(p => new {
                p.Name
            })
            .ToList();

        if (passengers == null || !passengers.Any()) return NotFound();

        return Json(passengers);
    }

}
