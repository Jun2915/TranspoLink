using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace TranspoLink.Controllers;

public class HomeController(DB db) : Controller
{
    // GET: Home/Index
    public IActionResult Index()
    {
        return View();
    }

    // GET: Home/AboutUs
    public IActionResult AboutUs()
    {
        return View();
    }

    //GET: Home/Reports
    public IActionResult Reports()
    {
        return View();
    }
}