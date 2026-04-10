using System;
using System.ComponentModel.DataAnnotations;

namespace TourGuideApi.Models;

/// <summary>
/// Admin/User model with RBAC (Role-Based Access Control)
/// Roles: Admin, Editor, Viewer
/// </summary>
public class User
{
    [Key]
    public int Id { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// User role: "Admin" | "Editor" | "Viewer"
    /// Admin: Full access, can manage users
    /// Editor: Can create/edit content and manage localizations
    /// Viewer: Read-only access
    /// </summary>
    public string Role { get; set; } = "Viewer";

    /// <summary>
    /// Whether the user account is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// JWT token issued at this time (for token refresh tracking)
    /// </summary>
    public DateTime? LastTokenIssuedAt { get; set; }

    /// <summary>
    /// When the user account was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the user account was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
