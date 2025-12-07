using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Route = TranspoLink.Models.Route;

namespace TranspoLink.Controllers;

public class RouteNTripController(DB db, Helper hp) : Controller
{
    // ============================================================================
    // PUBLIC SEARCH ACTIONS (Accessible by Everyone)
    // ============================================================================

    // 1. AJAX Autocomplete: Get unique locations
    [HttpGet]
    public IActionResult GetLocations()
    {
        var origins = db.Routes.Where(r => r.IsActive).Select(r => r.Origin).Distinct();
        var destinations = db.Routes.Where(r => r.IsActive).Select(r => r.Destination).Distinct();

        var locations = origins.Union(destinations)
                               .Distinct()
                               .OrderBy(x => x)
                               .ToList();

        return Json(locations);
    }

    // 2. Search Results Page
    [Authorize]
    [AcceptVerbs("GET", "POST")]
    public IActionResult SearchTrip(string origin, string destination, DateTime departDate, string transportType = "Bus")
    {
        // Basic Validation
        if (string.IsNullOrEmpty(origin) || string.IsNullOrEmpty(destination))
        {
            TempData["Info"] = "Please select an Origin and Destination.";
            return RedirectToAction("Index", "Home");
        }

        // Query Logic
        var query = db.Trips
            .Include(t => t.Route)
            .Include(t => t.Vehicle)
            .Where(t => t.Status == "Scheduled") // Only show scheduled trips
            .AsQueryable();

        // 1. Filter by Transport Type
        if (!string.IsNullOrEmpty(transportType))
        {
            query = query.Where(t => t.Route.TransportType == transportType);
        }

        // 2. Filter by Route (Case insensitive matches)
        query = query.Where(t => t.Route.Origin == origin && t.Route.Destination == destination);

        // 3. Filter by Date (Compare Date parts only)
        query = query.Where(t => t.DepartureTime.Date == departDate.Date);

        var results = query.OrderBy(t => t.DepartureTime).ToList();

        // Pass params back for display
        ViewBag.SearchOrigin = origin;
        ViewBag.SearchDest = destination;
        ViewBag.SearchDate = departDate.ToString("dd MMM yyyy");
        ViewBag.TransportType = transportType;

        return View(results);
    }


    // ============================================================================
    // ADMIN MANAGEMENT ACTIONS (Authorized Only)
    // ============================================================================

    [Authorize(Roles = "Admin")]
    public IActionResult Routes(string type = "")
    {
        var query = db.Routes.AsQueryable();
        if (!string.IsNullOrEmpty(type))
            query = query.Where(r => r.TransportType == type);

        var routes = query.OrderBy(r => r.Id).ToList();
        ViewBag.SelectedType = type;
        ViewBag.NextRouteId = hp.GetNextRouteId(db);

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return PartialView(routes);

        return View(routes);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public IActionResult CreateRoute(Route route)
    {
        if (ModelState.IsValid)
        {
            route.Id = hp.GetNextRouteId(db);
            db.Routes.Add(route);
            db.SaveChanges();
            TempData["Info"] = $"Route {route.Id} created successfully.";
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

    [Authorize(Roles = "Admin")]
    public IActionResult Trips(string status = "")
    {
        var query = db.Trips.Include(t => t.Route).Include(t => t.Vehicle).AsQueryable();
        if (!string.IsNullOrEmpty(status))
            query = query.Where(t => t.Status == status);

        var trips = query.OrderByDescending(t => t.DepartureTime).ToList();

        ViewBag.SelectedStatus = status;

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return PartialView(trips);

        return View(trips);
    }

    [Authorize(Roles = "Admin")]
    public IActionResult CreateTrip()
    {
        var routes = db.Routes.Where(r => r.IsActive)
                       .Select(r => new { r.Id, Name = r.Origin + " ➝ " + r.Destination }).ToList();

        ViewBag.RouteList = new SelectList(routes, "Id", "Name");
        ViewBag.VehicleList = new SelectList(db.Vehicles.Where(v => v.IsActive), "Id", "VehicleNumber");
        ViewBag.NextTripId = hp.GetNextTripId(db);

        return View();
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public IActionResult CreateTrip(Trip trip)
    {
        if (trip.ArrivalTime <= trip.DepartureTime)
            ModelState.AddModelError("ArrivalTime", "Arrival must be after Departure.");

        if (ModelState.IsValid)
        {
            if (trip.AvailableSeats == 0)
            {
                var v = db.Vehicles.Find(trip.VehicleId);
                trip.AvailableSeats = v?.TotalSeats ?? 40;
            }

            trip.Id = hp.GetNextTripId(db);
            trip.Status = "Scheduled";

            db.Trips.Add(trip);
            db.SaveChanges();

            // Auto-generate TripStops based on RouteStops
            var routeStops = db.RouteStops.Where(rs => rs.RouteId == trip.RouteId).ToList();
            foreach (var rs in routeStops)
            {
                db.TripStops.Add(new TripStop
                {
                    Id = hp.GetNextTripStopId(db),
                    TripId = trip.Id,
                    RouteStopId = rs.Id,
                    ScheduledArrival = trip.DepartureTime.AddMinutes(rs.MinutesFromStart),
                    Status = "Scheduled"
                });
                db.SaveChanges();
            }

            TempData["Info"] = $"Trip {trip.Id} generated.";
            return RedirectToAction("Trips");
        }

        var routes = db.Routes.Where(r => r.IsActive).Select(r => new { r.Id, Name = r.Origin + " ➝ " + r.Destination }).ToList();
        ViewBag.RouteList = new SelectList(routes, "Id", "Name", trip.RouteId);
        ViewBag.VehicleList = new SelectList(db.Vehicles.Where(v => v.IsActive), "Id", "VehicleNumber", trip.VehicleId);
        return View(trip);
    }

    [Authorize(Roles = "Admin")]
    public IActionResult TripStop(string id)
    {
        var trip = db.Trips.Include(t => t.Route).FirstOrDefault(t => t.Id == id);
        if (trip == null)
            return RedirectToAction("Trips");

        var stops = db.TripStops
            .Include(ts => ts.RouteStop)
            .Where(ts => ts.TripId == id)
            .OrderBy(ts => ts.RouteStop.Sequence)
            .ToList();

        ViewBag.TripInfo = $"Trip {trip.Id} ({trip.Route.Origin} ➝ {trip.Route.Destination})";
        ViewBag.TripId = id;
        return View(stops);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public IActionResult UpdateTripStop(string id, DateTime actualTime, string status)
    {
        var ts = db.TripStops.Find(id);
        if (ts != null)
        {
            ts.ActualArrival = actualTime;
            ts.Status = status;
            db.SaveChanges();
        }
        return Json(new { success = true });
    }
}