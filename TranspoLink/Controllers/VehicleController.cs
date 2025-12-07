using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace TranspoLink.Controllers;

[Authorize(Roles = "Admin")]
public class VehicleController(DB db, Helper hp) : Controller
{
    // ============================================================================
    // READ: List Vehicles (Updated for AJAX)
    // ============================================================================
    public IActionResult Vehicles(string search = "", string type = "", string sort = "Id", string dir = "asc")
    {
        // 1. Include Driver information
        var query = db.Vehicles.Include(v => v.Driver).AsQueryable();

        // 2. Search Logic (Updated to search Driver Name)
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(v =>
                v.VehicleNumber.Contains(search) ||
                (v.Driver != null && v.Driver.Name.Contains(search)));
        }

        if (!string.IsNullOrEmpty(type))
        {
            query = query.Where(v => v.Type == type);
        }

        // 3. Sort Logic (Updated Operator -> Driver)
        query = sort switch
        {
            "VehicleNumber" => dir == "asc" ? query.OrderBy(v => v.VehicleNumber) : query.OrderByDescending(v => v.VehicleNumber),
            "Type" => dir == "asc" ? query.OrderBy(v => v.Type) : query.OrderByDescending(v => v.Type),
            "Driver" => dir == "asc" ? query.OrderBy(v => v.Driver.Name) : query.OrderByDescending(v => v.Driver.Name),
            "Seats" => dir == "asc" ? query.OrderBy(v => v.TotalSeats) : query.OrderByDescending(v => v.TotalSeats),
            "Status" => dir == "asc" ? query.OrderBy(v => v.IsActive) : query.OrderByDescending(v => v.IsActive),
            _ => dir == "asc" ? query.OrderBy(v => v.Id) : query.OrderByDescending(v => v.Id)
        };

        var vehicles = query.ToList();

