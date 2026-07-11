using BudgetPilot_API.Entities;
using BudgetPilot_API.Dtos;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Provides business logic for role operations including CRUD, pagination,
/// and uniqueness enforcement.
/// </summary>
public class RolesService
{
    private readonly AppDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="RolesService"/> class
    /// with the specified database context.
    /// </summary>
    /// <param name="context">The application database context for accessing the roles table.</param>
    public RolesService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Retrieves a paginated list of roles, optionally filtered by a partial name match.
    /// </summary>
    /// <param name="page">The one-based page number to retrieve.</param>
    /// <param name="pageSize">The number of items per page (1–100).</param>
    /// <param name="search">Optional partial match on the role name.</param>
    /// <returns>A tuple containing the list of roles and the total matching count.</returns>
    public async Task<(List<RolesOBJ> Items, int TotalCount)> GetRoles(
        int page, int pageSize, string? search)
    {
        var query = _context.Roles.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(r => r.Name.Contains(search));

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(r => r.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    /// <summary>
    /// Retrieves a single role by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the role to retrieve.</param>
    /// <returns>
    /// The matching role if found; otherwise, <see langword="null" />.
    /// </returns>
    public async Task<RolesOBJ?> GetRoleById(Guid id)
    {
        return await _context.Roles.FirstOrDefaultAsync(r => r.Id == id);
    }

    /// <summary>
    /// Creates a new role after verifying that the name is unique.
    /// </summary>
    /// <param name="dto">The role data provided by the client.</param>
    /// <returns>
    /// A tuple containing the newly created role and a flag indicating whether
    /// a conflict with an existing role was detected.
    /// </returns>
    public async Task<(RolesOBJ? Role, bool IsConflict)> CreateRole(RolesDTO dto)
    {
        var exists = await _context.Roles
            .AnyAsync(r => r.Name == dto.Name);

        if (exists)
            return (null, true);

        var role = new RolesOBJ
        {
            Id = Guid.NewGuid(),
            Name = dto.Name
        };

        _context.Roles.Add(role);
        await _context.SaveChangesAsync();

        return (role, false);
    }

    /// <summary>
    /// Updates an existing role with new values after ensuring the new name
    /// does not conflict with another role.
    /// </summary>
    /// <param name="id">The unique identifier of the role to update.</param>
    /// <param name="dto">The updated role data provided by the client.</param>
    /// <returns>
    /// A tuple containing the updated role and a flag indicating whether
    /// a conflict with another role was detected. If the role is not found,
    /// both values signal the not-found condition.
    /// </returns>
    public async Task<(RolesOBJ? Role, bool IsConflict)> UpdateRole(Guid id, RolesDTO dto)
    {
        var duplicateExists = await _context.Roles
            .AnyAsync(r => r.Id != id && r.Name == dto.Name);

        if (duplicateExists)
            return (null, true);

        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == id);

        if (role == null)
            return (null, false);

        role.Name = dto.Name;

        await _context.SaveChangesAsync();

        return (role, false);
    }

    /// <summary>
    /// Permanently deletes a role from the database after checking for linked users.
    /// </summary>
    /// <param name="id">The unique identifier of the role to delete.</param>
    /// <returns>
    /// A tuple indicating whether the role was deleted and whether it has
    /// linked users that prevent deletion.
    /// </returns>
    public async Task<(bool Deleted, bool HasConflict)> DeleteRole(Guid id)
    {
        var hasUsers = await _context.Users
            .AnyAsync(u => u.RoleId == id);

        if (hasUsers)
            return (false, true);

        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == id);

        if (role == null)
            return (false, false);

        _context.Roles.Remove(role);
        await _context.SaveChangesAsync();

        return (true, false);
    }
}
