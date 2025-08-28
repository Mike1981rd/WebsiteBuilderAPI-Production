using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebsiteBuilderAPI.Data;

namespace WebsiteBuilderAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SetupController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SetupController> _logger;

        public SetupController(ApplicationDbContext context, ILogger<SetupController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Inicializa o actualiza los permisos del sistema
        /// </summary>
        [HttpPost("seed-permissions")]
        [AllowAnonymous] // Temporalmente permitir sin auth para configuración inicial
        public async Task<IActionResult> SeedPermissions()
        {
            try
            {
                _logger.LogInformation("Starting permissions seeding...");
                
                // Seed permissions
                await PermissionsSeeder.SeedPermissionsAsync(_context);
                _logger.LogInformation("Permissions seeded successfully");

                // Seed roles with permissions
                await PermissionsSeeder.SeedRolesAsync(_context);
                _logger.LogInformation("Roles seeded successfully");

                // Create default super admin user
                await PermissionsSeeder.SeedDefaultUserAsync(_context);
                _logger.LogInformation("Default user created successfully");

                return Ok(new 
                { 
                    message = "Permissions, roles and default user seeded successfully",
                    details = new
                    {
                        defaultUser = "admin@websitebuilder.com",
                        defaultPassword = "Admin@123",
                        note = "Please change the default password after first login"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding permissions");
                return StatusCode(500, new { message = "Error seeding permissions", error = ex.Message });
            }
        }

        /// <summary>
        /// Obtiene estadísticas del sistema de permisos
        /// </summary>
        [HttpGet("permissions-stats")]
        [Authorize]
        public async Task<IActionResult> GetPermissionsStats()
        {
            try
            {
                var permissionsCount = await _context.Permissions.CountAsync();
                var rolesCount = await _context.Roles.CountAsync();
                var usersCount = await _context.Users.CountAsync();
                var userRolesCount = await _context.UserRoles.CountAsync();

                var stats = new
                {
                    totalPermissions = permissionsCount,
                    totalRoles = rolesCount,
                    totalUsers = usersCount,
                    totalUserRoleAssignments = userRolesCount,
                    systemRoles = await _context.Roles.Where(r => r.IsSystemRole).Select(r => r.Name).ToListAsync(),
                    resources = await _context.Permissions.Select(p => p.Resource).Distinct().OrderBy(r => r).ToListAsync()
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting permissions stats");
                return StatusCode(500, new { message = "Error getting stats", error = ex.Message });
            }
        }

        /// <summary>
        /// Debug endpoint to check role status
        /// </summary>
        [HttpGet("debug-roles")]
        [AllowAnonymous]
        public async Task<IActionResult> DebugRoles()
        {
            var roles = await _context.Roles
                .Select(r => new { r.Id, r.Name, r.IsSystemRole })
                .OrderBy(r => r.Name)
                .ToListAsync();

            var adminUser = _context.Users
                .Where(u => u.Email == "admin@websitebuilder.com")
                .Select(u => new 
                { 
                    u.Id, 
                    u.Email, 
                    Roles = u.UserRoles.Select(ur => ur.Role.Name).ToList() 
                })
                .FirstOrDefault();

            return Ok(new { roles, adminUser });
        }

        /// <summary>
        /// Debug specific role with permissions
        /// </summary>
        [HttpGet("debug-role/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> DebugRole(int id)
        {
            var role = await _context.Roles
                .Where(r => r.Id == id)
                .Select(r => new 
                { 
                    r.Id, 
                    r.Name, 
                    r.Description,
                    r.IsSystemRole,
                    Permissions = r.RolePermissions.Select(rp => new 
                    {
                        rp.PermissionId,
                        rp.Permission.Resource,
                        rp.Permission.Action
                    }).ToList()
                })
                .FirstOrDefaultAsync();

            return Ok(role);
        }

        /// <summary>
        /// Reset permissions to only have read, write, create
        /// </summary>
        [HttpPost("reset-permissions")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPermissions()
        {
            try
            {
                // Clear all existing permissions and role permissions
                _context.RolePermissions.RemoveRange(_context.RolePermissions);
                _context.Permissions.RemoveRange(_context.Permissions);
                await _context.SaveChangesAsync();

                // Re-seed permissions with only 3 actions
                await PermissionsSeeder.SeedPermissionsAsync(_context);
                
                // Re-seed roles with updated permissions
                await PermissionsSeeder.SeedRolesAsync(_context);

                return Ok(new 
                { 
                    message = "Permissions reset successfully to read, write, create only",
                    permissionCount = _context.Permissions.Count()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting permissions");
                return StatusCode(500, new { message = "Error resetting permissions", error = ex.Message });
            }
        }

        /// <summary>
        /// Fix system roles - only SuperAdmin and Administrator should be system roles
        /// </summary>
        [HttpPost("fix-system-roles")]
        [AllowAnonymous]
        public async Task<IActionResult> FixSystemRoles()
        {
            try
            {
                // Update all roles to non-system except SuperAdmin and Administrator
                var rolesToUpdate = _context.Roles
                    .Where(r => r.Name != "SuperAdmin" && r.Name != "Administrator")
                    .ToList();

                foreach (var role in rolesToUpdate)
                {
                    role.IsSystemRole = false;
                }

                // Ensure SuperAdmin and Administrator are system roles
                var systemRoles = _context.Roles
                    .Where(r => r.Name == "SuperAdmin" || r.Name == "Administrator")
                    .ToList();

                foreach (var role in systemRoles)
                {
                    role.IsSystemRole = true;
                }

                await _context.SaveChangesAsync();

                var updatedRoles = _context.Roles
                    .Select(r => new { r.Name, r.IsSystemRole })
                    .OrderBy(r => r.Name)
                    .ToList();

                return Ok(new 
                { 
                    message = "System roles fixed successfully",
                    roles = updatedRoles
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fixing system roles");
                return StatusCode(500, new { message = "Error fixing system roles", error = ex.Message });
            }
        }
    }
}