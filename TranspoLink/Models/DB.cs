using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TranspoLink.Models;

#nullable disable warnings

public class DB(DbContextOptions options) : DbContext(options)
{
    // DB Sets
    public DbSet<User> Users { get; set; }
    public DbSet<Admin> Admins { get; set; }
    public DbSet<Member> Members { get; set; }

    public DbSet<Type> Types { get; set; }
    public DbSet<Room> Rooms { get; set; }
    public DbSet<Reservation> Reservations { get; set; }
}

// Entity Classes -------------------------------------------------------------

public class User
{
    [Key, MaxLength(100)]
    public string Email { get; set; }
    [MaxLength(100)]
    public string Hash { get; set; }
    [MaxLength(100)]
    public string Name { get; set; }

    public string Role => GetType().Name;
}

public class Admin : User
{

}

public class Member : User
{
    [MaxLength(100)]
    public string PhotoURL { get; set; }

    // Navigation Properties
    public List<Reservation> Reservations { get; set; } = [];
}

// Type, Room, Reservation

public class Type
{
    [Key, MaxLength(1)]
    public string Id { get; set; }

    [MaxLength(100)]
    public string Name { get; set; }

    [Precision(6, 2)]
    public decimal Price { get; set; }

    // Navigation Properties
    public List<Room> Rooms { get; set; } = [];
}

public class Room
{
    [Key, MaxLength(4)]
    public string Id { get; set; }

    // Foreign Keys
    public string TypeId { get; set; }

    // Navigation Properties
    public Type Type { get; set; }
    public List<Reservation> Reservations { get; set; } = [];
}

public class Reservation
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public DateOnly CheckIn { get; set; }

    public DateOnly CheckOut { get; set; }

    [Precision(6, 2)]
    public decimal Price { get; set; }

    public bool Paid { get; set; }

    // Foreign Keys (MemberEmail, RoomId)
    public string MemberEmail { get; set; }
    public string RoomId { get; set; }

    // Navigation Properties
    public Member Member { get; set; }
    public Room Room { get; set; }
}