        ViewBag.Search = search;
        ViewBag.SelectedType = type;
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return PartialView("_VehicleTable", vehicles);
        }

        return View(vehicles);
    }

    // ============================================================================
    // DETAILS
    // ============================================================================
    public IActionResult VehicleDetails(int id)
    {
        var vehicle = db.Vehicles.Include(v => v.Driver).FirstOrDefault(v => v.Id == id);
        if (vehicle == null)
        {
            TempData["Info"] = "Vehicle not found.";
            return RedirectToAction("Vehicles");
        }

        var trips = db.Trips
            .Where(t => t.VehicleId == id)
            .Select(t => t.DepartureTime.Date)
            .ToList();

        ViewBag.TripDates = trips;

        return View(vehicle);
    }

    // ============================================================================
    // CREATE
    // ============================================================================
    public IActionResult CreateVehicle()
    {
        ViewBag.VehicleTypes = new SelectList(new[] { "Bus", "Train", "Ferry" });

        // Populate Drivers Dropdown (Only those WITHOUT a vehicle)
        var availableDrivers = db.Drivers
            .Where(d => d.Vehicle == null && !d.IsBlocked)
            .Select(d => new { d.Id, Display = d.Name + " (" + d.Id + ")" })
            .ToList();

        ViewBag.Drivers = new SelectList(availableDrivers, "Id", "Display");

        string nextId = hp.GetNextVehicleNumber(db);
        return View(new Vehicle { VehicleNumber = nextId });
    }

    [HttpPost]
    public IActionResult CreateVehicle(Vehicle vehicle)
    {
        // Unique Vehicle Number Check
        if (db.Vehicles.Any(v => v.VehicleNumber == vehicle.VehicleNumber))
        {
            ModelState.AddModelError("VehicleNumber", "This Vehicle Number already exists.");
        }

        // 1:1 Constraint Check (Double check just in case)
        if (!string.IsNullOrEmpty(vehicle.DriverId) && db.Vehicles.Any(v => v.DriverId == vehicle.DriverId))
        {
            ModelState.AddModelError("DriverId", "This Driver is already assigned to another vehicle.");
        }

        if (ModelState.IsValid)
        {
            vehicle.IsActive = true;
            db.Vehicles.Add(vehicle);
            db.SaveChanges();

            TempData["Info"] = "✨ New vehicle added successfully!";
            return RedirectToAction("Vehicles");
        }

        ViewBag.VehicleTypes = new SelectList(new[] { "Bus", "Train", "Ferry" });

        // Re-populate dropdown
        var availableDrivers = db.Drivers
            .Where(d => d.Vehicle == null && !d.IsBlocked)
            .Select(d => new { d.Id, Display = d.Name + " (" + d.Id + ")" })
            .ToList();
        ViewBag.Drivers = new SelectList(availableDrivers, "Id", "Display", vehicle.DriverId);

        return View(vehicle);
    }

    // ============================================================================
    // EDIT
    // ============================================================================
    public IActionResult EditVehicle(int id)
    {
        var vehicle = db.Vehicles.Find(id);
        if (vehicle == null)
            return RedirectToAction("Vehicles");

        ViewBag.VehicleTypes = new SelectList(new[] { "Bus", "Train", "Ferry" }, vehicle.Type);

        // Populate Drivers: Available Drivers + The Current Driver of this vehicle
        var drivers = db.Drivers
            .Where(d => (d.Vehicle == null || d.Vehicle.Id == id) && !d.IsBlocked)
            .Select(d => new { d.Id, Display = d.Name + " (" + d.Id + ")" })
            .ToList();

        ViewBag.Drivers = new SelectList(drivers, "Id", "Display", vehicle.DriverId);

        return View(vehicle);
    }

    [HttpPost]
    public IActionResult EditVehicle(Vehicle vehicle)
    {
        if (db.Vehicles.Any(v => v.VehicleNumber == vehicle.VehicleNumber && v.Id != vehicle.Id))
        {
            ModelState.AddModelError("VehicleNumber", "This Vehicle Number is taken.");
        }

        // 1:1 Constraint Check
        if (!string.IsNullOrEmpty(vehicle.DriverId) && db.Vehicles.Any(v => v.DriverId == vehicle.DriverId && v.Id != vehicle.Id))
        {
            ModelState.AddModelError("DriverId", "This Driver is already assigned to another vehicle.");
        }

        if (ModelState.IsValid)
        {
            db.Vehicles.Update(vehicle);
            db.SaveChanges();

            TempData["Info"] = "Vehicle updated successfully.";
            return RedirectToAction("Vehicles");
        }

        ViewBag.VehicleTypes = new SelectList(new[] { "Bus", "Train", "Ferry" }, vehicle.Type);

        var drivers = db.Drivers
            .Where(d => (d.Vehicle == null || d.Vehicle.Id == vehicle.Id) && !d.IsBlocked)
            .Select(d => new { d.Id, Display = d.Name + " (" + d.Id + ")" })
            .ToList();
        ViewBag.Drivers = new SelectList(drivers, "Id", "Display", vehicle.DriverId);

        return View(vehicle);
    }

    // DELETE & TOGGLE (Standard)
    [HttpPost]
    public IActionResult DeleteVehicle(int id)
    {
        var vehicle = db.Vehicles.Find(id);
        if (vehicle != null && !db.Trips.Any(t => t.VehicleId == id))
        {
            db.Vehicles.Remove(vehicle);
            db.SaveChanges();
            TempData["Info"] = "Vehicle deleted.";
        }
        else
        {
            TempData["Info"] = "Cannot delete: Vehicle is in use.";
        }
        return RedirectToAction("Vehicles");
    }

    [HttpPost]
    public IActionResult ToggleVehicleStatus(int id)
    {
        var v = db.Vehicles.Find(id);
        if (v != null)
        {
            v.IsActive = !v.IsActive;
            db.SaveChanges();
            TempData["Info"] = "Status updated.";
        }
        return RedirectToAction("Vehicles");
    }
}