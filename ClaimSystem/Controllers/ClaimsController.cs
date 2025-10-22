using System.Security.Claims;
using ClaimSystem.Data;
using ClaimSystem.Models;
using ClaimSystem.Models.ViewModels;
using ClaimSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClaimSystem.Controllers
{
    public class ClaimsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<ClaimsController> _log;

        public ClaimsController(ApplicationDbContext db, IWebHostEnvironment env, ILogger<ClaimsController> log)
        {
            _db = db;
            _env = env;
            _log = log;
        }

        public IActionResult Index() => RedirectToAction(nameof(Submit));

        [HttpGet]
        public IActionResult Submit()
            => View(new ClaimFormViewModel { Month = DateTime.UtcNow.ToString("MMMM yyyy") });

        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<IActionResult> Submit(ClaimFormViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            try
            {
      
                var lecturer = vm.LecturerName?.Trim() ?? string.Empty;
                var month = vm.Month?.Trim() ?? string.Empty;

          
                var exists = await _db.Claims.AnyAsync(c =>
                    c.LecturerName == lecturer &&
                    c.Month == month &&
                    c.Status != ClaimStatus.Rejected);

                if (exists)
                {
                    ModelState.AddModelError(string.Empty,
                        "A claim for this lecturer and month already exists and is under review or already submitted.");
                    ModelState.AddModelError(nameof(vm.Month), "Duplicate for this month.");
                    return View(vm);
                }

             
                var claim = new Models.Claim
                {
                    LecturerName = lecturer,
                    Month = month,
                    HoursWorked = vm.HoursWorked,
                    HourlyRate = vm.HourlyRate,
                    Notes = string.IsNullOrWhiteSpace(vm.Notes) ? null : vm.Notes.Trim(),
                    Status = ClaimStatus.Submitted
                };

                _db.Claims.Add(claim);
                await _db.SaveChangesAsync(); 

                
                if (vm.File is { Length: > 0 })
                {
                    if (!DocumentRules.IsAllowed(vm.File.FileName))
                    {
                        ModelState.AddModelError("File", "Only .pdf, .docx, or .xlsx files are allowed.");
                        return View(vm);
                    }
                    if (DocumentRules.IsTooLarge(vm.File.Length))
                    {
                        ModelState.AddModelError("File", "File too large (max 10 MB).");
                        return View(vm);
                    }

                    var uploadsRoot = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "claims", claim.Id.ToString());
                    Directory.CreateDirectory(uploadsRoot);

                    var stored = $"{Guid.NewGuid():N}{Path.GetExtension(vm.File.FileName)}";
                    var savePath = Path.Combine(uploadsRoot, stored);

                    using (var fs = System.IO.File.Create(savePath))
                        await vm.File.CopyToAsync(fs);

                    claim.AttachmentFileName = vm.File.FileName; // user-facing
                    claim.AttachmentStoredName = stored;          // on-disk
                    await _db.SaveChangesAsync();
                }

                TempData["ok"] = "Claim submitted successfully.";
                return RedirectToAction(nameof(Status), new { id = claim.Id });
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Submit failed");
                ModelState.AddModelError(string.Empty, "Sorry, something went wrong while submitting your claim.");
                return View(vm);
            }
        }

        // ---------------------------------------------------------------------
        // COORDINATOR REVIEW (Submitted)  → forward to Manager (PendingReview)
        // ---------------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> CoordinatorReview()
        {
            var items = await _db.Claims
                .AsNoTracking()
                .Where(c => c.Status == ClaimStatus.Submitted)
                .OrderByDescending(c => c.Id)
                .ToListAsync();

            // reuse your Review.cshtml or create a dedicated Coordinator view
            return View("Review", items);
        }

        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<IActionResult> CoordinatorApprove(int id)
        {
            try
            {
                var claim = await _db.Claims.FindAsync(id);
                if (claim is null) return NotFound();

                var ok = TryTransition(claim, from: ClaimStatus.Submitted, to: ClaimStatus.PendingReview);
                if (!ok)
                {
                    TempData["err"] = "Only newly submitted claims can be forwarded for manager review.";
                    return RedirectToAction(nameof(CoordinatorReview));
                }

                await _db.SaveChangesAsync();
                TempData["ok"] = "Forwarded to manager for review.";
                return RedirectToAction(nameof(CoordinatorReview));
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "CoordinatorApprove failed for claim {Id}", id);
                TempData["err"] = "Could not forward the claim due to an internal error.";
                return RedirectToAction(nameof(CoordinatorReview));
            }
        }

        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<IActionResult> CoordinatorReject(int id, string? reason)
        {
            try
            {
                var claim = await _db.Claims.FindAsync(id);
                if (claim is null) return NotFound();

                var ok = TryTransition(claim, from: ClaimStatus.Submitted, to: ClaimStatus.Rejected);
                if (!ok)
                {
                    TempData["err"] = "Only newly submitted claims can be rejected by the coordinator.";
                    return RedirectToAction(nameof(CoordinatorReview));
                }

                await _db.SaveChangesAsync();
                TempData["ok"] = "Claim rejected by coordinator.";
                return RedirectToAction(nameof(CoordinatorReview));
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "CoordinatorReject failed for claim {Id}", id);
                TempData["err"] = "Could not reject the claim due to an internal error.";
                return RedirectToAction(nameof(CoordinatorReview));
            }
        }

        [HttpGet]
        public async Task<IActionResult> ManagerReview()
        {
            var items = await _db.Claims
                .AsNoTracking()
                .Where(c => c.Status == ClaimStatus.PendingReview)
                .OrderByDescending(c => c.Id)
                .ToListAsync();

            return View(items); 
        }

        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<IActionResult> ManagerApprove(int id)
        {
            try
            {
                var claim = await _db.Claims.FindAsync(id);
                if (claim is null) return NotFound();

                var ok = TryTransition(claim, from: ClaimStatus.PendingReview, to: ClaimStatus.Approved);
                if (!ok)
                {
                    TempData["err"] = "Only claims pending review can be approved by the manager.";
                    return RedirectToAction(nameof(ManagerReview));
                }

                await _db.SaveChangesAsync();
                TempData["ok"] = "✅ Claim approved.";
                return RedirectToAction(nameof(ManagerReview));
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "ManagerApprove failed for claim {Id}", id);
                TempData["err"] = "Could not approve the claim due to an internal error.";
                return RedirectToAction(nameof(ManagerReview));
            }
        }

        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<IActionResult> ManagerReject(int id, string? reason)
        {
            try
            {
                var claim = await _db.Claims.FindAsync(id);
                if (claim is null) return NotFound();

                var ok = TryTransition(claim, from: ClaimStatus.PendingReview, to: ClaimStatus.Rejected);
                if (!ok)
                {
                    TempData["err"] = "Only claims pending review can be rejected by the manager.";
                    return RedirectToAction(nameof(ManagerReview));
                }

                await _db.SaveChangesAsync();
                TempData["ok"] = "⚠️ Claim rejected.";
                return RedirectToAction(nameof(ManagerReview));
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "ManagerReject failed for claim {Id}", id);
                TempData["err"] = "Could not reject the claim due to an internal error.";
                return RedirectToAction(nameof(ManagerReview));
            }
        }

        [HttpGet]
        public Task<IActionResult> Review() => CoordinatorReview();

        [HttpGet]
        public async Task<IActionResult> Status(int id)
        {
            Models.Claim? claim = null;

            if (id > 0)
            {
                claim = await _db.Claims
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == id);
            }

            if (claim is null)
            {
                claim = new Models.Claim 
                {
                    LecturerName = "—",
                    Month = "—",
                    HoursWorked = 0,
                    HourlyRate = 0,
                    Status = ClaimStatus.Submitted
                };
            }

            return View(claim);
        }

        [HttpGet]
        public async Task<IActionResult> StatusJson(int id)
        {
            var c = await _db.Claims.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (c is null) return NotFound();
            return Json(new
            {
                id = c.Id,
                lecturer = c.LecturerName,
                month = c.Month,
                status = c.Status.ToString()
            });
        }

        [HttpGet]
        public async Task<IActionResult> StatusList()
        {
            var items = await _db.Claims.AsNoTracking()
                .OrderByDescending(x => x.Id)
                .Take(100)
                .ToListAsync();

            return View(items);
        }

        private static bool TryTransition(Models.Claim claim, ClaimStatus from, ClaimStatus to)
        {
            if (claim.Status != from) return false;
            claim.Status = to;
            return true;
        }
    }
}
