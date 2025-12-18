using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Route = TranspoLink.Models.Route;
using TranspoLink.Models;
using System.Linq;
using System; // Ensure this is available for Exception handling

namespace TranspoLink.Controllers;

// NOTE: Assumes you have a DriverVM model and Helper class (hp) defined elsewhere.
public class RouteNTripController(DB db, Helper hp) : Controller
{
    // ============================================================================
    // PRIVATE ID GENERATION HELPERS
    // ============================================================================

    private string GetNextTripId(DB db)
    {
        var lastId = db.Trips
            .Select(t => t.Id)
            .Where(id => id.StartsWith("T") && id.Length == 5)
            .OrderByDescending(id => id)
            .FirstOrDefault();
        if (string.IsNullOrEmpty(lastId))
            return "T0001";
        if (lastId.Length == 5 && int.TryParse(lastId.Substring(1), out int num))
            return "T" + (num + 1).ToString("D4");
        return "T0001";
    }

    private string GetNextRouteStopId(DB db)
    {
        var lastId = db.RouteStops
            .Select(rs => rs.Id)
            .Where(id => id.StartsWith("S") && id.Length == 6)
            .OrderByDescending(id => id)
            .FirstOrDefault();
        if (string.IsNullOrEmpty(lastId))
            return "S00001";
        if (lastId.Length == 6 && int.TryParse(lastId.Substring(1), out int num))
            return "S" + (num + 1).ToString("D5");
        return "S00001";
    }

    private string GetNextTripStopId(DB db)
    {
        // MaxLength(7)
        var lastId = db.TripStops
            .Select(ts => ts.Id)
            .Where(id => id.StartsWith("TS") && id.Length == 7)
            .OrderByDescending(id => id)
            .FirstOrDefault();
        if (string.IsNullOrEmpty(lastId))
            return "TS00001";
        if (lastId.Length == 7 && int.TryParse(lastId.Substring(2), out int num))
            return "TS" + (num + 1).ToString("D5");
        return "TS00001";
    }


    private string GetNextDriverId(DB db)
    {
        var lastId = db.Drivers.OrderByDescending(d => d.Id).Select(d => d.Id).FirstOrDefault();
        if (string.IsNullOrEmpty(lastId))
            return "D001";
        if (int.TryParse(lastId.Substring(1), out int num))
            return "D" + (num + 1).ToString("D3");
        return "D001";
    }

    // ============================================================================
    // PUBLIC SEARCH ACTIONS
    // ============================================================================

    [HttpGet]
    public IActionResult GetLocations()
    {
        var origins = db.Routes.Where(r => r.IsActive).Select(r => r.Origin).Distinct();
        var destinations = db.Routes.Where(r => r.IsActive).Select(r => r.Destination).Distinct();
        var locations = origins.Union(destinations).Distinct().OrderBy(x => x).ToList();
        return Json(locations);
    }

    [Authorize]
    [AcceptVerbs("GET", "POST")]
    public IActionResult SearchTrip(string origin, string destination, DateTime departDate, string transportType = "Bus")
    {
        if (string.IsNullOrEmpty(origin) || string.IsNullOrEmpty(destination))
        {
            TempData["Info"] = "Please select an Origin and Destination.";
            return RedirectToAction("Index", "Home");
        }

        var query = db.Trips
            .Include(t => t.Route)
            .Include(t => t.Vehicle)
            .Where(t => t.Status == "Scheduled")
            .AsQueryable();

        if (!string.IsNullOrEmpty(transportType))
            query = query.Where(t => t.Route.TransportType == transportType);

        query = query.Where(t => t.Route.Origin == origin && t.Route.Destination == destination);
        query = query.Where(t => t.DepartureTime.Date == departDate.Date);

        var results = query.OrderBy(t => t.DepartureTime).ToList();

        ViewBag.SearchOrigin = origin;
        ViewBag.SearchDest = destination;
        ViewBag.SearchDate = departDate.ToString("dd MMM yyyy");
        ViewBag.TransportType = transportType;

        return View(results);
    }


    // ============================================================================
    // ADMIN MANAGEMENT ACTIONS ( Route )
    // ============================================================================

