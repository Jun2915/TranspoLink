using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TranspoLink.Models;

#nullable disable warnings

public class ChatMessage
{
    public int Id { get; set; }
    [MaxLength(100)] public string SenderId { get; set; }   
    [MaxLength(100)] public string ReceiverId { get; set; } 
    [MaxLength(100)] public string SenderName { get; set; } 
    public string Message { get; set; }
    [MaxLength(200)] public string? PhotoUrl { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public bool IsRead { get; set; } = false;
}

public class DB(DbContextOptions options) : DbContext(options)
{
    public DbSet<User> Users { get; set; }
    public DbSet<Admin> Admins { get; set; }
    public DbSet<Member> Members { get; set; }
    public DbSet<Driver> Drivers { get; set; } 

    public DbSet<Route> Routes { get; set; }
    public DbSet<RouteStop> RouteStops { get; set; }
    public DbSet<Trip> Trips { get; set; }
    public DbSet<TripStop> TripStops { get; set; }
    public DbSet<Vehicle> Vehicles { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }  
    public DbSet<ChatMessage> ChatMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User Indexes
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique().HasFilter("[Email] IS NOT NULL");
        modelBuilder.Entity<User>().HasIndex(u => u.Phone).IsUnique().HasFilter("[Phone] IS NOT NULL");

        // Relationships
        modelBuilder.Entity<Booking>()
           .HasOne(b => b.Member).WithMany(m => m.Bookings).HasForeignKey(b => b.MemberId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Booking>()
            .HasOne(b => b.Trip).WithMany(t => t.Bookings).HasForeignKey(b => b.TripId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Trip>()
            .HasOne(t => t.Route).WithMany(r => r.Trips).HasForeignKey(t => t.RouteId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Trip>()
            .HasOne(t => t.Vehicle).WithMany(v => v.Trips).HasForeignKey(t => t.VehicleId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RouteStop>()
            .HasOne(rs => rs.Route).WithMany(r => r.RouteStops).HasForeignKey(rs => rs.RouteId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TripStop>()
            .HasOne(ts => ts.Trip).WithMany(t => t.TripStops).HasForeignKey(ts => ts.TripId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TripStop>()
            .HasOne(ts => ts.RouteStop).WithMany().HasForeignKey(ts => ts.RouteStopId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Vehicle>()
            .HasOne(v => v.Driver)
            .WithOne(d => d.Vehicle)
            .HasForeignKey<Vehicle>(v => v.DriverId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class User
{
    [Key, MaxLength(5)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public string Id { get; set; }
    [MaxLength(100), EmailAddress] public string? Email { get; set; }
    [MaxLength(20), Phone] public string? Phone { get; set; }
    [MaxLength(100)] public string Hash { get; set; }
    [MaxLength(100)] public string Name { get; set; }
    public int LoginRetryCount { get; set; } = 0;
    public DateTime? LockoutEnd { get; set; }
    public bool IsBlocked { get; set; } = false;
    public string Role => GetType().Name;
}

public class Admin : User { [MaxLength(100)] public string? PhotoURL { get; set; } }
public class Member : User { [MaxLength(100)] public string PhotoURL { get; set; } public virtual ICollection<Booking> Bookings { get; set; } }

public class Driver : User
{
    [MaxLength(100)] public string? PhotoURL { get; set; }
    [MaxLength(20)] public string LicenseNumber { get; set; }
    public virtual Vehicle? Vehicle { get; set; }
}

public class Route
{
    [Key, MaxLength(5)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public string Id { get; set; }
    [MaxLength(100)] public string Origin { get; set; }
    [MaxLength(100)] public string Destination { get; set; }
    [Column(TypeName = "decimal(10,2)")] public decimal BasePrice { get; set; }
    public int durationMinutes { get; set; }
    [MaxLength(20)] public string TransportType { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual ICollection<Trip> Trips { get; set; }
    public virtual ICollection<RouteStop> RouteStops { get; set; }
}

public class RouteStop
{
    [Key, MaxLength(6)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public string Id { get; set; }

    [MaxLength(5)] public string RouteId { get; set; }
    [MaxLength(100)] public string StopName { get; set; }
    public int Sequence { get; set; }
    public int MinutesFromStart { get; set; }

    public virtual Route Route { get; set; }
}

public class Trip
{
    [Key, MaxLength(5)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public string Id { get; set; }

    [MaxLength(5)] public string RouteId { get; set; }
    public int VehicleId { get; set; }

    public DateTime DepartureTime { get; set; }
    public DateTime ArrivalTime { get; set; }
    public int AvailableSeats { get; set; }
    [Column(TypeName = ("decimal(10,2)"))] public decimal Price { get; set; }
    [MaxLength(20)] public string Status { get; set; }

    public virtual Route Route { get; set; }
    public virtual Vehicle Vehicle { get; set; }
    public virtual ICollection<Booking> Bookings { get; set; }
    public virtual ICollection<TripStop> TripStops { get; set; }
}

public class TripStop
{
    [Key, MaxLength(6)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public string Id { get; set; }

    [MaxLength(5)] public string TripId { get; set; }
    [MaxLength(6)] public string RouteStopId { get; set; }

    public DateTime ScheduledArrival { get; set; }
    public DateTime? ActualArrival { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "Scheduled";

    public virtual Trip Trip { get; set; }
    public virtual RouteStop RouteStop { get; set; }
}

public class Vehicle
{
    [Key] public int Id { get; set; }
    [MaxLength(20)] public string VehicleNumber { get; set; }
    [MaxLength(20)] public string Type { get; set; }
    public int TotalSeats { get; set; }

    [MaxLength(5)]
    public string? DriverId { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<Trip> Trips { get; set; }
    public virtual Driver? Driver { get; set; }
}

public class Booking
{
    [Key] public int Id { get; set; }
    [MaxLength(5)] public string MemberId { get; set; }
    [MaxLength(5)] public string TripId { get; set; }
    public DateTime BookingDate { get; set; } = DateTime.Now;
    public int NumberOfSeats { get; set; } = 1;
    [Column(TypeName = "decimal(10,2)")] public decimal TotalPrice { get; set; }
    [MaxLength(20)] public string Status { get; set; }
    public bool IsPaid { get; set; } = false;
    [MaxLength(50)] public string? BookingReference { get; set; }
    public virtual Member Member { get; set; }
    public virtual Trip Trip { get; set; }
}

public class AuditLog
{
    public int Id { get; set; }
    [MaxLength(5)] public string AdminId { get; set; }
    [MaxLength(50)] public string Action { get; set; }
    [MaxLength(100)] public string Details { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    [MaxLength(10)] public string Icon { get; set; }
    [MaxLength(20)] public string CssClass { get; set; }
}