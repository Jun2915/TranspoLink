using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TranspoLink.Models;

namespace TranspoLink.Controllers;

[Authorize(Roles = "Admin")]
public class VehicleController(DB db, Helper hp) : Controller
{
    // ============================================================================
    // PRIVATE ID GENERATION HELPER (Local implementation)
    // ============================================================================
    private string GetNextVehicleNumber(DB db)
    {
        var lastId = db.Vehicles.OrderByDescending(v => v.Id).Select(v => v.VehicleNumber).FirstOrDefault();
        if (string.IsNullOrEmpty(lastId)) return "V001";

        if (lastId.Length > 1 && int.TryParse(lastId.Substring(1), out int num))
        {
            return "V" + (num + 1).ToString("D3");
        }
        return "V001";
    }

    // ============================================================================
    // READ: List Vehicles
    // ============================================================================
    public IActionResult Vehicles(string search = "", string type = "", string sort = "Id", string dir = "asc")
    {
        var query = db.Vehicles.Include(v => v.Driver).AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(v =>
                v.VehicleNumber.Contains(search) ||
                (v.Driver != null && v.Driver.Name.Contains(search)));
        }

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
    public IActionResult CreateVehicle(string driverId = null)
    {
        ViewBag.VehicleTypes = new SelectList(new[] { "Bus", "Train", "Ferry" });

        var availableDrivers = db.Drivers
            .Where(d => d.Vehicle == null && !d.IsBlocked)
            .Select(d => new { d.Id, Display = d.Name + " (" + d.Id + ")" })
            .ToList();

        ViewBag.Drivers = new SelectList(availableDrivers, "Id", "Display", driverId);

        string nextId = GetNextVehicleNumber(db);

        return View(new Vehicle { VehicleNumber = nextId, DriverId = driverId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CreateVehicle(Vehicle vehicle)
    {
        // Unique Vehicle Number Check
        if (db.Vehicles.Any(v => v.VehicleNumber == vehicle.VehicleNumber))
        {
            ModelState.AddModelError("VehicleNumber", "This Vehicle Number already exists.");
        }

        // 1:1 Constraint Check (Driver assigned check)
        if (!string.IsNullOrEmpty(vehicle.DriverId) && db.Vehicles.Any(v => v.DriverId == vehicle.DriverId))
        {
            ModelState.AddModelError("DriverId", "This Driver is already assigned to another vehicle.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                vehicle.IsActive = true;
                db.Vehicles.Add(vehicle);
                db.SaveChanges();

                TempData["Info"] = "✨ New vehicle added successfully!";
                return RedirectToAction("Vehicles");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Database Error: " + ex.InnerException?.Message ?? ex.Message);
            }
        }

        // --- FAILURE PATH ---

        // Repopulate ViewBags
        ViewBag.VehicleTypes = new SelectList(new[] { "Bus", "Train", "Ferry" }, vehicle.Type);

        var availableDrivers = db.Drivers
            .Where(d => d.Vehicle == null && !d.IsBlocked)
            .Select(d => new { d.Id, Display = d.Name + " (" + d.Id + ")" })
            .ToList();
        ViewBag.Drivers = new SelectList(availableDrivers, "Id", "Display", vehicle.DriverId);

        // Regenerate Vehicle Number if lost
        if (string.IsNullOrEmpty(vehicle.VehicleNumber))
        {
            vehicle.VehicleNumber = GetNextVehicleNumber(db);
        }

        return View(vehicle);
    }

    // ============================================================================
    // EDIT, DELETE, TOGGLE (Standard)
    // ============================================================================
    public IActionResult EditVehicle(int id)
    {
        var vehicle = db.Vehicles.Find(id);
        if (vehicle == null)
            return RedirectToAction("Vehicles");

        ViewBag.VehicleTypes = new SelectList(new[] { "Bus", "Train", "Ferry" }, vehicle.Type);

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