using Microsoft.AspNetCore.Mvc;

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

    // GET: Home/Reports
    public IActionResult Reports()
    {
        return View();
    }

    // GET: Home/TermsAndConditions
    public IActionResult TermsAndConditions()
    {
        return View();
    }

    // GET: Home/PrivacyPolicy
    public IActionResult PrivacyPolicy()
    {
        return View();
    }
}