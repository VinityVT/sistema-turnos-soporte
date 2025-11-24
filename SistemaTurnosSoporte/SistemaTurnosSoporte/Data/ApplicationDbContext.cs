using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SistemaSoporte.Models;

namespace SistemaSoporte.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Ticket> Tickets { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ApplicationUser>(entity =>
            {
                entity.Property(u => u.FechaRegistro)
                      .HasDefaultValueSql("GETDATE()");

                entity.Property(u => u.FotoPerfil)
                      .HasDefaultValue(string.Empty);

                entity.Property(u => u.NombreCompleto)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(u => u.Departamento)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.Property(u => u.TipoUsuario)
                      .IsRequired()
                      .HasMaxLength(20);
            });

            builder.Entity<Ticket>(entity =>
            {
                entity.HasOne(t => t.Usuario)
                      .WithMany(u => u.SolicitudesCreadas)
                      .HasForeignKey(t => t.UsuarioId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(t => t.TecnicoAsignado)
                      .WithMany(u => u.SolicitudesAsignadas)
                      .HasForeignKey(t => t.TecnicoAsignadoId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}