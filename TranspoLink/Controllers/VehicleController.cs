using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace TranspoLink.Controllers;

[Authorize(Roles = "Admin")]
public class VehicleController(DB db, Helper hp) : Controller // (Injected Helper hp)
{
    //

    // ============================================================================
    // READ: List Vehicles (Updated for AJAX)
    // ============================================================================
    public IActionResult Vehicles(string search = "", string type = "", string sort = "Id", string dir = "asc")
    {
        var query = db.Vehicles.AsQueryable();

        // 1. Search Logic
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(v =>
                v.VehicleNumber.Contains(search) ||
                v.Operator.Contains(search));
        }

        // 2. Filter by Type
        if (!string.IsNullOrEmpty(type))
        {
            query = query.Where(v => v.Type == type);
        }

        // 3. Sort Logic
        query = sort switch
        {
            "VehicleNumber" => dir == "asc" ? query.OrderBy(v => v.VehicleNumber) : query.OrderByDescending(v => v.VehicleNumber),
            "Type" => dir == "asc" ? query.OrderBy(v => v.Type) : query.OrderByDescending(v => v.Type),
            "Operator" => dir == "asc" ? query.OrderBy(v => v.Operator) : query.OrderByDescending(v => v.Operator),
            "Seats" => dir == "asc" ? query.OrderBy(v => v.TotalSeats) : query.OrderByDescending(v => v.TotalSeats),
            "Status" => dir == "asc" ? query.OrderBy(v => v.IsActive) : query.OrderByDescending(v => v.IsActive),
            _ => dir == "asc" ? query.OrderBy(v => v.Id) : query.OrderByDescending(v => v.Id)
        };

        var vehicles = query.ToList();

        ViewBag.Search = search;
        ViewBag.SelectedType = type;
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        // Return Partial View for AJAX calls
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return PartialView("_VehicleTable", vehicles);
        }

        return View(vehicles);
    }

    // ============================================================================
    // DETAILS: Vehicle Details & Calendar
    // ============================================================================
    public IActionResult VehicleDetails(int id)
    {
        var vehicle = db.Vehicles.Find(id);
        if (vehicle == null)
        {
            TempData["Info"] = "Vehicle not found.";
            return RedirectToAction("Vehicles");
        }

        // Fetch trips for this vehicle to show on calendar
        var trips = db.Trips
            .Where(t => t.VehicleId == id)
            .Select(t => t.DepartureTime.Date)
            .ToList();

        ViewBag.TripDates = trips; // Pass dates to view

        return View(vehicle);
    }

    // CREATE (GET) - Auto-generate ID here
    public IActionResult CreateVehicle()
    {
        ViewBag.VehicleTypes = new SelectList(new[] { "Bus", "Train", "Ferry" });

        // 1. Generate the VXXX ID
        string nextId = hp.GetNextVehicleNumber(db);

        // 2. Pass it to the view
        return View(new Vehicle { VehicleNumber = nextId });
    }

    // CREATE (POST)
    [HttpPost]
    public IActionResult CreateVehicle(Vehicle vehicle)
    {
        if (db.Vehicles.Any(v => v.VehicleNumber == vehicle.VehicleNumber))
        {
            ModelState.AddModelError("VehicleNumber", "This Vehicle Number already exists.");
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
        return View(vehicle);
    }

    // EDIT (GET)
    public IActionResult EditVehicle(int id)
    {
        var vehicle = db.Vehicles.Find(id);
        if (vehicle == null) return RedirectToAction("Vehicles");

        ViewBag.VehicleTypes = new SelectList(new[] { "Bus", "Train", "Ferry" }, vehicle.Type);
        return View(vehicle);
    }

    // EDIT (POST) - Redirects back to Vehicles
    [HttpPost]
    public IActionResult EditVehicle(Vehicle vehicle)
    {
        if (db.Vehicles.Any(v => v.VehicleNumber == vehicle.VehicleNumber && v.Id != vehicle.Id))
        {
            ModelState.AddModelError("VehicleNumber", "This Vehicle Number is taken.");
        }

        if (ModelState.IsValid)
        {
            db.Vehicles.Update(vehicle);
            db.SaveChanges();

            TempData["Info"] = "Vehicle updated successfully.";

            // 3. JUMP BACK TO VEHICLES PAGE
            return RedirectToAction("Vehicles");
        }

        ViewBag.VehicleTypes = new SelectList(new[] { "Bus", "Train", "Ferry" }, vehicle.Type);
        return View(vehicle);
    }

    // DELETE
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

    // TOGGLE STATUS
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