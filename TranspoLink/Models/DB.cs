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

    public DbSet<Route> Routes { get; set; }
    public DbSet<Trip> Trips { get; set; }
    public DbSet<Vehicle> Vehicles { get; set; }
    public DbSet<Booking> Bookings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique()
            .HasFilter("[Email] IS NOT NULL");

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Phone)
            .IsUnique()
            .HasFilter("[Phone] IS NOT NULL");

        modelBuilder.Entity<Booking>()
           .HasOne(b => b.Member)
           .WithMany(m => m.Bookings)
           .HasForeignKey(b => b.MemberId)
           .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Booking>()
            .HasOne(b => b.Trip)
            .WithMany(t => t.Bookings)
            .HasForeignKey(b => b.TripId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Trip>()
            .HasOne(t => t.Route)
            .WithMany(r => r.Trips)
            .HasForeignKey(t => t.RouteId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Trip>()
            .HasOne(t => t.Vehicle)
            .WithMany(v => v.Trips)
            .HasForeignKey(t => t.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

// Entity Classes -------------------------------------------------------------

public class User
{
    // CHANGED: Id is now String and Manual Entry
    [Key, MaxLength(5)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public string Id { get; set; }

    [MaxLength(100)]
    [EmailAddress]
    public string? Email { get; set; }

    [MaxLength(20)]
    [Phone]
    public string? Phone { get; set; }

    [MaxLength(100)]
    public string Hash { get; set; }

    [MaxLength(100)]
    public string Name { get; set; }

    public string Role => GetType().Name;
}

public class Admin : User
{
    [MaxLength(100)]
    public string? PhotoURL { get; set; }
}

public class Member : User
{
    [MaxLength(100)]
    public string PhotoURL { get; set; }
    public virtual ICollection<Booking> Bookings { get; set; }
}

public class Route
{
    [Key]
    public int Id { get; set; }

    [MaxLength(100)]
    public string Origin { get; set; }

    [MaxLength(100)]
    public string Destination { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal BasePrice { get; set; }

    public int durationMinutes { get; set; }

    [MaxLength(20)]
    public string TransportType { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Trip> Trips { get; set; }
}

public class Trip
{
    [Key]
    public int Id { get; set; }

    public int RouteId { get; set; }
    public int VehicleId { get; set; }

    public DateTime DepartureTime { get; set; }
    public DateTime ArrivalTime { get; set; }

    public int AvailableSeats { get; set; }

    [Column(TypeName = ("decimal(10,2)"))]
    public decimal Price { get; set; }

    [MaxLength(20)]
    public string Status { get; set; }

    public virtual Route Route { get; set; }
    public virtual Vehicle Vehicle { get; set; }
    public virtual ICollection<Booking> Bookings { get; set; }
}

public class Vehicle
{
    [Key]
    public int Id { get; set; }

    [MaxLength(20)]
    public string VehicleNumber { get; set; }

    [MaxLength(20)]
    public string Type { get; set; } // Bus, Train, Ferry

    public int TotalSeats { get; set; }

    [MaxLength(50)]
    public string Operator { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<Trip> Trips { get; set; }
}

public class Booking
{
    [Key]
    public int Id { get; set; }

    // CHANGED: FK to Member is now string
    [MaxLength(5)]
    public string MemberId { get; set; }

    public int TripId { get; set; }

    public DateTime BookingDate { get; set; } = DateTime.Now;

    public int NumberOfSeats { get; set; } = 1;

    [Column(TypeName = "decimal(10,2)")]
    public decimal TotalPrice { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } // Pending, Confirmed, Cancelled, Completed

    public bool IsPaid { get; set; } = false;

    [MaxLength(50)]
    public string? BookingReference { get; set; }

    public virtual Member Member { get; set; }
    public virtual Trip Trip { get; set; }
}