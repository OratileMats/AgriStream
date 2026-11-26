using AgriStream.Data;
using AgriStream.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriStream.Controllers
{
    [Authorize(Roles = Roles.Admin)]
    public class UsersController : Controller
    {
        private readonly AppDbContext _db;
        public UsersController(AppDbContext db) => _db = db;

        public async Task<IActionResult> Index()
            => View(await _db.Users.AsNoTracking().OrderBy(u => u.Username).ToListAsync());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeRole(int id, string role)
        {
            if (!Roles.All.Contains(role)) return BadRequest();
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.Role = role;
            await _db.SaveChangesAsync();
            TempData["Message"] = $"{user.Username} is now a {role}. The change applies at their next sign-in.";
            return RedirectToAction(nameof(Index));
        }
    }
}