    [Authorize(Roles = "Admin")]
    public IActionResult Routes(string type = "")
    {
        var query = db.Routes.AsQueryable();
        if (!string.IsNullOrEmpty(type))
            query = query.Where(r => r.TransportType == type);

        var routes = query.OrderBy(r => r.Id).ToList();
        ViewBag.SelectedType = type;
        // NOTE: Assuming hp.GetNextRouteId(db) is available via the Helper class dependency
        ViewBag.NextRouteId = hp.GetNextRouteId(db);

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return PartialView(routes);

        return View(routes);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CreateRoute(Route route)
    {
        if (ModelState.IsValid)
        {
            try
            {
                if (string.IsNullOrEmpty(route.Id))
                {
                    ViewBag.NextRouteId = hp.GetNextRouteId(db);
                    route.Id = ViewBag.NextRouteId;
                }

                db.Routes.Add(route);
                db.SaveChanges();
                TempData["Success"] = $"Route {route.Id} created successfully.";
            }
            catch (Exception ex)
            {
                var innerMessage = ex.InnerException?.Message ?? ex.Message;
                TempData["Error"] = $"Failed to create route (Database Error). Check ID length or data types. Error: {innerMessage}";
            }
        }
        else
        {
            var errors = ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToList()
                );

            if (errors.Count > 0)
            {
                var errorList = string.Join("<br>", errors.Select(e =>
                    $"**{e.Key}**: {string.Join(", ", e.Value)}"
                ));
                TempData["Error"] = $"Route submission failed validation: {errorList}";
            }
            else
            {
                TempData["Error"] = "Route submission failed validation for unknown reason.";
            }
        }
        return RedirectToAction("Routes");
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public IActionResult DeleteRoute(string id)
    {
        var route = db.Routes.Find(id);
        if (route != null)
        {
            if (db.Trips.Any(t => t.RouteId == id))
            {
                TempData["Info"] = "Cannot delete: Route is in use by existing trips.";
            }
            else
            {
                db.Routes.Remove(route);
                db.SaveChanges();
                TempData["Info"] = "Route deleted.";
            }
        }
        return RedirectToAction("Routes");
    }

    [Authorize(Roles = "Admin")]
    public IActionResult RouteStop(string id)
    {
        var route = db.Routes.Include(r => r.RouteStops).FirstOrDefault(r => r.Id == id);
        if (route == null)
            return RedirectToAction("Routes");

        ViewBag.RouteId = id;
        ViewBag.RouteName = $"{route.Origin} ➝ {route.Destination}";
        return View(route.RouteStops.OrderBy(s => s.Sequence).ToList());
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public IActionResult AddRouteStop(string routeId, string stopName, int minutes)
    {
        var count = db.RouteStops.Count(s => s.RouteId == routeId);

        var stop = new RouteStop
        {
            // NOTE: Assuming hp.GetNextRouteStopId(db) is available
            Id = hp.GetNextRouteStopId(db),
            RouteId = routeId,
            StopName = stopName,
            MinutesFromStart = minutes,
            Sequence = count + 1
        };
        db.RouteStops.Add(stop);
        db.SaveChanges();
        return RedirectToAction("RouteStop", new { id = routeId });
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public IActionResult DeleteRouteStop(string id)
    {
        var stop = db.RouteStops.Find(id);
        if (stop != null)
        {
            string rId = stop.RouteId;
            db.RouteStops.Remove(stop);
            db.SaveChanges();
            return RedirectToAction("RouteStop", new { id = rId });
        }
        return RedirectToAction("Routes");
    }

    // ============================================================================
    // ADMIN MANAGEMENT ACTIONS ( Trip )
    // ============================================================================

    // *** CRITICAL FIX: This is the ONLY Trips method. It replaces all others. ***
    [Authorize(Roles = "Admin")]
    public IActionResult Trips(string search, string status)
    {
        // Load related data to avoid null-reference crashes on Path or Vehicle
        var query = db.Trips
            .Include(t => t.Route)
            .Include(t => t.Vehicle)
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(t => t.Id.Contains(search) ||
                                     (t.Route != null && (t.Route.Origin.Contains(search) || t.Route.Destination.Contains(search))));
        }

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(t => t.Status == status);
        }

        ViewBag.Search = search;
        ViewBag.Status = status ?? "";

        return View(query.OrderByDescending(t => t.DepartureTime).ToList());
    }


    [Authorize(Roles = "Admin")]
    public IActionResult CreateTrip()
    {
        var routes = db.Routes.Where(r => r.IsActive).Select(r => new { r.Id, Name = r.Origin + " ➝ " + r.Destination }).ToList();

        ViewBag.RouteList = new SelectList(routes, "Id", "Name");
        ViewBag.VehicleList = new SelectList(db.Vehicles.Where(v => v.IsActive), "Id", "VehicleNumber");
        ViewBag.NextTripId = GetNextTripId(db); // Using local helper

        return View();
    }



    [Authorize(Roles = "Admin")]
[HttpPost]
public IActionResult CreateTrip(Trip trip)
{
    if (!ModelState.IsValid)
    {
        TempData["Error"] = "Invalid trip data.";
        return RedirectToAction("Trips");
    }

    // *********************
    // FIX: FETCH VEHICLE CAPACITY AND SET AVAILABLE SEATS
    // *********************
    // 1. Fetch the Vehicle entity to get the TotalSeats property.
    var vehicle = db.Vehicles.FirstOrDefault(v => v.Id == trip.VehicleId);

    if (vehicle == null)
    {
        TempData["Error"] = $"Error: Could not find Vehicle with ID {trip.VehicleId}.";
        return RedirectToAction("Trips");
    }

    // 2. CRITICAL: Initialize AvailableSeats with the Vehicle's total capacity.
    trip.AvailableSeats = vehicle.TotalSeats; 
    // *********************


    trip.Id = GetNextTripId(db);

    var routeStops = db.RouteStops
        .Where(rs => rs.RouteId == trip.RouteId)
        .OrderBy(rs => rs.Sequence)
        .ToList();

    int tripDurationMinutes = routeStops.Any()
        ? routeStops.Max(rs => rs.MinutesFromStart)
        : 0;

    trip.ArrivalTime = trip.DepartureTime.AddMinutes(tripDurationMinutes);

    trip.Status = "Scheduled";

    db.Trips.Add(trip); // Now, trip.AvailableSeats is correctly set!

    foreach (var rs in routeStops)
    {
        db.TripStops.Add(new TripStop
        {
            Id = GetNextTripStopId(db),
            TripId = trip.Id,
            RouteStopId = rs.Id,
            // Scheduled Arrival is based on the trip's start time + stop offset
            ScheduledArrival = trip.DepartureTime.AddMinutes(rs.MinutesFromStart),
            Status = "Scheduled"
        });
    }

    try
    {
        db.SaveChanges();
        TempData["Success"] = $"Trip {trip.Id} scheduled successfully with {routeStops.Count} stops and {vehicle.TotalSeats} seats!";
        return RedirectToAction("TripStop", new { id = trip.Id });
    }
    catch (Exception ex)
    {
        TempData["Error"] = "Error scheduling trip. Database exception occurred.";
        // Log the full exception details here for real debugging: ex.ToString()
        return RedirectToAction("Trips");
    }
}


    [Authorize(Roles = "Admin")]
    public IActionResult TripStop(string id)
    {
        // Eager load data required for the view (Route and Vehicle)
        var trip = db.Trips
            .Include(t => t.Route)
            .Include(t => t.Vehicle)
            .FirstOrDefault(t => t.Id == id);

        if (trip == null)
            return RedirectToAction("Trips");

        // Fix CS8602: Safely access Route properties using ?. to remove warnings
        ViewBag.TripInfo = $"Trip {trip.Id} ({trip.Route?.Origin ?? "N/A"} ➝ {trip.Route?.Destination ?? "N/A"})";

        return View(trip);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateTripStatus(string id, string status)
    {
        var trip = db.Trips.Find(id);

        if (trip == null)
        {
            TempData["Error"] = "Trip not found.";
            return RedirectToAction("Trips"); // Redirect to list on error
        }

        trip.Status = status;

        try
        {
            db.SaveChanges();
            TempData["Success"] = $"Trip {id} status updated to {status} successfully.";

            return RedirectToAction("Trips");
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Database error: Could not save status change.";
            return RedirectToAction("TripStop", new { id = trip.Id }); // Stay on details page on database error
        }
    }


    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken] // Always use Anti-Forgery Token for DELETE actions
    public IActionResult DeleteTrip(string id)
    {
        var trip = db.Trips
            .Include(t => t.TripStops) // Ensure related stops are loaded/considered
            .FirstOrDefault(t => t.Id == id);

        if (trip == null)
        {
            TempData["Info"] = "Trip not found.";
            return RedirectToAction("Trips");
        }

        // You should check if the trip has any completed or paid bookings before deleting.
        if (db.Bookings.Any(b => b.TripId == id && b.IsPaid))
        {
            TempData["Error"] = "Cannot delete trip: Paid bookings exist. Please cancel bookings first.";
            return RedirectToAction("Trips");
        }

        try
        {
            // Delete related TripStops first (though your DB.cs uses explicit DeleteBehavior, 
            // EF Core sometimes requires explicit action or loading the children).
            db.TripStops.RemoveRange(trip.TripStops);

            // Delete the parent Trip
            db.Trips.Remove(trip);

            db.SaveChanges();
            TempData["Success"] = $"Trip {id} and all related stops successfully deleted.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Database error: Failed to delete the trip. " + ex.Message;
        }

        // Redirect back to the list page
        return RedirectToAction("Trips");
    }


    // ============================================================================
    // ADMIN MANAGEMENT ACTIONS ( Driver List & Create )
    // ============================================================================

    [Authorize(Roles = "Admin")]
    public IActionResult Drivers(string search = "", int page = 1, string sort = "Id", string dir = "asc")
    {
        int pageSize = 10;

        var query = db.Drivers.Include(d => d.Vehicle).AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(d => d.Id.Contains(search) || d.Name.Contains(search) || d.Email.Contains(search) || d.Phone.Contains(search));
        }

        query = (sort.ToLower(), dir.ToLower()) switch
        {
            ("name", "asc") => query.OrderBy(d => d.Name),
            ("name", "desc") => query.OrderByDescending(d => d.Name),
            ("email", "asc") => query.OrderBy(d => d.Email),
            ("email", "desc") => query.OrderByDescending(d => d.Email),
            _ => query.OrderBy(d => d.Id)
        };

        int totalItems = query.Count();
        int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
        var drivers = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.Search = search;
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return PartialView("_DriverTable", drivers);

        return View(drivers);
    }

