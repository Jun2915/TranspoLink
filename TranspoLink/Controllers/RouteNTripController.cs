using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Route = TranspoLink.Models.Route;

namespace TranspoLink.Controllers;

[Authorize(Roles = "Admin")]
public class RouteNTripController(DB db, Helper hp) : Controller
{
    // ROUTES
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

    // ROUTE STOPS
    public IActionResult RouteStop(string id) // Changed int -> string
    {
        var route = db.Routes.Include(r => r.RouteStops).FirstOrDefault(r => r.Id == id);
        if (route == null)
            return RedirectToAction("Routes");

        ViewBag.RouteId = id;
        ViewBag.RouteName = $"{route.Origin} ➝ {route.Destination}";
        return View(route.RouteStops.OrderBy(s => s.Sequence).ToList());
    }

    [HttpPost]
    public IActionResult AddRouteStop(string routeId, string stopName, int minutes)
    {
        var count = db.RouteStops.Count(s => s.RouteId == routeId);

        var stop = new RouteStop
        {
            Id = hp.GetNextRouteStopId(db), // GENERATE ID RSXXX
            RouteId = routeId,
            StopName = stopName,
            MinutesFromStart = minutes,
            Sequence = count + 1
        };
        db.RouteStops.Add(stop);
        db.SaveChanges();
        return RedirectToAction("RouteStop", new { id = routeId });
    }

    [HttpPost]
    public IActionResult DeleteRouteStop(string id) // Changed int -> string
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

    // TRIPS
    public IActionResult Trips(string status = "")
    {
        var query = db.Trips.Include(t => t.Route).Include(t => t.Vehicle).AsQueryable();
        if (!string.IsNullOrEmpty(status))
            query = query.Where(t => t.Status == status);

        var trips = query.OrderByDescending(t => t.DepartureTime).ToList();
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return PartialView(trips);
        return View(trips);
    }

    public IActionResult CreateTrip()
    {
        var routes = db.Routes.Where(r => r.IsActive)
                       .Select(r => new { r.Id, Name = r.Origin + " ➝ " + r.Destination }).ToList();
        ViewBag.RouteList = new SelectList(routes, "Id", "Name");
        ViewBag.VehicleList = new SelectList(db.Vehicles.Where(v => v.IsActive), "Id", "VehicleNumber");
        ViewBag.NextTripId = hp.GetNextTripId(db);

        return View();
    }

    [HttpPost]
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

            // 👇 FORCE GENERATE ID (Ignores user input)
            trip.Id = hp.GetNextTripId(db);
            trip.Status = "Scheduled";

            db.Trips.Add(trip);
            db.SaveChanges();

            // (Generate TripStops logic...)
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

    // TRIP STOPS
    public IActionResult TripStop(string id) // Changed int -> string
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

    [HttpPost]
    public IActionResult UpdateTripStop(string id, DateTime actualTime, string status) // Changed int -> string
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