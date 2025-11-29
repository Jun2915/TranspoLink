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
    // ADMIN MANAGEMENT
    // ============================================================================

    // GET: Admin/Admins
    public IActionResult Admins(string search = "", int page = 1, string sort = "Id", string dir = "asc")
    {
        // 1. Identify Current User & System Admin Status
        var currentUser = db.Users.FirstOrDefault(u => u.Email == User.Identity.Name || u.Phone == User.Identity.Name);
        ViewBag.CurrentUserId = currentUser?.Id;
        ViewBag.IsSystemAdmin = currentUser?.Id == "A001";

        var query = db.Admins.AsQueryable();

        // 2. Search Logic
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(a =>
                a.Id.Contains(search) ||
                a.Name.Contains(search) ||
                a.Email.Contains(search) ||
                a.Phone.Contains(search));
        }

        // 3. Sort Logic
        query = sort switch
        {
            "Name" => dir == "asc" ? query.OrderBy(a => a.Name) : query.OrderByDescending(a => a.Name),
            "Email" => dir == "asc" ? query.OrderBy(a => a.Email) : query.OrderByDescending(a => a.Email),
            "Phone" => dir == "asc" ? query.OrderBy(a => a.Phone) : query.OrderByDescending(a => a.Phone),
            _ => dir == "asc" ? query.OrderBy(a => a.Id) : query.OrderByDescending(a => a.Id)
        };

        ViewBag.Search = search;
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        // 4. Pagination
        int pageSize = 10;
        var admins = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        ViewBag.TotalPages = (int)Math.Ceiling(query.Count() / (double)pageSize);
        ViewBag.CurrentPage = page;

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return PartialView("_AdminTable", admins);
        }

        return View(admins);
    }

    // GET: Admin/CreateAdmin
    public IActionResult CreateAdmin()
    {
        var currentUser = db.Users.FirstOrDefault(u => u.Email == User.Identity.Name || u.Phone == User.Identity.Name);
        if (currentUser?.Id != "A001")
        {
            TempData["Info"] = "Access Denied: Only System Administrator can create new admins.";
            return RedirectToAction("Admins");
        }

        return View();
    }

    // POST: Admin/CreateAdmin
    [HttpPost]
    public IActionResult CreateAdmin(AdminVM vm)
    {
        var currentUser = db.Users.FirstOrDefault(u => u.Email == User.Identity.Name || u.Phone == User.Identity.Name);
        if (currentUser?.Id != "A001")
        {
            return RedirectToAction("Admins");
        }

        if (string.IsNullOrEmpty(vm.Password))
        {
            ModelState.AddModelError("Password", "Password is required.");
        }

        if (db.Users.Any(u => u.Email == vm.Email))
            ModelState.AddModelError("Email", "Email already in use.");

        if (db.Users.Any(u => u.Phone == vm.Phone))
            ModelState.AddModelError("Phone", "Phone number already in use.");

        if (vm.Photo != null)
        {
            var err = hp.ValidatePhoto(vm.Photo);
            if (err != "") ModelState.AddModelError("Photo", err);
        }

        if (ModelState.IsValid)
        {
            var newAdmin = new Admin
            {
                Id = hp.GetNextId(db, "Admin"),
                Name = vm.Name,
                Email = vm.Email,
                Phone = vm.Phone,
                Hash = hp.HashPassword(vm.Password),
                PhotoURL = vm.Photo != null ? hp.SavePhoto(vm.Photo, "images") : "beauty_admin.png",
                IsBlocked = false
            };

            db.Admins.Add(newAdmin);
            db.SaveChanges();

            TempData["Info"] = "New Admin account created successfully.";
            return RedirectToAction("Admins");
        }

        return View(vm);
    }

    // GET: Admin/AdminDetails/A002
    public IActionResult AdminDetails(string id)
    {
        var admin = db.Admins.Find(id);
        if (admin == null)
        {
            TempData["Info"] = "Admin not found.";
            return RedirectToAction("Admins");
        }

        var timeline = db.AuditLogs
        .Where(l => l.AdminId == id)
        .OrderByDescending(l => l.Timestamp) 
        .Take(10) 
        .Select(l => new TimelineItemVM
        {
            Title = l.Action,
            Time = l.Timestamp.ToString("dd MMM yyyy, hh:mm tt") + " - " + l.Details,
            Icon = l.Icon,
            CssClass = l.CssClass
        })
        .ToList();

        if (!timeline.Any())
        {
            timeline.Add(new TimelineItemVM
            {
                Title = "Account Created",
                Time = "No recent activity recorded.",
                Icon = "✨",
                CssClass = "marker-add"
            });
        }

        ViewBag.Timeline = timeline;
        // =========================================================

        return View(admin);
    }

    // POST: Admin/ToggleBlockAdmin/A002 (NEW)
    [HttpPost]
    public IActionResult ToggleBlockAdmin(string id)
    {
        var currentUser = db.Users.FirstOrDefault(u => u.Email == User.Identity.Name || u.Phone == User.Identity.Name);

        // Security Check
        if (currentUser?.Id != "A001")
        {
            TempData["Info"] = "Access Denied: Only System Administrator can block admins.";
            return RedirectToAction("Admins");
        }

        if (id == "A001")
        {
            TempData["Info"] = "System Administrator cannot be blocked.";
            return RedirectToAction("AdminDetails", new { id });
        }

        var admin = db.Admins.Find(id);
        if (admin == null)
        {
            TempData["Info"] = "Admin not found.";
            return RedirectToAction("Admins");
        }

        admin.IsBlocked = !admin.IsBlocked;
        db.SaveChanges();

        var currentAdmin = db.Users.FirstOrDefault(u => u.Email == User.Identity.Name || u.Phone == User.Identity.Name);

        if (currentAdmin != null)
        {
            string actionName = admin.IsBlocked ? "Blocked Admin" : "Unblocked Admin";
            string icon = admin.IsBlocked ? "🔒" : "🔓";
            string css = admin.IsBlocked ? "marker-block" : "marker-login";

            hp.LogActivity(db, currentAdmin.Id, actionName, $"Target: {admin.Name} ({admin.Id})", icon, css);
        }

        string status = admin.IsBlocked ? "blocked" : "unblocked";
        TempData["Info"] = $"Admin {admin.Name} has been {status}.";

        return RedirectToAction("AdminDetails", new { id = admin.Id });
    }

    // GET: Admin/EditAdmin/A001
    public IActionResult EditAdmin(string id)
    {
        var currentUser = db.Users.FirstOrDefault(u => u.Email == User.Identity.Name || u.Phone == User.Identity.Name);
        if (currentUser?.Id != "A001")
        {
            TempData["Info"] = "Access Denied.";
            return RedirectToAction("Admins");
        }

        var admin = db.Admins.Find(id);
        if (admin == null) return RedirectToAction("Admins");

        var vm = new AdminVM
        {
            Id = admin.Id,
            Name = admin.Name,
            Email = admin.Email,
            Phone = admin.Phone,
            ExistingPhotoURL = admin.PhotoURL,
            IsBlocked = admin.IsBlocked
        };

        return View(vm);
    }

    // POST: Admin/EditAdmin
    [HttpPost]
    public IActionResult EditAdmin(AdminVM vm)
    {
        var currentUser = db.Users.FirstOrDefault(u => u.Email == User.Identity.Name || u.Phone == User.Identity.Name);
        if (currentUser?.Id != "A001") return RedirectToAction("Admins");

        var admin = db.Admins.Find(vm.Id);
        if (admin == null) return RedirectToAction("Admins");

        if (db.Users.Any(u => u.Email == vm.Email && u.Id != vm.Id))
            ModelState.AddModelError("Email", "Email already in use.");

        if (db.Users.Any(u => u.Phone == vm.Phone && u.Id != vm.Id))
            ModelState.AddModelError("Phone", "Phone number already in use.");

        if (vm.Photo != null)
        {
            var err = hp.ValidatePhoto(vm.Photo);
            if (err != "") ModelState.AddModelError("Photo", err);
        }

        if (ModelState.IsValid)
        {
            admin.Name = vm.Name;
            admin.Email = vm.Email;
            admin.Phone = vm.Phone;
            admin.IsBlocked = vm.IsBlocked;

            if (!string.IsNullOrEmpty(vm.Password))
            {
                admin.Hash = hp.HashPassword(vm.Password);
            }

            if (vm.Photo != null)
            {
                if (!string.IsNullOrEmpty(admin.PhotoURL) && !admin.PhotoURL.StartsWith("/images/") && admin.PhotoURL != "beauty_admin.png")
                {
                    hp.DeletePhoto(admin.PhotoURL, "images");
                }
                admin.PhotoURL = hp.SavePhoto(vm.Photo, "images");
            }

            db.SaveChanges();
            TempData["Info"] = "Admin details updated.";
            return RedirectToAction("Admins");
        }

        vm.ExistingPhotoURL = admin.PhotoURL;
        return View(vm);
    }

    // POST: Admin/DeleteAdmin/A001
    [HttpPost]
    public IActionResult DeleteAdmin(string id)
    {
        var currentUser = db.Users.FirstOrDefault(u => u.Email == User.Identity.Name || u.Phone == User.Identity.Name);

        if (currentUser?.Id != "A001")
        {
            TempData["Info"] = "Access Denied: Only System Administrator can delete admins.";
            return RedirectToAction("Admins");
        }

        if (id == "A001")
        {
            TempData["Info"] = "System Administrator cannot be deleted.";
            return RedirectToAction("Admins");
        }

        var admin = db.Admins.Find(id);
        if (admin != null)
        {
            if (admin.PhotoURL != null && admin.PhotoURL != "beauty_admin.png" && !admin.PhotoURL.StartsWith("/images/"))
            {
                hp.DeletePhoto(admin.PhotoURL, "images");
            }

            db.Admins.Remove(admin);
            db.SaveChanges();
            TempData["Info"] = "Admin account deleted.";
        }
        return RedirectToAction("Admins");
    }

    // ============================================================================
    // MEMBER MANAGEMENT
    // ============================================================================

    public IActionResult Members(string search = "", int page = 1, string sort = "Id", string dir = "asc")
    {
        var query = db.Members.AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(m =>
                m.Id.Contains(search) ||
                m.Name.Contains(search) ||
                m.Email.Contains(search) ||
                m.Phone.Contains(search));
        }

        query = sort switch
        {
            "Name" => dir == "asc" ? query.OrderBy(m => m.Name) : query.OrderByDescending(m => m.Name),
            "Email" => dir == "asc" ? query.OrderBy(m => m.Email) : query.OrderByDescending(m => m.Email),
            "Phone" => dir == "asc" ? query.OrderBy(m => m.Phone) : query.OrderByDescending(m => m.Phone),
            "Status" => dir == "asc" ? query.OrderBy(m => m.IsBlocked) : query.OrderByDescending(m => m.IsBlocked),
            _ => dir == "asc" ? query.OrderBy(m => m.Id) : query.OrderByDescending(m => m.Id)
        };

        ViewBag.Search = search;
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        int pageSize = 10;
        var members = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        ViewBag.TotalPages = (int)Math.Ceiling(query.Count() / (double)pageSize);
        ViewBag.CurrentPage = page;

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return PartialView("_MemberTable", members);
        }

        return View(members);
    }

    public IActionResult MemberDetails(string id)
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

    [HttpPost]
    public IActionResult DeleteMember(string id)
    {
        var member = db.Members.Find(id);

        if (member == null)
        {
            TempData["Info"] = "Member not found.";
            return RedirectToAction("Members");
        }

        if (db.Bookings.Any(b => b.MemberId == member.Id))
        {
            TempData["Info"] = "Cannot delete member with existing bookings.";
            return RedirectToAction("Members");
        }

        if (member.PhotoURL != null && member.PhotoURL != "/images/add_photo.png")
        {
            hp.DeletePhoto(member.PhotoURL, "photos");
        }

        db.Members.Remove(member);
        db.SaveChanges();

        TempData["Info"] = "Member deleted successfully.";
        return RedirectToAction("Members");
    }

    [HttpPost]
    public IActionResult ToggleBlockMember(string id)
    {
        var member = db.Members.Find(id);
        if (member == null)
        {
            TempData["Info"] = "Member not found.";
            return RedirectToAction("Members");
        }

        member.IsBlocked = !member.IsBlocked;
        db.SaveChanges();

        string status = member.IsBlocked ? "blocked" : "unblocked";
        TempData["Info"] = $"Member {member.Name} has been {status}.";

        return RedirectToAction("MemberDetails", new { id = member.Id });
    }

    // ============================================================================
    // ROUTE MANAGEMENT
    // ============================================================================

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

    public IActionResult CreateRoute()
    {
        ViewBag.TransportTypes = new SelectList(new[] { "Bus", "Train", "Ferry" });
        return View();
    }

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

    [HttpPost]
    public IActionResult DeleteRoute(int id)
    {
        var route = db.Routes.Find(id);

        if (route == null)
        {
            TempData["Info"] = "Route not found.";
            return RedirectToAction("Routes");
        }

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

    public IActionResult CreateVehicle()
    {
        ViewBag.VehicleTypes = new SelectList(new[] { "Bus", "Train", "Ferry" });
        return View();
    }

    [HttpPost]
    public IActionResult CreateVehicle(Vehicle vehicle)
    {
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

    [HttpPost]
    public IActionResult EditVehicle(Vehicle vehicle)
    {
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

    [HttpPost]
    public IActionResult DeleteVehicle(int id)
    {
        var vehicle = db.Vehicles.Find(id);

        if (vehicle == null)
        {
            TempData["Info"] = "Vehicle not found.";
            return RedirectToAction("Vehicles");
        }

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

    public IActionResult CreateTrip()
    {
        ViewBag.RouteList = new SelectList(db.Routes.Where(r => r.IsActive).OrderBy(r => r.Origin), "Id", "Origin");
        ViewBag.VehicleList = new SelectList(db.Vehicles.Where(v => v.IsActive).OrderBy(v => v.VehicleNumber), "Id", "VehicleNumber");
        ViewBag.StatusList = new SelectList(new[] { "Scheduled", "InProgress", "Completed", "Cancelled" });
        return View();
    }

    [HttpPost]
    public IActionResult CreateTrip(Trip trip)
    {
        if (trip.ArrivalTime <= trip.DepartureTime)
        {
            ModelState.AddModelError("ArrivalTime", "Arrival time must be after departure time.");
        }

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

    [HttpPost]
    public IActionResult DeleteTrip(int id)
    {
        var trip = db.Trips.Find(id);

        if (trip == null)
        {
            TempData["Info"] = "Trip not found.";
            return RedirectToAction("Trips");
        }

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

    [HttpPost]
    public IActionResult CancelBooking(int id)
    {
        var booking = db.Bookings
            .Include(b => b.Trip)
            .FirstOrDefault(b => b.Id == id);

        if (booking != null && booking.Status != "Cancelled")
        {
            booking.Status = "Cancelled";
            booking.Trip.AvailableSeats += booking.NumberOfSeats;
            db.SaveChanges();

            TempData["Info"] = "Booking cancelled successfully.";
        }

        return RedirectToAction("BookingDetails", new { id });
    }

    [HttpPost]
    public IActionResult MarkAsPaid(int id)
    {
        var booking = db.Bookings.Find(id);

        if (booking != null && !booking.IsPaid)
        {
            booking.IsPaid = true;
            db.SaveChanges();

            TempData["Info"] = "Booking marked as paid.";
        }

        return RedirectToAction("BookingDetails", new { id });
    }

    // ============================================================================
    // REPORTS & ANALYTICS
    // ============================================================================

    public IActionResult Reports()
    {
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