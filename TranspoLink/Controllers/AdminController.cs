using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Route = TranspoLink.Models.Route;

namespace TranspoLink.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController(DB db, Helper hp) : Controller
{
    // ============================================================================
    // DASHBOARD
    // ============================================================================

    // GET: Admin/Index
    public IActionResult Index()
    {
        // Statistics for dashboard
        ViewBag.TotalMembers = db.Members.Count();
        ViewBag.TotalRoutes = db.Routes.Count();
        ViewBag.TotalVehicles = db.Vehicles.Count();
        ViewBag.TotalBookings = db.Bookings.Count();
        ViewBag.PendingBookings = db.Bookings.Count(b => b.Status == "Pending");
        ViewBag.TodayBookings = db.Bookings.Count(b => b.BookingDate.Date == DateTime.Today);

        // Recent bookings
        var recentBookings = db.Bookings
            .Include(b => b.Member)
            .Include(b => b.Trip)
            .ThenInclude(t => t.Route)
            .OrderByDescending(b => b.BookingDate)
            .Take(10)
            .ToList();

        return View(recentBookings);
    }

    // ============================================================================
    // MEMBER MANAGEMENT
    // ============================================================================

    // GET: Admin/Members
    public IActionResult Members(string search = "", int page = 1, string sort = "Id", string dir = "asc")
    {
        var query = db.Members.AsQueryable();

        // 1. Search Logic
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(m =>
                m.Email.Contains(search) ||
                m.Phone.Contains(search) ||
                m.Name.Contains(search));
        }

        // 2. Sort Logic
        query = sort switch
        {
            "Name" => dir == "asc" ? query.OrderBy(m => m.Name) : query.OrderByDescending(m => m.Name),
            "Email" => dir == "asc" ? query.OrderBy(m => m.Email) : query.OrderByDescending(m => m.Email),
            "Phone" => dir == "asc" ? query.OrderBy(m => m.Phone) : query.OrderByDescending(m => m.Phone),
            _ => dir == "asc" ? query.OrderBy(m => m.Id) : query.OrderByDescending(m => m.Id)
        };

        ViewBag.Search = search;
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        // 3. Pagination
        int pageSize = 20;
        var members = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        ViewBag.TotalPages = (int)Math.Ceiling(query.Count() / (double)pageSize);
        ViewBag.CurrentPage = page;

