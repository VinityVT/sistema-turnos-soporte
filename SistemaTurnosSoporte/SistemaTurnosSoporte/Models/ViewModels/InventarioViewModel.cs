using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SistemaSoporte.Models.ViewModels
{
    public class InventarioViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es requerido")]
        [StringLength(100, ErrorMessage = "El nombre no puede exceder 100 caracteres")]
        public string Nombre { get; set; } = null!;

        [Required(ErrorMessage = "El tipo es requerido")]
        [StringLength(50, ErrorMessage = "El tipo no puede exceder 50 caracteres")]
        public string Tipo { get; set; } = null!;

        [StringLength(500, ErrorMessage = "La descripción no puede exceder 500 caracteres")]
        public string? Descripcion { get; set; }

        [Required(ErrorMessage = "El stock actual es requerido")]
        [Range(0, int.MaxValue, ErrorMessage = "El stock actual no puede ser negativo")]
        public int StockActual { get; set; }

        [Required(ErrorMessage = "El stock mínimo es requerido")]
        [Range(1, int.MaxValue, ErrorMessage = "El stock mínimo debe ser al menos 1")]
        public int StockMinimo { get; set; }

        public DateTime FechaRegistro { get; set; }
        public DateTime FechaActualizacion { get; set; }
        public bool Activo { get; set; }
        public string EstadoStock { get; set; } = null!;
    }

    public class MovimientoInventarioViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El artículo es requerido")]
        public int InventarioId { get; set; }

        [Required(ErrorMessage = "El tipo de movimiento es requerido")]
        public string TipoMovimiento { get; set; } = null!;

        [Required(ErrorMessage = "La cantidad es requerida")]
        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser al menos 1")]
        public int Cantidad { get; set; }

        [Required(ErrorMessage = "El motivo es requerido")]
        [StringLength(500, ErrorMessage = "El motivo no puede exceder 500 caracteres")]
        public string Motivo { get; set; } = null!;

        public string? UsuarioNombre { get; set; }
        public DateTime FechaMovimiento { get; set; }
        public string NombreArticulo { get; set; } = null!;
    }

    public class MovimientoInventarioCreacionViewModel
    {
        [Required]
        public int InventarioId { get; set; }

        [Required]
        public string TipoMovimiento { get; set; } = null!;

        [Required]
        [Range(1, int.MaxValue)]
        public int Cantidad { get; set; }

        [Required]
        [StringLength(500)]
        public string Motivo { get; set; } = null!;
    }

    public class InventarioDashboardViewModel
    {
        public int TotalArticulos { get; set; }
        public int EnStock { get; set; }
        public int BajoStock { get; set; }
        public int SinStock { get; set; }
        public List<InventarioViewModel> ArticulosStockBajo { get; set; } = new List<InventarioViewModel>();
        public List<MovimientoInventarioViewModel> MovimientosRecientes { get; set; } = new List<MovimientoInventarioViewModel>();
    }
}