    [Authorize(Roles = "Admin")]
    public IActionResult CreateDriver()
    {
        ViewBag.NextDriverId = GetNextDriverId(db);
        return View();
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CreateDriver(Driver driver)
    {
        if (ModelState.IsValid)
        {
            try
            {
                driver.Id = GetNextDriverId(db);

                db.Users.Add(driver);

                db.SaveChanges();
                TempData["Success"] = $"Driver {driver.Id} created successfully. Now register their vehicle.";

                return RedirectToAction("CreateVehicle", "Vehicle", new { driverId = driver.Id });
            }
            catch (Exception ex)
            {
                var innerMessage = ex.InnerException?.Message ?? ex.Message;
                TempData["Error"] = $"Failed to create driver (Database Error): {innerMessage}";
                ViewBag.NextDriverId = GetNextDriverId(db);
                return View(driver);
            }
        }

        TempData["Error"] = "Driver submission failed validation. Please check all fields.";
        ViewBag.NextDriverId = GetNextDriverId(db);
        return View(driver);
    }

    [Authorize(Roles = "Admin")]
    public IActionResult DriverDetails(string id)
    {
        var driver = db.Drivers.Include(d => d.Vehicle).FirstOrDefault(d => d.Id == id);
        if (driver == null)
        {
            TempData["Info"] = "Driver not found.";
            return RedirectToAction("Drivers");
        }

        ViewBag.RecentTrips = db.Trips.Where(t => t.Vehicle.DriverId == id).OrderByDescending(t => t.DepartureTime).Take(5).ToList();

        return View(driver);
    }


    [Authorize(Roles = "Admin")]
    [HttpPost]
    public IActionResult DeleteDriver(string id)
    {
        var driver = db.Drivers.Find(id);
        if (driver == null)
        {
            TempData["Info"] = "Driver not found.";
            return RedirectToAction("Drivers");
        }

        if (db.Vehicles.Any(v => v.DriverId == id))
        {
            TempData["Error"] = "Cannot delete: Driver is still assigned to a vehicle.";
            return RedirectToAction("Drivers");
        }

        db.Drivers.Remove(driver);
        db.SaveChanges();
        TempData["Info"] = $"Driver {id} deleted successfully.";

        return RedirectToAction("Drivers");
    }


    // GET: RouteNTrip/ModifyDriver
    [Authorize(Roles = "Admin")]
    public IActionResult ModifyDriver(string id)
    {
        var driver = db.Drivers.Find(id);
        if (driver == null)
        {
            TempData["Info"] = "Driver not found.";
            return RedirectToAction("Drivers");
        }

        // NOTE: DriverVM assumed to exist
        var vm = new DriverVM
        {
            Id = driver.Id,
            Name = driver.Name,
            Email = driver.Email,
            Phone = driver.Phone,
            LicenseNumber = driver.LicenseNumber,
            ExistingPhotoURL = driver.PhotoURL,
            IsBlocked = driver.IsBlocked
        };

        ViewBag.VehicleNumber = driver.Vehicle?.VehicleNumber;

        return View(vm);
    }

    // POST: RouteNTrip/ModifyDriver
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ModifyDriver(DriverVM vm)
    {
        var driver = db.Drivers.Find(vm.Id);
        if (driver == null)
            return RedirectToAction("Drivers");

        // 1. Validation Checks (Email/Phone uniqueness)
        if (db.Users.Any(u => u.Email == vm.Email && u.Id != vm.Id))
            ModelState.AddModelError("Email", "Email already in use.");

        if (db.Users.Any(u => u.Phone == vm.Phone && u.Id != vm.Id))
            ModelState.AddModelError("Phone", "Phone number already in use.");

        // 2. Photo Validation
        if (vm.Photo != null)
        {
            var err = hp.ValidatePhoto(vm.Photo); // Assumes helper method exists
            if (err != "")
                ModelState.AddModelError("Photo", err);
        }

        if (ModelState.IsValid)
        {
            driver.Name = vm.Name;
            driver.Email = vm.Email;
            driver.Phone = vm.Phone;
            driver.LicenseNumber = vm.LicenseNumber;
            driver.IsBlocked = vm.IsBlocked;

            // 3. Password Update
            if (!string.IsNullOrEmpty(vm.Password))
            {
                driver.Hash = hp.HashPassword(vm.Password); // Assumes helper method exists
            }

            // 4. Photo Update/Deletion
            if (vm.Photo != null)
            {
                if (!string.IsNullOrEmpty(driver.PhotoURL) && driver.PhotoURL != "default_photo.png")
                {
                    hp.DeletePhoto(driver.PhotoURL, "images"); // Assumes helper method exists
                }
                driver.PhotoURL = hp.SavePhoto(vm.Photo, "images"); // Assumes helper method exists
            }

            db.SaveChanges();
            TempData["Success"] = $"Driver {driver.Id} updated successfully.";
            return RedirectToAction("DriverDetails", new { id = driver.Id });
        }

        ViewBag.VehicleNumber = driver.Vehicle?.VehicleNumber;
        return View(vm);
    }


    // POST: RouteNTrip/InitiateDriverPasswordReset/D001
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult InitiateDriverPasswordReset(string id)
    {
        var driver = db.Drivers.Find(id);
        if (driver == null || string.IsNullOrEmpty(driver.Email))
        {
            TempData["Error"] = "Driver not found or email address is missing.";
            return RedirectToAction("DriverDetails", new { id });
        }

        // --- TEMPORARY FIX: COMMENT OUT LIVE HELPER CALLS ---
        string resetToken = Guid.NewGuid().ToString(); // TEMP: Placeholder 

        string resetLink = Url.Action("ResetPassword", "Account",
                                      new { userId = driver.Id, token = resetToken },
                                      Request.Scheme);

        // bool success = hp.SendPasswordResetEmail(driver.Email, driver.Name, resetLink); // COMMENTED OUT
        bool success = true; // TEMP: Assume success for debugging MVC flow
        // --- End Fix ---

        if (success)
        {
            TempData["Success"] = $"Password reset link successfully generated for {driver.Email}. (Email not actually sent in this version)";
        }
        else
        {
            TempData["Error"] = "Failed to initiate reset link.";
        }

        return RedirectToAction("DriverDetails", new { id });
    }

    // POST: RouteNTrip/ToggleBlockDriver/D001
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ToggleBlockDriver(string id)
    {
        var driver = db.Drivers.Find(id);
        if (driver == null)
        {
            TempData["Info"] = "Driver not found.";
            return RedirectToAction("Drivers");
        }

        if (id == "A001")
        {
            TempData["Error"] = "System Administrator cannot be blocked.";
            return RedirectToAction("DriverDetails", new { id });
        }

        driver.IsBlocked = !driver.IsBlocked;
        db.SaveChanges();

        string status = driver.IsBlocked ? "blocked" : "unblocked";
        TempData["Success"] = $"Driver {driver.Name} has been {status}.";

        return RedirectToAction("DriverDetails", new { id = driver.Id });
    }

}