        // 4. AJAX CHECK (Prevents duplication)
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            // Return ONLY the table HTML, not the whole layout
            return PartialView("_MemberTable", members);
        }

        return View(members);
    }

    // GET: Admin/MemberDetails/C001
    public IActionResult MemberDetails(string id) // CHANGED: int -> string
    {
        var member = db.Members
            .Include(m => m.Bookings)
            .ThenInclude(b => b.Trip)
            .ThenInclude(t => t.Route)
            .FirstOrDefault(m => m.Id == id);

        if (member == null)
        {
            TempData["Info"] = "Member not found.";
            return RedirectToAction("Members");
        }

        return View(member);
    }

    // POST: Admin/DeleteMember/C001
    [HttpPost]
    public IActionResult DeleteMember(string id) // CHANGED: int -> string
    {
        var member = db.Members.Find(id);

        if (member == null)
        {
            TempData["Info"] = "Member not found.";
            return RedirectToAction("Members");
        }

        // Check if member has bookings
        if (db.Bookings.Any(b => b.MemberId == member.Id))
        {
            TempData["Info"] = "Cannot delete member with existing bookings.";
            return RedirectToAction("Members");
        }

        // Delete photo if exists
        if (member.PhotoURL != null && member.PhotoURL != "/images/add_photo.png")
        {
            hp.DeletePhoto(member.PhotoURL, "photos");
        }

        db.Members.Remove(member);
        db.SaveChanges();

        TempData["Info"] = "Member deleted successfully.";
        return RedirectToAction("Members");
    }

    // ============================================================================
    // ROUTE MANAGEMENT
    // ============================================================================

    // GET: Admin/Routes
    public IActionResult Routes(string type = "")
    {
        var query = db.Routes.AsQueryable();

        if (!string.IsNullOrEmpty(type))
        {
            query = query.Where(r => r.TransportType == type);
        }

        var routes = query
            .OrderBy(r => r.Origin)
            .ThenBy(r => r.Destination)
            .ToList();

        ViewBag.SelectedType = type;

        return View(routes);
    }

    // GET: Admin/CreateRoute
    public IActionResult CreateRoute()
    {
        ViewBag.TransportTypes = new SelectList(new[] { "Bus", "Train", "Ferry" });
        return View();
    }

    // POST: Admin/CreateRoute
    [HttpPost]
    public IActionResult CreateRoute(Route route)
    {
        if (ModelState.IsValid)
        {
            db.Routes.Add(route);
            db.SaveChanges();

            TempData["Info"] = "Route created successfully.";
            return RedirectToAction("Routes");
        }

        ViewBag.TransportTypes = new SelectList(new[] { "Bus", "Train", "Ferry" });
        return View(route);
    }

    // GET: Admin/EditRoute/5
    public IActionResult EditRoute(int id)
    {
        var route = db.Routes.Find(id);

        if (route == null)
        {
            TempData["Info"] = "Route not found.";
            return RedirectToAction("Routes");
        }

        ViewBag.TransportTypes = new SelectList(new[] { "Bus", "Train", "Ferry" });
        return View(route);
    }

    // POST: Admin/EditRoute/5
    [HttpPost]
    public IActionResult EditRoute(Route route)
    {
        if (ModelState.IsValid)
        {
            db.Routes.Update(route);
            db.SaveChanges();

            TempData["Info"] = "Route updated successfully.";
            return RedirectToAction("Routes");
        }

        ViewBag.TransportTypes = new SelectList(new[] { "Bus", "Train", "Ferry" });
        return View(route);
    }

    // POST: Admin/DeleteRoute/5
    [HttpPost]
    public IActionResult DeleteRoute(int id)
    {
        var route = db.Routes.Find(id);

        if (route == null)
        {
            TempData["Info"] = "Route not found.";
            return RedirectToAction("Routes");
        }

        // Check if route has trips
        if (db.Trips.Any(t => t.RouteId == id))
        {
            TempData["Info"] = "Cannot delete route with existing trips.";
            return RedirectToAction("Routes");
        }

        db.Routes.Remove(route);
        db.SaveChanges();

        TempData["Info"] = "Route deleted successfully.";
        return RedirectToAction("Routes");
    }

    // POST: Admin/ToggleRouteStatus/5
    [HttpPost]
    public IActionResult ToggleRouteStatus(int id)
    {
        var route = db.Routes.Find(id);

        if (route != null)
        {
            route.IsActive = !route.IsActive;
            db.SaveChanges();

            TempData["Info"] = $"Route {(route.IsActive ? "activated" : "deactivated")} successfully.";
        }

        return RedirectToAction("Routes");
    }

    // ============================================================================
    // VEHICLE MANAGEMENT
    // ============================================================================

    // GET: Admin/Vehicles
    public IActionResult Vehicles(string type = "")
    {
        var query = db.Vehicles.AsQueryable();

        if (!string.IsNullOrEmpty(type))
        {
            query = query.Where(v => v.Type == type);
        }

        var vehicles = query
            .OrderBy(v => v.VehicleNumber)
            .ToList();

        ViewBag.SelectedType = type;

        return View(vehicles);
    }

    // GET: Admin/CreateVehicle
    public IActionResult CreateVehicle()
    {
        ViewBag.VehicleTypes = new SelectList(new[] { "Bus", "Train", "Ferry" });
        return View();
    }

    // POST: Admin/CreateVehicle
    [HttpPost]
    public IActionResult CreateVehicle(Vehicle vehicle)
    {
        // Check for duplicate vehicle number
        if (db.Vehicles.Any(v => v.VehicleNumber == vehicle.VehicleNumber))
        {
            ModelState.AddModelError("VehicleNumber", "Vehicle number already exists.");
        }

        if (ModelState.IsValid)
        {
            db.Vehicles.Add(vehicle);
            db.SaveChanges();

            TempData["Info"] = "Vehicle created successfully.";
            return RedirectToAction("Vehicles");
        }

        ViewBag.VehicleTypes = new SelectList(new[] { "Bus", "Train", "Ferry" });
        return View(vehicle);
    }

    // GET: Admin/EditVehicle/5
    public IActionResult EditVehicle(int id)
    {
        var vehicle = db.Vehicles.Find(id);

        if (vehicle == null)
        {
            TempData["Info"] = "Vehicle not found.";
            return RedirectToAction("Vehicles");
        }

        ViewBag.VehicleTypes = new SelectList(new[] { "Bus", "Train", "Ferry" });
        return View(vehicle);
    }

    // POST: Admin/EditVehicle/5
    [HttpPost]
    public IActionResult EditVehicle(Vehicle vehicle)
    {
        // Check for duplicate vehicle number (excluding current)
        if (db.Vehicles.Any(v => v.VehicleNumber == vehicle.VehicleNumber && v.Id != vehicle.Id))
        {
            ModelState.AddModelError("VehicleNumber", "Vehicle number already exists.");
        }

        if (ModelState.IsValid)
        {
            db.Vehicles.Update(vehicle);
            db.SaveChanges();

            TempData["Info"] = "Vehicle updated successfully.";
            return RedirectToAction("Vehicles");
        }

        ViewBag.VehicleTypes = new SelectList(new[] { "Bus", "Train", "Ferry" });
        return View(vehicle);
    }

    // POST: Admin/DeleteVehicle/5
    [HttpPost]
    public IActionResult DeleteVehicle(int id)
    {
        var vehicle = db.Vehicles.Find(id);

        if (vehicle == null)
        {
            TempData["Info"] = "Vehicle not found.";
            return RedirectToAction("Vehicles");
        }

        // Check if vehicle has trips
        if (db.Trips.Any(t => t.VehicleId == id))
        {
            TempData["Info"] = "Cannot delete vehicle with existing trips.";
            return RedirectToAction("Vehicles");
        }

        db.Vehicles.Remove(vehicle);
        db.SaveChanges();

        TempData["Info"] = "Vehicle deleted successfully.";
        return RedirectToAction("Vehicles");
    }

    // POST: Admin/ToggleVehicleStatus/5
    [HttpPost]
    public IActionResult ToggleVehicleStatus(int id)
    {
        var vehicle = db.Vehicles.Find(id);

        if (vehicle != null)
        {
            vehicle.IsActive = !vehicle.IsActive;
            db.SaveChanges();

            TempData["Info"] = $"Vehicle {(vehicle.IsActive ? "activated" : "deactivated")} successfully.";
        }

        return RedirectToAction("Vehicles");
    }

    // ============================================================================
    // TRIP MANAGEMENT
    // ============================================================================

    // GET: Admin/Trips
    public IActionResult Trips(string status = "", int page = 1)
    {
        var query = db.Trips
            .Include(t => t.Route)
            .Include(t => t.Vehicle)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(t => t.Status == status);
        }

        ViewBag.SelectedStatus = status;

        // Pagination
        int pageSize = 20;
        var trips = query
            .OrderByDescending(t => t.DepartureTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        ViewBag.TotalPages = (int)Math.Ceiling(query.Count() / (double)pageSize);
        ViewBag.CurrentPage = page;

        return View(trips);
    }

    // GET: Admin/CreateTrip
    public IActionResult CreateTrip()
    {
        ViewBag.RouteList = new SelectList(db.Routes.Where(r => r.IsActive).OrderBy(r => r.Origin), "Id", "Origin");
        ViewBag.VehicleList = new SelectList(db.Vehicles.Where(v => v.IsActive).OrderBy(v => v.VehicleNumber), "Id", "VehicleNumber");
        ViewBag.StatusList = new SelectList(new[] { "Scheduled", "InProgress", "Completed", "Cancelled" });
        return View();
    }

    // POST: Admin/CreateTrip
    [HttpPost]
    public IActionResult CreateTrip(Trip trip)
    {
        // Validation: Arrival must be after departure
        if (trip.ArrivalTime <= trip.DepartureTime)
        {
            ModelState.AddModelError("ArrivalTime", "Arrival time must be after departure time.");
        }

        // Set available seats from vehicle if not specified
        if (trip.AvailableSeats == 0)
        {
            var vehicle = db.Vehicles.Find(trip.VehicleId);
            if (vehicle != null)
            {
                trip.AvailableSeats = vehicle.TotalSeats;
            }
        }

        if (ModelState.IsValid)
        {
            db.Trips.Add(trip);
            db.SaveChanges();

            TempData["Info"] = "Trip created successfully.";
            return RedirectToAction("Trips");
        }

        ViewBag.RouteList = new SelectList(db.Routes.Where(r => r.IsActive), "Id", "Origin");
        ViewBag.VehicleList = new SelectList(db.Vehicles.Where(v => v.IsActive), "Id", "VehicleNumber");
        ViewBag.StatusList = new SelectList(new[] { "Scheduled", "InProgress", "Completed", "Cancelled" });
        return View(trip);
    }

    // GET: Admin/EditTrip/5
    public IActionResult EditTrip(int id)
    {
        var trip = db.Trips.Find(id);

        if (trip == null)
        {
            TempData["Info"] = "Trip not found.";
            return RedirectToAction("Trips");
        }

        ViewBag.RouteList = new SelectList(db.Routes, "Id", "Origin", trip.RouteId);
        ViewBag.VehicleList = new SelectList(db.Vehicles, "Id", "VehicleNumber", trip.VehicleId);
        ViewBag.StatusList = new SelectList(new[] { "Scheduled", "InProgress", "Completed", "Cancelled" }, trip.Status);
        return View(trip);
    }

    // POST: Admin/EditTrip/5
    [HttpPost]
    public IActionResult EditTrip(Trip trip)
    {
        if (trip.ArrivalTime <= trip.DepartureTime)
        {
            ModelState.AddModelError("ArrivalTime", "Arrival time must be after departure time.");
        }

        if (ModelState.IsValid)
        {
            db.Trips.Update(trip);
            db.SaveChanges();

            TempData["Info"] = "Trip updated successfully.";
            return RedirectToAction("Trips");
        }

        ViewBag.RouteList = new SelectList(db.Routes, "Id", "Origin", trip.RouteId);
        ViewBag.VehicleList = new SelectList(db.Vehicles, "Id", "VehicleNumber", trip.VehicleId);
        ViewBag.StatusList = new SelectList(new[] { "Scheduled", "InProgress", "Completed", "Cancelled" }, trip.Status);
        return View(trip);
    }

    // POST: Admin/DeleteTrip/5
    [HttpPost]
    public IActionResult DeleteTrip(int id)
    {
        var trip = db.Trips.Find(id);

        if (trip == null)
        {
            TempData["Info"] = "Trip not found.";
            return RedirectToAction("Trips");
        }

        // Check if trip has bookings
        if (db.Bookings.Any(b => b.TripId == id))
        {
            TempData["Info"] = "Cannot delete trip with existing bookings.";
            return RedirectToAction("Trips");
        }

        db.Trips.Remove(trip);
        db.SaveChanges();

        TempData["Info"] = "Trip deleted successfully.";
        return RedirectToAction("Trips");
    }

    // ============================================================================
    // BOOKING MANAGEMENT
    // ============================================================================

    // GET: Admin/Bookings
    public IActionResult Bookings(string status = "", int page = 1)
    {
        var query = db.Bookings
            .Include(b => b.Member)
            .Include(b => b.Trip)
            .ThenInclude(t => t.Route)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(b => b.Status == status);
        }

        ViewBag.SelectedStatus = status;

        // Pagination
        int pageSize = 20;
        var bookings = query
            .OrderByDescending(b => b.BookingDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        ViewBag.TotalPages = (int)Math.Ceiling(query.Count() / (double)pageSize);
        ViewBag.CurrentPage = page;

        return View(bookings);
    }

    // GET: Admin/BookingDetails/5
    public IActionResult BookingDetails(int id)
    {
        var booking = db.Bookings
            .Include(b => b.Member)
            .Include(b => b.Trip)
            .ThenInclude(t => t.Route)
            .Include(b => b.Trip)
            .ThenInclude(t => t.Vehicle)
            .FirstOrDefault(b => b.Id == id);

        if (booking == null)
        {
            TempData["Info"] = "Booking not found.";
            return RedirectToAction("Bookings");
        }

        return View(booking);
    }

    // POST: Admin/ConfirmBooking/5
    [HttpPost]
    public IActionResult ConfirmBooking(int id)
    {
        var booking = db.Bookings.Find(id);

        if (booking != null && booking.Status == "Pending")
        {
            booking.Status = "Confirmed";
            db.SaveChanges();

            TempData["Info"] = "Booking confirmed successfully.";
        }

        return RedirectToAction("BookingDetails", new { id });
    }

    // POST: Admin/CancelBooking/5
    [HttpPost]
    public IActionResult CancelBooking(int id)
    {
        var booking = db.Bookings
            .Include(b => b.Trip)
            .FirstOrDefault(b => b.Id == id);

        if (booking != null && booking.Status != "Cancelled")
        {
            booking.Status = "Cancelled";

            // Return seats to trip
            booking.Trip.AvailableSeats += booking.NumberOfSeats;

            db.SaveChanges();

            TempData["Info"] = "Booking cancelled successfully.";
        }

        return RedirectToAction("BookingDetails", new { id });
    }

    // POST: Admin/MarkAsPaid/5
    [HttpPost]
    public IActionResult MarkAsPaid(int id)
    {
        var booking = db.Bookings.Find(id);

        if (booking != null && !booking.IsPaid)
        {
            booking.IsPaid = true;
            //Date = DateTime.Now;
            db.SaveChanges();

            TempData["Info"] = "Booking marked as paid.";
        }

        return RedirectToAction("BookingDetails", new { id });
    }

    // ============================================================================
    // REPORTS & ANALYTICS
    // ============================================================================

    // GET: Admin/Reports
    public IActionResult Reports()
    {
        // Revenue by transport type
        var revenueByType = db.Bookings
            .Include(b => b.Trip)
            .ThenInclude(t => t.Route)
            .Where(b => b.IsPaid)
            .GroupBy(b => b.Trip.Route.TransportType)
            .Select(g => new
            {
                Type = g.Key,
                Revenue = g.Sum(b => b.TotalPrice),
                Count = g.Count()
            })
            .ToList();

        ViewBag.RevenueByType = revenueByType;

        // Top routes
        var topRoutes = db.Bookings
            .Include(b => b.Trip)
            .ThenInclude(t => t.Route)
            .GroupBy(b => new { b.Trip.Route.Origin, b.Trip.Route.Destination })
            .Select(g => new
            {
                Route = g.Key.Origin + " → " + g.Key.Destination,
                BookingCount = g.Count(),
                Revenue = g.Sum(b => b.TotalPrice)
            })
            .OrderByDescending(x => x.BookingCount)
            .Take(10)
            .ToList();

        ViewBag.TopRoutes = topRoutes;

        // Monthly revenue
        var monthlyRevenue = db.Bookings
            .Where(b => b.IsPaid && b.BookingDate.Year == DateTime.Now.Year)
            .GroupBy(b => b.BookingDate.Month)
            .Select(g => new
            {
                Month = g.Key,
                Revenue = g.Sum(b => b.TotalPrice)
            })
            .OrderBy(x => x.Month)
            .ToList();

        ViewBag.MonthlyRevenue = monthlyRevenue;

        return View();
    }
}