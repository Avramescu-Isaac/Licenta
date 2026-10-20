using CourseHub.Data;
using CourseHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Authorize]
public class ConnectionsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public ConnectionsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var currentUserId = _userManager.GetUserId(User);
        var currentUser = await _userManager.GetUserAsync(User);

        var matches = currentUser.Role == "Teach"
        ? await _context.HireRequests.Include(r => r.Requester)
            .Where(r => r.TargetId == currentUserId && r.IsAccepted)
            .Select(r => r.Requester).ToListAsync()
        : await _context.HireRequests.Include(r => r.Target)
            .Where(r => r.RequesterId == currentUserId && r.IsAccepted)
            .Select(r => r.Target).ToListAsync();


        ViewBag.IsTeach = currentUser.Role == "Teach";
        return View(matches);
    }
}
