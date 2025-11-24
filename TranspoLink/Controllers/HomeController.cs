using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Demo.Controllers;

public class HomeController(DB db) : Controller
{
    // GET: Home/Index
    public IActionResult Index()
    {
        ViewBag.Members = db.Members;
        ViewBag.Types   = db.Types.OrderBy(t => t.Price);
        ViewBag.Rooms   = db.Rooms.OrderBy(rm => rm.Id);
        return View();
    }



    // ------------------------------------------------------------------------
    // Reservation
    // ------------------------------------------------------------------------

    // GET: Home/Reserve
    [Authorize(Roles = "Member")]
    public IActionResult Reserve()
    {
        var vm = new ReserveVM
        {
            // TODO
            CheckIn = DateTime.Today.ToDateOnly(),
            CheckOut = DateTime.Today.ToDateOnly().AddDays(1),
        };

        // TODO
        ViewBag.TypeList = new SelectList(db.Types.OrderBy(t => t.Price), "Id", "Name");
        return View(vm);
    }

    // POST: Home/Reserve
    [Authorize(Roles = "Member")]
    [HttpPost]
    public IActionResult Reserve(ReserveVM vm)
    {
        // Validation (1): CheckIn within 30 days range
        if (ModelState.IsValid("CheckIn"))
        {
            // TODO
            var a = DateTime.Today.ToDateOnly();
            var b = DateTime.Today.ToDateOnly().AddDays(30);

            if (vm.CheckIn < a || vm.CheckIn > b)
            {
                ModelState.AddModelError("CheckIn", "Date out of range.");
            }
        }

        // Validation (2): CheckOut within 10 days range (after CheckIn)
        if (ModelState.IsValid("CheckIn") && ModelState.IsValid("CheckOut"))
        {
            // TODO
            var a = vm.CheckIn.AddDays(1);
            var b = vm.CheckIn.AddDays(10);

            if (vm.CheckOut <a || vm.CheckOut > b)
            {
                ModelState.AddModelError("CheckOut", "Date out of range.");
            }
        }
        
        if (ModelState.IsValid)
        {
            // 1. Get occupied rooms 
            // TODO
            var occupied = db.Reservations
                          .Where(rs => vm.CheckIn < rs.CheckOut && rs.CheckIn < vm.CheckOut)
                          .Select(rs => rs.Room);

            // 2. Get first available room (filtered by room type)
            // TODO
            Room? room = db.Rooms.Except(occupied).Include(rm => rm.Type).FirstOrDefault(rm => rm.TypeId == vm.TypeId);
           // Room? room = db.Rooms
            //            .Include(rm => rm.Type)
              //          .Where(rm => rm.TypeId == vm.TypeId && !occupied.Contains(rm.Id))
                //        .FirstOrDefault();

            // 3. Is room available?
            if (room == null)
            {
                ModelState.AddModelError("TypeId", "No room availble.");
            }
            else
            {
                // 4. Insert Reservation record
                // TODO
                var rs = new Reservation
                {
                    MemberEmail = User.Identity!.Name!,
                    RoomId      = room.Id,
                    CheckIn     = vm.CheckIn,
                    CheckOut    = vm.CheckOut,
                    Price       = room.Type.Price,
                    Paid        = false,
                };
                db.Reservations.Add(rs);
                db.SaveChanges();

                TempData["Info"] = "Room reserved.";
                return RedirectToAction("Detail", new { rs.Id });
            }
        }

        ViewBag.TypeList = new SelectList(db.Types.OrderBy(t => t.Price), "Id", "Name");
        return View(vm);
    }

    // GET: Home/List
    public IActionResult List()
    {
        var m = db.Reservations
                  .Include(rs => rs.Member)
                  .Include(rs => rs.Room)
                  .ThenInclude(rm => rm.Type);

        return View(m);
    }

    // POST: Home/Reset
    [HttpPost]
    public IActionResult Reset()
    {
        db.Database.ExecuteSqlRaw(@"
            TRUNCATE TABLE [Reservations];
        ");

        return RedirectToAction("List");
    }

    // GET: Home/Detail
    public IActionResult Detail(int id)
    {
        var m = db.Reservations
                  .Include(rs => rs.Member)
                  .Include(rs => rs.Room)
                  .ThenInclude(rm => rm.Type)
                  .FirstOrDefault(rs => rs.Id == id);

        if (m == null) 
        {
            return RedirectToAction("List");
        }

        return View(m);
    }

    // GET: Home/Status
    public IActionResult Status(DateOnly? month)
    {
        var m = month.GetValueOrDefault(DateTime.Today.ToDateOnly());

        // Min = First day of the month
        // Max = First day of next month
        
        // TODO
        var min = new DateOnly(m.Year, m.Month, 1);
        var max = min.AddMonths(1);

        ViewBag.Min = min;
        ViewBag.Max = max;

        // 1. Initialize dictionary
        // ------------------------
        // Dictionary<Room, List<DateOnly>>
        // Key   = Room object
        // Value = List of DateOnly objects
        //
        // dict[R001] = [2022-12-01, 2022-12-02, ...]
        // dict[R002] = [2022-12-03, 2022-12-04, ...]

        // TODO
        var dict = db.Rooms
                  .OrderBy(rm => rm.Id)
                  .ToDictionary(rm => rm, rm => new List<DateOnly>());

        // 2. Retrieve reservation records
        // -------------------------------
        // Example: 2024-12-01 (min) ... 2025-01-01 (max)

        // TODO
        var reservations = db.Reservations
                           .Where(rs => min < rs.CheckOut && rs.CheckIn < max);

        // 3. Fill the dictionary
        // ----------------------
        // Example: CheckIn = 2024-12-10, CheckOut = 2024-12-15
        // Entries --> 10, 11, 12, 13, 14 *** 15 not included ***

        // TODO
        foreach (var rs in reservations)
        {
            for(var d = rs.CheckIn; d < rs.CheckOut; d = d.AddDays(1))
            {
                dict[rs.Room].Add(d);
            }
        }

        // TODO
        return View(dict);
    }
}
